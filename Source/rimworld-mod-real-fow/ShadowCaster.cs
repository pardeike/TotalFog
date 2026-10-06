using System;
using RimWorld;

namespace RimWorldRealFoW;

public class ShadowCaster
{
    private static readonly ColumnPortionQueue queue = new(64);

    public static void computeFieldOfViewWithShadowCasting(int startX, int startY, int radius, bool[] viewBlockerCells,
        int maxX, int maxY, bool handleSeenAndCache, MapComponentSeenFog mapCompSeenFog, Faction faction,
        short[] factionShownCells, bool[] fovGrid, int fovGridMinX, int fovGridMinY, int fovGridWidth,
        bool[] oldFovGrid, int oldFovGridMinX, int oldFovGridMaxX, int oldFovGridMinY, int oldFovGridMaxY,
        int oldFovGridWidth, byte specificOctant = 255, int targetX = -1, int targetY = -1)
    {
        var radiusSquared = radius * radius;
        if (specificOctant == byte.MaxValue)
        {
            for (byte b = 0; b < 8; b += 1)
            {
                computeFieldOfViewInOctantZero(b, fovGrid, fovGridMinX, fovGridMinY, fovGridWidth, oldFovGrid,
                    oldFovGridMinX, oldFovGridMaxX, oldFovGridMinY, oldFovGridMaxY, oldFovGridWidth, radius,
                    radiusSquared,
                    startX, startY, maxX, maxY, viewBlockerCells, handleSeenAndCache, mapCompSeenFog, faction,
                    factionShownCells, targetX, targetY, 0, 1, 1, 1, 0);
            }
        }
        else
        {
            computeFieldOfViewInOctantZero(specificOctant, fovGrid, fovGridMinX, fovGridMinY, fovGridWidth, oldFovGrid,
                oldFovGridMinX, oldFovGridMaxX, oldFovGridMinY, oldFovGridMaxY, oldFovGridWidth, radius, radiusSquared,
                startX,
                startY, maxX, maxY, viewBlockerCells, handleSeenAndCache, mapCompSeenFog, faction, factionShownCells,
                targetX, targetY, 0, 1, 1, 1, 0);
        }
    }

    private static void computeFieldOfViewInOctantZero(byte octant, bool[] fovGrid, int fovGridMinX, int fovGridMinY,
        int fovGridWidth, bool[] oldFovGrid, int oldFovGridMinX, int oldFovGridMaxX, int oldFovGridMinY,
        int oldFovGridMaxY, int oldFovGridWidth, int radius, int r_r, int startX, int startY, int maxX, int maxY,
        bool[] viewBlockerCells, bool handleSeenAndCache, MapComponentSeenFog mapCompSeenFog, Faction faction,
        short[] factionShownCells, int targetX, int targetY, int x, int topVectorX, int topVectorY, int bottomVectorX,
        int bottomVectorY)
    {
        var num = 0;
        var num2 = 0;
        var continueLoop = true;
        while (continueLoop || !queue.Empty())
        {
            if (!continueLoop)
            {
                ref var ptr = ref queue.Dequeue();
                x = ptr.x;
                topVectorX = ptr.topVectorX;
                topVectorY = ptr.topVectorY;
                bottomVectorX = ptr.bottomVectorX;
                bottomVectorY = ptr.bottomVectorY;
            }
            else
            {
                continueLoop = false;
            }

            while (x <= radius)
            {
                var num3 = 2 * x;
                var num4 = x * x;
                int num5;
                if (x == 0)
                {
                    num5 = 0;
                }
                else
                {
                    var num6 = (num3 + 1) * topVectorY / (2 * topVectorX);
                    var num7 = (num3 + 1) * topVectorY % (2 * topVectorX);
                    if (num7 > topVectorX)
                    {
                        num5 = num6 + 1;
                    }
                    else
                    {
                        num5 = num6;
                    }
                }

                int num8;
                if (x == 0)
                {
                    num8 = 0;
                }
                else
                {
                    var num6 = (num3 - 1) * bottomVectorY / (2 * bottomVectorX);
                    var num7 = (num3 - 1) * bottomVectorY % (2 * bottomVectorX);
                    if (num7 >= bottomVectorX)
                    {
                        num8 = num6 + 1;
                    }
                    else
                    {
                        num8 = num6;
                    }
                }

                var firstCheck = false;
                var secondCheck = false;
                switch (octant)
                {
                    case 1:
                    case 2:
                        num = startY + x;
                        break;
                    case 3:
                    case 4:
                        num2 = startX - x;
                        break;
                    case 5:
                    case 6:
                        num = startY - x;
                        break;
                    default:
                        num2 = startX + x;
                        break;
                }

                for (var i = num5; i >= num8; i--)
                {
                    switch (octant)
                    {
                        case 1:
                        case 6:
                            num2 = startX + i;
                            break;
                        case 2:
                        case 5:
                            num2 = startX - i;
                            break;
                        case 4:
                        case 7:
                            num = startY - i;
                            break;
                        default:
                            num = startY + i;
                            break;
                    }

                    var num9 = (num * maxX) + num2;
                    if (num4 + (i * i) < r_r && num2 >= 0 && num >= 0 && num2 < maxX && num < maxY)
                    {
                        if (targetX == -1)
                        {
                            var num10 = ((num - fovGridMinY) * fovGridWidth) + (num2 - fovGridMinX);
                            if (!fovGrid[num10])
                            {
                                fovGrid[num10] = true;
                                if (handleSeenAndCache)
                                {
                                    if (oldFovGrid == null || num2 < oldFovGridMinX || num < oldFovGridMinY ||
                                        num2 > oldFovGridMaxX || num > oldFovGridMaxY)
                                    {
                                        mapCompSeenFog.IncrementSeen(faction, factionShownCells, num9);
                                    }
                                    else
                                    {
                                        var num11 = ((num - oldFovGridMinY) * oldFovGridWidth) +
                                                    (num2 - oldFovGridMinX);
                                        if (!oldFovGrid[num11])
                                        {
                                            mapCompSeenFog.IncrementSeen(faction, factionShownCells, num9);
                                        }
                                        else
                                        {
                                            oldFovGrid[num11] = false;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (targetX == num2 && targetY == num)
                            {
                                fovGrid[0] = true;
                                return;
                            }
                        }
                    }

                    if (secondCheck)
                    {
                        if (num4 + (i * i) >= r_r || num2 < 0 || num < 0 || num2 >= maxX || num >= maxY ||
                            viewBlockerCells[num9])
                        {
                            if (!firstCheck)
                            {
                                ref var ptr2 = ref queue.Enqueue();
                                ptr2.x = x + 1;
                                ptr2.topVectorX = topVectorX;
                                ptr2.topVectorY = topVectorY;
                                ptr2.bottomVectorX = num3 - 1;
                                ptr2.bottomVectorY = (2 * i) + 1;
                            }
                        }
                        else
                        {
                            if (firstCheck)
                            {
                                topVectorX = num3 + 1;
                                topVectorY = (2 * i) + 1;
                            }
                        }
                    }

                    secondCheck = true;
                    firstCheck = num4 + (i * i) >= r_r || num2 < 0 || num < 0 || num2 >= maxX || num >= maxY ||
                                 viewBlockerCells[num9];
                }

                if (!(secondCheck && !firstCheck))
                {
                    break;
                }

                x++;
            }
        }
    }

    private sealed class ColumnPortionQueue(int size)
    {
        private int currentPos;

        private int nextInsertPos;

        private ColumnPortion[] nodes = new ColumnPortion[size];

        public ref ColumnPortion Enqueue()
        {
            var num = nextInsertPos;
            nextInsertPos = num + 1;
            if (nextInsertPos >= nodes.Length)
            {
                nextInsertPos = 0;
            }

            if (nextInsertPos != currentPos)
            {
                return ref nodes[num];
            }

            var array = new ColumnPortion[nodes.Length * 2];
            if (nextInsertPos == 0)
            {
                nextInsertPos = nodes.Length;
                Array.Copy(nodes, array, nodes.Length);
            }
            else
            {
                Array.Copy(nodes, 0, array, 0, nextInsertPos);
                Array.Copy(nodes, currentPos, array, array.Length - (nodes.Length - currentPos),
                    nodes.Length - currentPos);
                currentPos = array.Length - (nodes.Length - currentPos);
            }

            nodes = array;

            return ref nodes[num];
        }

        public ref ColumnPortion Dequeue()
        {
            var num = currentPos;
            currentPos = num + 1;
            if (currentPos >= nodes.Length)
            {
                currentPos = 0;
            }

            return ref nodes[num];
        }

        public bool Empty()
        {
            return currentPos == nextInsertPos;
        }
    }

    private struct ColumnPortion
    {
        public int x;

        public int topVectorX;

        public int topVectorY;

        public int bottomVectorX;

        public int bottomVectorY;
    }
}