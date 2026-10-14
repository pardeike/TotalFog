using System;

namespace TotalFog.Core;

/// <summary>Map-space heights are independent of the sprite's northward projection.</summary>
public static class TowerGeometry
{
    public const float GroundAimHeight = 1f;
    public const float PlatformAimHeight = 3f;
    public const float LowWallHeight = 1f;
    public const float PawnProjection = 1.65f;

    // Six seconds per direction at normal speed. Derive the phase from the
    // existing job clock so save/load and Multiplayer need no extra state.
    public static int WatchFacing(int elapsedTicks, bool drafted) =>
        drafted ? 2 : (2 + Math.Max(0, elapsedTicks) / 360) % 4;

    public static float Interpolate(float from, float to, float progress) =>
        from + (to - from) * Math.Max(0f, Math.Min(1f, progress));

    public static float PlatformLift(float elevation) =>
        Interpolate(0f, PlatformAimHeight - GroundAimHeight, elevation);

    public static bool ClearsLowWall(float sourceHeight, float targetHeight, float progress) =>
        Interpolate(sourceHeight, targetHeight, progress) > LowWallHeight + .001f;

    public static int SightRadius(float ordinaryRange, float multiplier, int maximum) =>
        Math.Max(0, Math.Min(maximum, (int)Math.Round(ordinaryRange * multiplier)));

    // Includes all ladder rotations, raised railings and an ordinary lookout.
    // Coordinates are relative to the logical cell, not its shifted center.
    public static bool FogDrawingContains(float x, float z, int towerX, int towerZ) =>
        x >= towerX - 1.5f && x <= towerX + 2.5f && z >= towerZ - 1.5f && z <= towerZ + 3.5f;

    public static float RememberedBrightness(byte alpha) => 1f - alpha / 255f;

    /// <summary>Shade only the solid 3x3 base, without changing discovery or sight.</summary>
    public static void FillFogFootprint(
        byte[] samples,
        int width,
        int height,
        int minX,
        int minZ,
        int towerX,
        int towerZ,
        int mapWidth,
        int mapHeight,
        byte alpha
    )
    {
        // Border samples repeat the nearest map cell, just like native fog.
        int left = towerX <= 1 ? minX : Math.Max(minX, towerX - 1);
        int right =
            towerX >= mapWidth - 2 ? minX + width - 1 : Math.Min(minX + width - 1, towerX + 1);
        int bottom = towerZ <= 1 ? minZ : Math.Max(minZ, towerZ - 1);
        int top =
            towerZ >= mapHeight - 2 ? minZ + height - 1 : Math.Min(minZ + height - 1, towerZ + 1);
        for (int z = bottom; z <= top; z++)
        for (int x = left; x <= right; x++)
            samples[(z - minZ) * width + x - minX] = alpha;
    }

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
