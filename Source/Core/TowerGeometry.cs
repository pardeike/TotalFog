using System;

namespace TotalFog.Core;

/// <summary>Map-space heights are independent of the sprite's northward projection.</summary>
public static class TowerGeometry
{
    public const float GroundAimHeight = 1f;
    public const float PlatformAimHeight = 3f;
    public const float LowWallHeight = 1f;
    public const float PawnProjection = 1.65f;

    public static float Interpolate(float from, float to, float progress) =>
        from + (to - from) * Math.Max(0f, Math.Min(1f, progress));

    public static bool ClearsLowWall(float sourceHeight, float targetHeight, float progress) =>
        Interpolate(sourceHeight, targetHeight, progress) > LowWallHeight + .001f;

    public static int SightRadius(float ordinaryRange, float multiplier, int maximum) =>
        Math.Max(0, Math.Min(maximum, (int)Math.Round(ordinaryRange * multiplier)));

    public static void CopySightWindow(
        bool[] source,
        bool[] destination,
        int width,
        int minX,
        int minZ,
        int maxX,
        int maxZ,
        int towerX,
        int towerZ
    )
    {
        for (int z = minZ; z <= maxZ; z++)
            Array.Copy(source, z * width + minX, destination, z * width + minX, maxX - minX + 1);
        // Only the observer's own footprint is exempt, in its private snapshot.
        for (int z = Math.Max(minZ, towerZ - 1); z <= Math.Min(maxZ, towerZ + 1); z++)
        for (int x = Math.Max(minX, towerX - 1); x <= Math.Min(maxX, towerX + 1); x++)
            destination[z * width + x] = false;
    }
}
