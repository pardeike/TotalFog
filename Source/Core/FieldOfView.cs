#nullable enable
using System;

namespace TotalFog.Core;

/// <summary>Symmetric shadowcasting: floor centers, joined opaque diamonds, exact rational slopes.</summary>
public static class FieldOfView
{
    public static void Compute(
        int width,
        int height,
        int originX,
        int originZ,
        int radius,
        bool[] opaque,
        Action<int> reveal
    ) => ComputeField(width, height, originX, originZ, radius, opaque, reveal, null);

    /// <summary>Write coordinates directly into a reusable footprint without per-cell index conversion.</summary>
    public static void ComputeMask(
        int width,
        int height,
        int originX,
        int originZ,
        int radius,
        bool[] opaque,
        VisibilityMask destination
    ) => ComputeField(width, height, originX, originZ, radius, opaque, null, destination);

    private static void ComputeField(
        int width,
        int height,
        int originX,
        int originZ,
        int radius,
        bool[] opaque,
        Action<int>? reveal,
        VisibilityMask? destination
    )
    {
        if (opaque == null || opaque.Length != checked(width * height))
            throw new ArgumentException("Occlusion dimensions differ", nameof(opaque));
        if (radius < 0 || (uint)originX >= width || (uint)originZ >= height)
            return;
        if (destination != null)
            destination.AddCell(originX, originZ);
        else
            reveal!(originZ * width + originX);
        var caster = new Caster(
            width,
            height,
            originX,
            originZ,
            radius,
            opaque,
            reveal,
            destination: destination
        );
        for (int quadrant = 0; quadrant < 4; quadrant++)
            if (destination != null)
                caster.ScanMask(1, new Slope(-1, 1), new Slope(1, 1), quadrant);
            else
                caster.Scan(1, new Slope(-1, 1), new Slope(1, 1), quadrant);
    }

    public static bool CanSee(
        int width,
        int height,
        int originX,
        int originZ,
        int targetX,
        int targetZ,
        int radius,
        bool[] opaque
    )
    {
        if (opaque == null || opaque.Length != checked(width * height))
            throw new ArgumentException("Occlusion dimensions differ", nameof(opaque));
        if (
            radius < 0
            || (uint)originX >= width
            || (uint)originZ >= height
            || (uint)targetX >= width
            || (uint)targetZ >= height
        )
            return false;
        if (originX == targetX && originZ == targetZ)
            return true;
        int dx = targetX - originX,
            dz = targetZ - originZ;
        if ((long)dx * dx + (long)dz * dz > (long)radius * radius)
            return false;
        var caster = new Caster(
            width,
            height,
            originX,
            originZ,
            radius,
            opaque,
            null,
            targetZ * width + targetX,
            Math.Max(Math.Abs(dx), Math.Abs(dz))
        );
        int quadrant =
            Math.Abs(dx) > Math.Abs(dz)
                ? dx > 0
                    ? 1
                    : 3
                : dz > 0
                    ? 2
                    : 0;
        caster.Scan(1, new Slope(-1, 1), new Slope(1, 1), quadrant);
        return caster.TargetSeen;
    }

    private struct Caster
    {
        private readonly int width,
            height,
            originX,
            originZ,
            limit;
        private readonly long rangeSquared;
        private readonly bool[] opaque;
        private readonly Action<int>? reveal;
        private readonly VisibilityMask? destination;
        private readonly int target;
        public bool TargetSeen;

        public Caster(
            int width,
            int height,
            int originX,
            int originZ,
            int radius,
            bool[] opaque,
            Action<int>? reveal,
            int target = -1,
            int maximumDepth = int.MaxValue,
            VisibilityMask? destination = null
        )
        {
            this.width = width;
            this.height = height;
            this.originX = originX;
            this.originZ = originZ;
            this.opaque = opaque;
            this.reveal = reveal;
            this.target = target;
            TargetSeen = false;
            this.destination = destination;
            limit = Math.Min(maximumDepth, Math.Min(radius, Math.Max(width, height)));
            rangeSquared = (long)radius * radius;
        }

        public void Scan(int depth, Slope start, Slope end, int quadrant)
        {
            // Tail traversal avoids growing the stack through open terrain.
            while (depth <= limit && !TargetSeen)
            {
                if (!start.HasRoomTo(end))
                    return;
                int first = (int)Floor(
                    2L * depth * start.Numerator + start.Denominator,
                    2L * start.Denominator
                );
                int last = (int)
                    -Floor(-(2L * depth * end.Numerator - end.Denominator), 2L * end.Denominator);
                int wallFirst = first,
                    wallLast = last;
                // Additional cells can close a joined shadow. They cannot
                // reveal opaque targets outside the original sector.
                first = Math.Max(-depth, first - 1);
                last = Math.Min(depth, last + 1);
                long depthSquared = (long)depth * depth;
                bool hasPrevious = false,
                    previousOpaque = false;
                for (int column = first; column <= last; column++)
                {
                    int x = originX,
                        z = originZ;
                    switch (quadrant)
                    {
                        case 0:
                            x += column;
                            z -= depth;
                            break;
                        case 1:
                            x += depth;
                            z += column;
                            break;
                        case 2:
                            x += column;
                            z += depth;
                            break;
                        default:
                            x -= depth;
                            z += column;
                            break;
                    }
                    bool inBounds = (uint)x < width && (uint)z < height;
                    bool inRange = depthSquared + (long)column * column <= rangeSquared;
                    bool wall = !inBounds || !inRange || opaque[z * width + x];
                    if (hasPrevious && previousOpaque && !wall)
                        start = start.Later(TransitionSlope(depth, column, true, quadrant));
                    if (
                        inBounds
                        && inRange
                        && (
                            wall && column >= wallFirst && column <= wallLast
                            || !wall
                                && !ClosedQuadrantCorner(depth, column, quadrant)
                                && start.IncludesLower(column, depth)
                                && end.IncludesUpper(column, depth)
                        )
                    )
                    {
                        int index = z * width + x;
                        if (index == target)
                        {
                            TargetSeen = true;
                            return;
                        }
                        reveal?.Invoke(index);
                    }
                    if (hasPrevious && !previousOpaque && wall)
                        Scan(
                            depth + 1,
                            start,
                            end.Earlier(TransitionSlope(depth, column, false, quadrant)),
                            quadrant
                        );
                    previousOpaque = wall;
                    hasPrevious = true;
                }
                if (!hasPrevious || previousOpaque)
                    return;
                depth++;
            }
        }

        // Keep the mask writer separate so point queries pay no row-proof or
        // mask dispatch cost. Differential tests keep both geometries equal.
        public void ScanMask(int depth, Slope start, Slope end, int quadrant)
        {
            while (depth <= limit)
            {
                if (!start.HasRoomTo(end))
                    return;
                int first = (int)Floor(
                    2L * depth * start.Numerator + start.Denominator,
                    2L * start.Denominator
                );
                int last = (int)
                    -Floor(-(2L * depth * end.Numerator - end.Denominator), 2L * end.Denominator);
                int wallFirst = first,
                    wallLast = last;
                int visibleFirst = first + (start.IncludesLower(first, depth) ? 0 : 1);
                int visibleLast = last - (end.IncludesUpper(last, depth) ? 0 : 1);
                first = Math.Max(-depth, first - 1);
                last = Math.Min(depth, last + 1);
                long depthSquared = (long)depth * depth;
                // Short rows cost less to cast directly than to prove clear.
                if (
                    last - first >= 15
                    && (
                        (quadrant & 1) == 0
                            ? WriteOpenHorizontalRow(
                                depth,
                                depthSquared,
                                first,
                                last,
                                visibleFirst,
                                visibleLast,
                                quadrant
                            )
                            : WriteOpenVerticalRow(
                                depth,
                                depthSquared,
                                first,
                                last,
                                visibleFirst,
                                visibleLast,
                                quadrant
                            )
                    )
                )
                {
                    depth++;
                    continue;
                }
                bool hasPrevious = false,
                    previousOpaque = false;
                for (int column = first; column <= last; column++)
                {
                    int x = originX,
                        z = originZ;
                    switch (quadrant)
                    {
                        case 0:
                            x += column;
                            z -= depth;
                            break;
                        case 1:
                            x += depth;
                            z += column;
                            break;
                        case 2:
                            x += column;
                            z += depth;
                            break;
                        default:
                            x -= depth;
                            z += column;
                            break;
                    }
                    bool inBounds = (uint)x < width && (uint)z < height;
                    bool inRange = depthSquared + (long)column * column <= rangeSquared;
                    bool wall = !inBounds || !inRange || opaque[z * width + x];
                    if (hasPrevious && previousOpaque && !wall)
                    {
                        start = start.Later(TransitionSlope(depth, column, true, quadrant));
                        visibleFirst = Math.Max(
                            visibleFirst,
                            column + (start.IncludesLower(column, depth) ? 0 : 1)
                        );
                    }
                    if (
                        inBounds
                        && inRange
                        && (
                            wall && column >= wallFirst && column <= wallLast
                            || !wall
                                && !ClosedQuadrantCorner(depth, column, quadrant)
                                && column >= visibleFirst
                                && column <= visibleLast
                        )
                    )
                        destination!.AddCell(x, z);
                    if (hasPrevious && !previousOpaque && wall)
                        ScanMask(
                            depth + 1,
                            start,
                            end.Earlier(TransitionSlope(depth, column, false, quadrant)),
                            quadrant
                        );
                    previousOpaque = wall;
                    hasPrevious = true;
                }
                if (!hasPrevious || previousOpaque)
                    return;
                depth++;
            }
        }

        private bool ClosedQuadrantCorner(int depth, int column, int quadrant)
        {
            // At the shared quadrant edge, the wall on the right has not
            // been scanned yet. Its joined corner must also hide this floor.
            return (column == -depth || column == depth)
                && IsOpaque(depth - 1, column, quadrant)
                && IsOpaque(depth, column + (column < 0 ? 1 : -1), quadrant);
        }

        private Slope TransitionSlope(int depth, int column, bool previousOpaque, int quadrant)
        {
            int numerator = 2 * column - 1;
            // Extend only touching opaque neighbours to their shared square
            // corner. Isolated walls keep their existing diamond silhouette.
            int neighbourDepth =
                depth + ((previousOpaque ? numerator < 0 : numerator > 0) ? 1 : -1);
            if (
                IsOpaque(depth, column - (previousOpaque ? 1 : 0), quadrant)
                && IsOpaque(neighbourDepth, column - (previousOpaque ? 0 : 1), quadrant)
            )
                return new Slope(numerator, depth + neighbourDepth, true);
            return new Slope(numerator, 2 * depth);
        }

        private bool IsOpaque(int depth, int column, int quadrant)
        {
            int x = originX,
                z = originZ;
            switch (quadrant)
            {
                case 0:
                    x += column;
                    z -= depth;
                    break;
                case 1:
                    x += depth;
                    z += column;
                    break;
                case 2:
                    x += column;
                    z += depth;
                    break;
                default:
                    x -= depth;
                    z += column;
                    break;
            }
            return (uint)x < width && (uint)z < height && opaque[z * width + x];
        }

        private bool WriteOpenVerticalRow(
            int depth,
            long depthSquared,
            int first,
            int last,
            int visibleFirst,
            int visibleLast,
            int quadrant
        )
        {
            int x = quadrant == 1 ? originX + depth : originX - depth;
            int firstZ = originZ + first,
                lastZ = originZ + last;
            if (
                first > last
                || (uint)x >= width
                || (uint)firstZ >= height
                || (uint)lastZ >= height
                || depthSquared + Math.Max((long)first * first, (long)last * last) > rangeSquared
            )
                return false;
            // Only an entirely clear, in-range column may skip the normal
            // shadow transitions. Stop at its first blocker before writing.
            int endIndex = lastZ * width + x;
            for (int index = firstZ * width + x; index <= endIndex; index += width)
                if (opaque[index])
                    return false;
            destination!.AddVerticalRun(originZ + visibleFirst, originZ + visibleLast, x);
            return true;
        }

        private bool WriteOpenHorizontalRow(
            int depth,
            long depthSquared,
            int first,
            int last,
            int visibleFirst,
            int visibleLast,
            int quadrant
        )
        {
            int z = quadrant == 0 ? originZ - depth : originZ + depth;
            int firstX = originX + first,
                lastX = originX + last;
            // Out-of-bounds/range cells are walls to the normal caster. A run
            // can skip slope transitions only when every candidate is floor.
            if (
                first > last
                || (uint)z >= height
                || (uint)firstX >= width
                || (uint)lastX >= width
                || depthSquared + Math.Max((long)first * first, (long)last * last) > rangeSquared
                || new ReadOnlySpan<bool>(opaque, z * width + firstX, last - first + 1).IndexOf(
                    true
                ) >= 0
            )
                return false;
            destination!.AddHorizontalRun(originX + visibleFirst, originX + visibleLast, z);
            return true;
        }
    }

    private static long Floor(long numerator, long denominator)
    {
        long result = numerator / denominator;
        return numerator < 0 && numerator % denominator != 0 ? result - 1 : result;
    }

    private readonly struct Slope(int numerator, int denominator, bool exclusive = false)
    {
        public readonly int Numerator = numerator;
        public readonly int Denominator = denominator;
        private readonly bool exclusive = exclusive;

        public bool IncludesLower(int column, int depth)
        {
            long difference = (long)column * Denominator - (long)depth * Numerator;
            return difference > 0 || difference == 0 && !exclusive;
        }

        public bool IncludesUpper(int column, int depth)
        {
            long difference = (long)column * Denominator - (long)depth * Numerator;
            return difference < 0 || difference == 0 && !exclusive;
        }

        public bool HasRoomTo(Slope end)
        {
            long difference = (long)Numerator * end.Denominator - (long)end.Numerator * Denominator;
            return difference < 0 || difference == 0 && !exclusive && !end.exclusive;
        }

        public Slope Later(Slope other)
        {
            long difference =
                (long)Numerator * other.Denominator - (long)other.Numerator * Denominator;
            return difference > 0 || difference == 0 && exclusive ? this : other;
        }

        public Slope Earlier(Slope other)
        {
            long difference =
                (long)Numerator * other.Denominator - (long)other.Numerator * Denominator;
            return difference < 0 || difference == 0 && exclusive ? this : other;
        }
    }
}
