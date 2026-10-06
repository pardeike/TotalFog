using System;

namespace TotalFog.Core;

/// <summary>A reusable local footprint. Cells are global map indices at the boundary.</summary>
public sealed class VisibilityMask
{
    private ulong[] words = Array.Empty<ulong>();
    private int width,
        minX,
        minZ,
        height,
        mapWidth,
        wordsPerRow;
    public int Area => width * height;

    public void Reset(int x, int z, int width, int height, int mapWidth)
    {
        this.minX = x;
        this.minZ = z;
        this.width = width;
        this.height = height;
        this.mapWidth = mapWidth;
        _ = checked(width * height);
        wordsPerRow = checked(width + 63) / 64;
        int count = checked(wordsPerRow * height);
        if (words.Length < count)
            words = new ulong[count];
        else
            Array.Clear(words, 0, count);
    }

    public void Add(int index) => AddCell(index % mapWidth, index / mapWidth);

    public void AddCell(int x, int z)
    {
        x -= minX;
        z -= minZ;
        if ((uint)x < width && (uint)z < height)
            words[z * wordsPerRow + (x >> 6)] |= 1UL << (x & 63);
    }

    /// <summary>Union an inclusive horizontal run, clipped to this footprint.</summary>
    public void AddHorizontalRun(int firstX, int lastX, int z)
    {
        z -= minZ;
        if ((uint)z >= height || firstX > lastX || width == 0)
            return;
        int first = Math.Max(firstX, minX) - minX;
        int last = Math.Min(lastX, minX + width - 1) - minX;
        if (first > last)
            return;
        int firstWord = first >> 6,
            lastWord = last >> 6,
            row = z * wordsPerRow;
        ulong firstBits = ulong.MaxValue << (first & 63);
        ulong lastBits = ulong.MaxValue >> (63 - (last & 63));
        if (firstWord == lastWord)
            words[row + firstWord] |= firstBits & lastBits;
        else
        {
            words[row + firstWord] |= firstBits;
            for (int word = firstWord + 1; word < lastWord; word++)
                words[row + word] = ulong.MaxValue;
            words[row + lastWord] |= lastBits;
        }
    }

    public bool Contains(int index)
    {
        if (mapWidth == 0)
            return false;
        int x = index % mapWidth - minX,
            z = index / mapWidth - minZ;
        return (uint)x < width
            && (uint)z < height
            && (words[z * wordsPerRow + (x >> 6)] & (1UL << (x & 63))) != 0;
    }

    public bool At(int localIndex)
    {
        int x = localIndex % width,
            z = localIndex / width;
        return (words[z * wordsPerRow + (x >> 6)] & (1UL << (x & 63))) != 0;
    }

    public int GlobalIndex(int localIndex) =>
        (minZ + localIndex / width) * mapWidth + minX + localIndex % width;

    public void AddIntersection(VisibilityMask a, VisibilityMask b)
    {
        if (!SameBounds(a) || !SameBounds(b))
            throw new ArgumentException("Intersection footprints differ");
        int count = wordsPerRow * height;
        for (int i = 0; i < count; i++)
            words[i] |= a.words[i] & b.words[i];
    }

    private bool SameBounds(VisibilityMask other) =>
        width == other.width
        && height == other.height
        && minX == other.minX
        && minZ == other.minZ
        && mapWidth == other.mapWidth;

    public static void ApplyDifference(
        VisibilityMask previous,
        VisibilityMask next,
        Action<int, bool> change
    )
    {
        next.ReportDifference(previous, true, change);
        previous.ReportDifference(next, false, change);
    }

    private void ReportDifference(VisibilityMask other, bool visible, Action<int, bool> change)
    {
        // Compare 64 cells at once. Only changed cells reach the callback;
        // row shifts are aligned from at most two words of the other footprint.
        for (int row = 0; row < height; row++)
        {
            int globalRow = (minZ + row) * mapWidth + minX;
            for (int word = 0; word < wordsPerRow; word++)
            {
                ulong changed = words[row * wordsPerRow + word];
                if (mapWidth == other.mapWidth)
                    changed &= ~other.RowSlice(
                        minZ + row - other.minZ,
                        minX + word * 64 - other.minX
                    );
                while (changed != 0)
                {
                    int index = globalRow + word * 64 + TrailingZeros(changed);
                    if (mapWidth == other.mapWidth || !other.Contains(index))
                        change(index, visible);
                    changed &= changed - 1;
                }
            }
        }
    }

    private ulong RowSlice(int row, int column)
    {
        if ((uint)row >= height)
            return 0;
        int word = column >> 6,
            shift = column & 63,
            start = row * wordsPerRow;
        ulong result = (uint)word < wordsPerRow ? words[start + word] >> shift : 0;
        if (shift != 0 && (uint)(word + 1) < wordsPerRow)
            result |= words[start + word + 1] << (64 - shift);
        return result;
    }

    private static int TrailingZeros(ulong value)
    {
        // The game's runtime lacks BitOperations. The input is always nonzero.
        int count = 0;
        if ((value & uint.MaxValue) == 0)
        {
            count += 32;
            value >>= 32;
        }
        if ((value & 0xffff) == 0)
        {
            count += 16;
            value >>= 16;
        }
        if ((value & 0xff) == 0)
        {
            count += 8;
            value >>= 8;
        }
        if ((value & 0xf) == 0)
        {
            count += 4;
            value >>= 4;
        }
        if ((value & 3) == 0)
        {
            count += 2;
            value >>= 2;
        }
        if ((value & 1) == 0)
            count++;
        return count;
    }
}
