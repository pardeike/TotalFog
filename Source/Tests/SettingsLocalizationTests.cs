using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace TotalFog.Tests;

public sealed class SettingsLocalizationTests
{
    [Theory]
    [InlineData("TotalFog_ValueMultiplier")]
    [InlineData("TotalFog_ValuePercent")]
    [InlineData("TotalFog_ValueSeconds")]
    public void Unit_formats_have_one_numeric_placeholder_in_every_shipped_language(string key)
    {
        foreach (var file in Languages())
        {
            var value = XDocument.Load(file).Root.Element(key)?.Value;
            Assert.False(string.IsNullOrWhiteSpace(value), file + ": " + key);
            Assert.Equal(new[] { "{0}" }, Regex.Matches(value, @"\{[^}]+\}").Select(m => m.Value));
            Assert.DoesNotContain("{", string.Format(value, "1.5"));
        }
    }

    [Fact]
    public void Every_language_keeps_complete_keys_and_matching_format_arguments()
    {
        var files = Languages();
        Assert.Equal(14, files.Length);
        var english = XDocument
            .Load(
                files.Single(p =>
                    p.Contains(
                        Path.DirectorySeparatorChar + "English" + Path.DirectorySeparatorChar
                    )
                )
            )
            .Root.Elements()
            .ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var file in files)
        {
            var entries = XDocument.Load(file).Root.Elements().ToArray();
            Assert.Equal(entries.Length, entries.Select(e => e.Name).Distinct().Count());
            Assert.Equal(
                english.Keys.OrderBy(k => k),
                entries.Select(e => e.Name.LocalName).OrderBy(k => k)
            );
            foreach (var entry in entries)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value), file + ": " + entry.Name);
                Assert.Equal(Arguments(english[entry.Name.LocalName]), Arguments(entry.Value));
            }
        }
    }

    private static string[] Arguments(string value) =>
        Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(s => s).ToArray();

    private static string[] Languages()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Languages")))
            root = root.Parent;
        Assert.NotNull(root);
        return Directory.GetFiles(
            Path.Combine(root.FullName, "Languages"),
            "TotalFog.xml",
            SearchOption.AllDirectories
        );
    }
}
