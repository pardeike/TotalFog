using System;
namespace TotalFog.Core;

public static class FogAppearance
{
    public static byte CellAlpha(bool vanillaFog, bool known, int viewers, byte rememberedAlpha)
        => vanillaFog || !known ? byte.MaxValue : viewers > 0 ? (byte)0 : rememberedAlpha;

    // Neighborhood is row-major, west to east then south to north. A shared
    // vertex uses the darkest touching cell, so unknown terrain cannot leak.
    public static void FillVertices(byte[] neighborhood, byte[] vertices)
    {
        if (neighborhood.Length != 9 || vertices.Length != 9) throw new ArgumentException("Fog cells have nine samples and vertices.");
        FillVertices(neighborhood, 4, 3, vertices, 0);
    }
    /// <summary>Expand a section's once-sampled cells into the native nine-vertex topology.</summary>
    public static void FillSection(byte[] samples, int width, int height, byte[] vertices)
    {
        if (width <= 0 || height <= 0 || samples.Length < checked((width + 2) * (height + 2)) ||
            vertices.Length != checked(width * height * 9)) throw new ArgumentException("Fog section dimensions differ.");
        int stride = width + 2, vertex = 0;
        // Native geometry is column-major; samples are row-major with a one-cell border.
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++, vertex += 9)
            FillVertices(samples, (z + 1) * stride + x + 1, stride, vertices, vertex);
    }
    private static void FillVertices(byte[] samples, int center, int stride, byte[] vertices, int vertex)
    {
        byte a = samples[center - stride - 1], b = samples[center - stride], c = samples[center - stride + 1],
            d = samples[center - 1], e = samples[center], f = samples[center + 1],
            g = samples[center + stride - 1], h = samples[center + stride], i = samples[center + stride + 1];
        vertices[vertex] = Max(a, b, d, e);
        vertices[vertex + 1] = Math.Max(d, e);
        vertices[vertex + 2] = Max(d, e, g, h);
        vertices[vertex + 3] = Math.Max(e, h);
        vertices[vertex + 4] = Max(e, f, h, i);
        vertices[vertex + 5] = Math.Max(e, f);
        vertices[vertex + 6] = Max(b, c, e, f);
        vertices[vertex + 7] = Math.Max(b, e);
        vertices[vertex + 8] = e;
    }
    private static byte Max(byte a, byte b, byte c, byte d) => Math.Max(Math.Max(a, b), Math.Max(c, d));

    public static byte Advance(byte current, byte target, int elapsedTicks, int speed)
    {
        if (elapsedTicks <= 0 || current == target) return current;
        long amount = (long)elapsedTicks * Math.Max(1, speed);
        return current < target ? (byte)Math.Min(target, current + amount) : (byte)Math.Max(target, current - amount);
    }
}
