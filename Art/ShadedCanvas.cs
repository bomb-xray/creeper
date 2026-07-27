using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CreeperGame.Art;

/// <summary>
/// A material defines the colour ramp a shape is shaded with. Ramps run from
/// darkest to brightest; the shading pass picks an index per pixel.
///
/// Eight steps is the sweet spot for this style: enough to describe a rounded
/// metal form, few enough that the result still reads as deliberate pixel art
/// rather than a downscaled render.
/// </summary>
public sealed class Material
{
    public string Name { get; }
    public Color[] Ramp { get; }

    public Material(string name, params (int r, int g, int b)[] ramp)
    {
        Name = name;
        Ramp = new Color[ramp.Length];
        for (int i = 0; i < ramp.Length; i++)
        {
            Ramp[i] = new Color(ramp[i].r, ramp[i].g, ramp[i].b);
        }
    }

    public Color Sample(float level)
    {
        int index = (int)MathF.Round(level);
        if (index < 0) index = 0;
        if (index >= Ramp.Length) index = Ramp.Length - 1;
        return Ramp[index];
    }
}

/// <summary>
/// One drawable piece of the figure: a coverage mask plus the material it is
/// made from, plus optional per-pixel shade bias for details like panel lines.
///
/// Kept separate from the canvas so parts can be sorted by depth and shaded
/// independently, which is what lets a pauldron read as sitting in front of an
/// arm rather than merging into it.
/// </summary>
public sealed class Shape
{
    public Material Material { get; }

    /// <summary>Draw order. Lower numbers are further back.</summary>
    public int Depth { get; }

    public readonly bool[,] Mask;

    /// <summary>Shade offsets applied after lighting, for engraved detail.</summary>
    public readonly float[,] Bias;

    public readonly int Width;
    public readonly int Height;

    public Shape(Material material, int depth, int width, int height)
    {
        Material = material;
        Depth = depth;
        Width = width;
        Height = height;
        Mask = new bool[height, width];
        Bias = new float[height, width];
    }

    private bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public void Plot(int x, int y)
    {
        if (InBounds(x, y)) Mask[y, x] = true;
    }

    public void Clear(int x, int y)
    {
        if (InBounds(x, y)) Mask[y, x] = false;
    }

    /// <summary>Filled ellipse. The building block for joints and rounded plate.</summary>
    public void Ellipse(float cx, float cy, float rx, float ry)
    {
        if (rx < 0.5f) rx = 0.5f;
        if (ry < 0.5f) ry = 0.5f;

        int x0 = (int)MathF.Floor(cx - rx), x1 = (int)MathF.Ceiling(cx + rx);
        int y0 = (int)MathF.Floor(cy - ry), y1 = (int)MathF.Ceiling(cy + ry);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1f) Plot(x, y);
            }
        }
    }

    public void Circle(float cx, float cy, float r) => Ellipse(cx, cy, r, r);

    /// <summary>
    /// A tapered capsule between two points: the workhorse for limbs, wing bones
    /// and blades. Stepping at half-pixel intervals avoids gaps on steep angles.
    /// </summary>
    public void Limb(float x1, float y1, float x2, float y2, float w1, float w2)
    {
        float length = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        int steps = Math.Max(2, (int)(length * 2f));

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float cx = x1 + (x2 - x1) * t;
            float cy = y1 + (y2 - y1) * t;
            float r = MathHelper.Lerp(w1, w2, t) * 0.5f;
            Circle(cx, cy, r);
        }
    }

    public void Limb(float x1, float y1, float x2, float y2, float w) =>
        Limb(x1, y1, x2, y2, w, w);

    /// <summary>Scanline polygon fill, for plate that is not a simple capsule.</summary>
    public void Polygon(params Vector2[] points)
    {
        if (points.Length < 3) return;

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 p in points)
        {
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var crossings = new List<float>();

        for (int y = (int)MathF.Floor(minY); y <= (int)MathF.Ceiling(maxY); y++)
        {
            crossings.Clear();

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];

                if (Math.Abs(a.Y - b.Y) < 0.0001f) continue;
                if (y < MathF.Min(a.Y, b.Y) || y >= MathF.Max(a.Y, b.Y)) continue;

                crossings.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }

            crossings.Sort();

            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                int sx = (int)MathF.Round(crossings[i]);
                int ex = (int)MathF.Round(crossings[i + 1]);
                for (int x = sx; x <= ex; x++) Plot(x, y);
            }
        }
    }

    public void Rect(float x, float y, float w, float h)
    {
        for (int yy = (int)y; yy < (int)(y + h); yy++)
        {
            for (int xx = (int)x; xx < (int)(x + w); xx++) Plot(xx, yy);
        }
    }

    /// <summary>Cuts a block out of the mask, used for visor slits and gaps.</summary>
    public void Carve(float x, float y, float w, float h)
    {
        for (int yy = (int)y; yy < (int)(y + h); yy++)
        {
            for (int xx = (int)x; xx < (int)(x + w); xx++) Clear(xx, yy);
        }
    }

    /// <summary>Biases a region darker or lighter after the lighting pass.</summary>
    public void Shade(float x, float y, float w, float h, float amount)
    {
        for (int yy = (int)y; yy < (int)(y + h); yy++)
        {
            for (int xx = (int)x; xx < (int)(x + w); xx++)
            {
                if (InBounds(xx, yy)) Bias[yy, xx] += amount;
            }
        }
    }

    /// <summary>Biases along a line, for edge highlights on plate.</summary>
    public void ShadeLine(float x1, float y1, float x2, float y2, float width, float amount)
    {
        float length = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        int steps = Math.Max(2, (int)(length * 2f));

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float cx = x1 + (x2 - x1) * t;
            float cy = y1 + (y2 - y1) * t;
            Shade(cx - width * 0.5f, cy - width * 0.5f, width, width, amount);
        }
    }
}

/// <summary>
/// Rasterises shapes into a shaded pixel image.
///
/// The lighting model is deliberately simple but consistent: a chamfer distance
/// transform measures how deep each pixel sits inside its shape, the gradient of
/// that depth stands in for a surface normal, and the normal is lit from a fixed
/// direction. Quantising the result onto a short ramp is what produces clean
/// pixel-art banding instead of a muddy gradient.
/// </summary>
public sealed class ShadedCanvas
{
    public int Width { get; }
    public int Height { get; }

    private readonly List<Shape> _shapes = new List<Shape>();

    /// <summary>Light direction, normalised. Upper left is the usual convention.</summary>
    private static readonly Vector2 Light = Vector2.Normalize(new Vector2(-0.55f, -0.83f));

    private static readonly Color Outline = new Color(8, 8, 12);

    public ShadedCanvas(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public Shape AddShape(Material material, int depth)
    {
        var shape = new Shape(material, depth, Width, Height);
        _shapes.Add(shape);
        return shape;
    }

    /// <summary>
    /// Two-pass chamfer distance transform. Cheap, and accurate enough that the
    /// gradient reads as a believable surface normal.
    /// </summary>
    private int[,] DistanceField(bool[,] mask)
    {
        const int Far = 9999;
        var d = new int[Height, Width];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) d[y, x] = mask[y, x] ? Far : 0;
        }

        // Forward pass: top-left to bottom-right.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (d[y, x] == 0) continue;

                int best = d[y, x];
                if (y > 0)
                {
                    best = Math.Min(best, d[y - 1, x] + 2);
                    if (x > 0) best = Math.Min(best, d[y - 1, x - 1] + 3);
                    if (x < Width - 1) best = Math.Min(best, d[y - 1, x + 1] + 3);
                }
                if (x > 0) best = Math.Min(best, d[y, x - 1] + 2);
                d[y, x] = best;
            }
        }

        // Backward pass: bottom-right to top-left.
        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = Width - 1; x >= 0; x--)
            {
                if (d[y, x] == 0) continue;

                int best = d[y, x];
                if (y < Height - 1)
                {
                    best = Math.Min(best, d[y + 1, x] + 2);
                    if (x > 0) best = Math.Min(best, d[y + 1, x - 1] + 3);
                    if (x < Width - 1) best = Math.Min(best, d[y + 1, x + 1] + 3);
                }
                if (x < Width - 1) best = Math.Min(best, d[y, x + 1] + 2);
                d[y, x] = best;
            }
        }

        return d;
    }

    /// <summary>Shades every shape back to front and traces the outline.</summary>
    public Color[] Render()
    {
        var pixels = new Color[Width * Height];
        var covered = new bool[Height, Width];

        _shapes.Sort((a, b) => a.Depth.CompareTo(b.Depth));

        foreach (Shape shape in _shapes)
        {
            int[,] depth = DistanceField(shape.Mask);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!shape.Mask[y, x]) continue;

                    // Depth gradient stands in for the surface normal: on a
                    // rounded form depth climbs fastest towards the interior.
                    float gx = (depth[y, Math.Min(Width - 1, x + 1)] -
                                depth[y, Math.Max(0, x - 1)]) * 0.5f;
                    float gy = (depth[Math.Min(Height - 1, y + 1), x] -
                                depth[Math.Max(0, y - 1), x]) * 0.5f;

                    float lit = 0f;
                    float len = MathF.Sqrt(gx * gx + gy * gy);
                    if (len > 0.01f)
                    {
                        lit = (gx / len) * Light.X + (gy / len) * Light.Y;
                    }

                    // Interior pixels sit mid-ramp; the directional term pushes
                    // lit edges up and shadowed edges down.
                    float core = MathF.Min(depth[y, x] * 0.5f, 6f);
                    float level = 2.6f + core * 0.32f + lit * 2.5f + shape.Bias[y, x];

                    pixels[y * Width + x] = shape.Material.Sample(level);
                    covered[y, x] = true;
                }
            }
        }

        TraceOutline(pixels, covered);
        return pixels;
    }

    /// <summary>Darkens every empty pixel that touches the silhouette.</summary>
    private void TraceOutline(Color[] pixels, bool[,] covered)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (covered[y, x]) continue;

                bool adjacent = false;
                for (int dy = -1; dy <= 1 && !adjacent; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                        if (covered[ny, nx]) { adjacent = true; break; }
                    }
                }

                if (adjacent) pixels[y * Width + x] = Outline;
            }
        }
    }
}
