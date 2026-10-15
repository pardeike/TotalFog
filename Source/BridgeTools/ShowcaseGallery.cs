using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using Steamworks;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Update only this mod's additional Workshop images, through the live owner session.</summary>
public sealed class ShowcaseGallery
{
    [Serializable]
    public sealed class Manifest
    {
        public Entry[] shots;
    }

    [Serializable]
    public sealed class Entry
    {
        public string id,
            jpgSha256;
    }

    public sealed class Preview
    {
        public uint index { get; set; }
        public string file { get; set; }
        public string url { get; set; }
        public string kind { get; set; }
    }

    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/showcase_gallery",
        Description = "Read or update Total Fog's additional Workshop screenshots and an optional already-uploaded YouTube preview. Verify the logged-on owner, app, item and manifest hashes. Update matching filenames/video IDs in place and retain other previews. No mod content, title, description, tags or primary preview changes. confirm=true submits."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string mediaDirectory,
        bool confirm = false,
        string youtubeVideoId = null,
        string previousYouTubeVideoId = null
    )
    {
        if (
            youtubeVideoId != null
            && !System.Text.RegularExpressions.Regex.IsMatch(youtubeVideoId, "^[A-Za-z0-9_-]{11}$")
        )
            throw new ArgumentException("Use a YouTube video ID, not a URL.");
        var manifest = JsonUtility.FromJson<Manifest>(
            File.ReadAllText(Path.Combine(mediaDirectory, "manifest.json"))
        );
        if (
            manifest.shots == null
            || manifest.shots.Length < 1
            || manifest.shots.Length > 12
            || manifest.shots.Select(s => s.id).Distinct().Count() != manifest.shots.Length
        )
            throw new ArgumentException(
                "Use a showcase manifest with one to twelve unique images."
            );
        var files = manifest
            .shots.OrderBy(s =>
                s.id == "03-colony" ? 0
                : s.id == "02-watchtower" ? 1
                : 2
            )
            .Select(s =>
            {
                if (
                    s.id != Path.GetFileName(s.id)
                    || !s.id.All(c => char.IsLetterOrDigit(c) || c == '-')
                )
                    throw new ArgumentException("Invalid image id.");
                var path = Path.GetFullPath(Path.Combine(mediaDirectory, s.id + ".jpg"));
                byte[] bytes = File.ReadAllBytes(path);
                using var sha = SHA256.Create();
                string hash = BitConverter
                    .ToString(sha.ComputeHash(bytes))
                    .Replace("-", "")
                    .ToLowerInvariant();
                if (bytes.Length >= 1000000 || hash != s.jpgSha256)
                    throw new InvalidOperationException(
                        "Workshop image differs from verified media: " + s.id
                    );
                return path;
            })
            .ToArray();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var item = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (!SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != 294100)
                        throw new InvalidOperationException(
                            "Use the logged-on Steam RimWorld session."
                        );
                    var root = LoadedModManager.GetMod<TotalFogMod>().Content.RootDir;
                    return new PublishedFileId_t(
                        ulong.Parse(
                            File.ReadAllText(Path.Combine(root, "About/PublishedFileId.txt")).Trim()
                        )
                    );
                },
                cancellationToken
            );
            var before = await Query();
            var videoIndex =
                before.FirstOrDefault(p => p.kind == "youtube" && p.url == youtubeVideoId)?.index
                ?? before
                    .FirstOrDefault(p =>
                        p.kind == "youtube"
                        && previousYouTubeVideoId != null
                        && p.url == previousYouTubeVideoId
                    )
                    ?.index;
            var changes = files
                .Select(path => new
                {
                    file = Path.GetFileName(path),
                    existingIndex = before
                        .FirstOrDefault(p => p.file == Path.GetFileName(path))
                        ?.index,
                })
                .ToArray();
            if (!confirm)
                return new
                {
                    success = true,
                    submitted = false,
                    publishedFileId = item.m_PublishedFileId,
                    youtubeVideoId,
                    videoIndex,
                    changes,
                    gallery = before,
                };

            var completion = new TaskCompletionSource<SubmitItemUpdateResult_t>();
            CallResult<SubmitItemUpdateResult_t> callback = null;
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        var update = SteamUGC.StartItemUpdate(new AppId_t(294100), item);
                        if (update == UGCUpdateHandle_t.Invalid)
                            throw new InvalidOperationException("Invalid Workshop update handle.");
                        for (int i = 0; i < files.Length; i++)
                        {
                            bool accepted = changes[i].existingIndex.HasValue
                                ? SteamUGC.UpdateItemPreviewFile(
                                    update,
                                    changes[i].existingIndex.Value,
                                    files[i]
                                )
                                : SteamUGC.AddItemPreviewFile(
                                    update,
                                    files[i],
                                    EItemPreviewType.k_EItemPreviewType_Image
                                );
                            if (!accepted)
                                throw new InvalidOperationException(
                                    "Steam rejected preview setup: " + changes[i].file
                                );
                        }
                        if (youtubeVideoId != null)
                        {
                            bool accepted = videoIndex.HasValue
                                ? SteamUGC.UpdateItemPreviewVideo(
                                    update,
                                    videoIndex.Value,
                                    youtubeVideoId
                                )
                                : SteamUGC.AddItemPreviewVideo(update, youtubeVideoId);
                            if (!accepted)
                                throw new InvalidOperationException(
                                    "Steam rejected the YouTube preview setup."
                                );
                        }
                        callback = CallResult<SubmitItemUpdateResult_t>.Create(
                            (result, failed) =>
                            {
                                if (failed)
                                    completion.TrySetException(
                                        new InvalidOperationException(
                                            "Steam gallery upload I/O failure."
                                        )
                                    );
                                else
                                    completion.TrySetResult(result);
                            }
                        );
                        var call = SteamUGC.SubmitItemUpdate(update, "");
                        if (call == SteamAPICall_t.Invalid)
                            throw new InvalidOperationException(
                                "Steam rejected gallery submission."
                            );
                        callback.Set(call);
                    },
                    cancellationToken
                );
                var result = await Complete(completion.Task);
                if (result.m_eResult != EResult.k_EResultOK)
                    throw new InvalidOperationException(
                        "Steam gallery result: " + result.m_eResult
                    );
                var after = await Query();
                return new
                {
                    success = (
                        youtubeVideoId == null
                        || after.Count(p => p.kind == "youtube" && p.url == youtubeVideoId) == 1
                    )
                        && changes.All(c =>
                            after.Count(p => p.file == c.file && !string.IsNullOrEmpty(p.url)) == 1
                        ),
                    submitted = true,
                    publishedFileId = item.m_PublishedFileId,
                    youtubeVideoId,
                    changes,
                    gallery = after,
                };
            }
            finally
            {
                await ctx.MainThread.InvokeAsync(() => callback?.Dispose(), CancellationToken.None);
            }

            async Task<List<Preview>> Query()
            {
                var done = new TaskCompletionSource<SteamUGCQueryCompleted_t>();
                CallResult<SteamUGCQueryCompleted_t> queryCallback = null;
                UGCQueryHandle_t query = UGCQueryHandle_t.Invalid;
                try
                {
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { item }, 1);
                            if (
                                query == UGCQueryHandle_t.Invalid
                                || !SteamUGC.SetReturnAdditionalPreviews(query, true)
                                || !SteamUGC.SetAllowCachedResponse(query, 0)
                            )
                                throw new InvalidOperationException(
                                    "Could not prepare a fresh Workshop gallery query."
                                );
                            queryCallback = CallResult<SteamUGCQueryCompleted_t>.Create(
                                (result, failed) =>
                                {
                                    if (failed)
                                        done.TrySetException(
                                            new InvalidOperationException(
                                                "Steam gallery query I/O failure."
                                            )
                                        );
                                    else
                                        done.TrySetResult(result);
                                }
                            );
                            queryCallback.Set(SteamUGC.SendQueryUGCRequest(query));
                        },
                        cancellationToken
                    );
                    var result = await Complete(done.Task);
                    if (
                        result.m_eResult != EResult.k_EResultOK
                        || result.m_unNumResultsReturned != 1
                    )
                        throw new InvalidOperationException(
                            "Workshop gallery query failed: " + result.m_eResult
                        );
                    return await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            if (
                                !SteamUGC.GetQueryUGCResult(query, 0, out var details)
                                || details.m_eResult != EResult.k_EResultOK
                                || details.m_nConsumerAppID.m_AppId != 294100
                                || details.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID
                                || details.m_nPublishedFileId != item
                            )
                                throw new InvalidOperationException(
                                    "Workshop item/app/owner verification failed."
                                );
                            var previews = new List<Preview>();
                            uint count = SteamUGC.GetQueryUGCNumAdditionalPreviews(query, 0);
                            for (uint i = 0; i < count; i++)
                            {
                                if (
                                    !SteamUGC.GetQueryUGCAdditionalPreview(
                                        query,
                                        0,
                                        i,
                                        out var url,
                                        4096,
                                        out var original,
                                        1024,
                                        out var previewType
                                    )
                                )
                                    throw new InvalidOperationException(
                                        "Could not read an existing preview."
                                    );
                                previews.Add(
                                    new Preview
                                    {
                                        index = i,
                                        file = Path.GetFileName(original),
                                        url = url,
                                        kind =
                                            previewType
                                            == EItemPreviewType.k_EItemPreviewType_YouTubeVideo
                                                ? "youtube"
                                                : "image",
                                    }
                                );
                            }
                            return previews;
                        },
                        cancellationToken
                    );
                }
                finally
                {
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            queryCallback?.Dispose();
                            if (query != UGCQueryHandle_t.Invalid)
                                SteamUGC.ReleaseQueryUGCRequest(query);
                        },
                        CancellationToken.None
                    );
                }
            }

            async Task<T> Complete<T>(Task<T> task)
            {
                if (await Task.WhenAny(task, Task.Delay(180000, cancellationToken)) != task)
                    throw new TimeoutException(
                        "Steam gallery callback timed out. Verify current Workshop state before retrying; do not assume a submitted update failed."
                    );
                return await task;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
