using System.Collections;
using System.Collections.Generic;

namespace TotalFog.Core;

public static class GridGeometry
{
    public static SectionRange AffectedSections(
        int x,
        int z,
        int width,
        int height,
        int sectionSize
    ) =>
        new(
            (x > 0 ? x - 1 : 0) / sectionSize,
            (x + 1 < width ? x + 1 : width - 1) / sectionSize,
            (z > 0 ? z - 1 : 0) / sectionSize,
            (z + 1 < height ? z + 1 : height - 1) / sectionSize,
            (width + sectionSize - 1) / sectionSize
        );
}

public readonly struct SectionRange(int minX, int maxX, int minZ, int maxZ, int columns)
    : IEnumerable<int>
{
    public Enumerator GetEnumerator() => new(minX, maxX, minZ, maxZ, columns);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator(int minX, int maxX, int minZ, int maxZ, int columns) : IEnumerator<int>
    {
        private readonly int firstZ = minZ;
        private int x = minX - 1,
            z = minZ;
        public int Current => z * columns + x;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (++x > maxX)
            {
                x = minX;
                z++;
            }
            return z <= maxZ;
        }

        public void Reset()
        {
            x = minX - 1;
            z = firstZ;
        }

        public void Dispose() { }
    }
}
