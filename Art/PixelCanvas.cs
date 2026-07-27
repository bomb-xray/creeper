using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CreeperGame.Art;

/// <summary>
/// A palette: a short, deliberately chosen list of colours.
///
/// The previous renderer shaded by distance-from-edge, which rounds every shape
/// into a balloon and is exactly why the results looked soft and toy-like. Games
/// in this style do the opposite: broad flat areas of a single tone, with light
/// and shadow placed by hand as distinct regions and hard boundaries between
/// them. So this canvas has no automatic shading at all -- every tone is chosen
/// explicitly by the code that draws the shape.
/// </summary>
public sealed class Palette
{
    private readonly Color[] _colours;

    public Palette(params (int r, int g, int b)[] colours)
    {
        _colours = new Color[colours.Length];
        for (int i = 0; i < colours.Length; i++)
        {
            _colours[i] = new Color(colours[i].r, colours[i].g, colours[i].b);
        }
    }

    public Color this[int index] => _colours[Math.Clamp(index, 0, _colours.Length - 1)];

    public int Count => _colours.Length;
}

/// <summary>
/// A direct pixel buffer with drawing primitives that paint an explicit colour.
///
/// Everything here works in whole pixels. There is no anti-aliasing and no
/// interpolation anywhere: a pixel is either set to a palette colour or left
/// alone. That constraint is what keeps the output looking hand-placed rather
/// than like a downscaled vector render.
/// </summary>
public sealed class PixelCanvas
{
    public int Width { get; }
    public int Height { get; }

    private readonly Color[] _pixels;

    /// <summary>Tracks which pixels have been written, for outlining.</summary>
    private readonly bool[] _filled;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Color[width * height];
        _filled = new bool[width * height];
    }

    public bool IsFilled(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height && _filled[y * Width + x];

    public Color At(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height ? _pixels[y * Width + x] : Color.Transparent;

    public void Plot(int x, int y, Color colour)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = y * Width + x;
        _pixels[i] = colour;
        _filled[i] = true;
    }

    /// <summary>Writes only where nothing has been written yet.</summary>
    public void PlotBehind(int x, int y, Color colour)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = y * Width + x;
        if (_filled[i]) return;
        _pixels[i] = colour;
        _filled[i] = true;
    }

    public void Erase(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = y * Width + x;
        _pixels[i] = Color.Transparent;
        _filled[i] = false;
    }

    // ---- primitives --------------------------------------------------------

    public void Rect(int x, int y, int w, int h, Color colour)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++) Plot(xx, yy, colour);
        }
    }

    /// <summary>Horizontal run. The fundamental unit when drawing by scanline.</summary>
    public void HLine(int x1, int x2, int y, Color colour)
    {
        if (x1 > x2) (x1, x2) = (x2, x1);
        for (int x = x1; x <= x2; x++) Plot(x, y, colour);
    }

    public void VLine(int x, int y1, int y2, Color colour)
    {
        if (y1 > y2) (y1, y2) = (y2, y1);
        for (int y = y1; y <= y2; y++) Plot(x, y, colour);
    }

    /// <summary>Bresenham line, so diagonals step cleanly instead of blurring.</summary>
    public void Line(int x1, int y1, int x2, int y2, Color colour)
    {
        int dx = Math.Abs(x2 - x1), sx = x1 < x2 ? 1 : -1;
        int dy = -Math.Abs(y2 - y1), sy = y1 < y2 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            Plot(x1, y1, colour);
            if (x1 == x2 && y1 == y2) break;

            int e2 = err * 2;
            if (e2 >= dy) { err += dy; x1 += sx; }
            if (e2 <= dx) { err += dx; y1 += sy; }
        }
    }

    /// <summary>
    /// A tapered limb drawn as a stack of horizontal runs, which keeps the edges
    /// as clean vertical steps rather than the ragged diagonal a circle-stamped
    /// capsule produces.
    /// </summary>
    public void Limb(int x1, int y1, int x2, int y2, int w1, int w2, Color colour)
    {
        int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        if (steps == 0) steps = 1;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int cx = (int)MathF.Round(x1 + (x2 - x1) * t);
            int cy = (int)MathF.Round(y1 + (y2 - y1) * t);
            int w = (int)MathF.Round(MathHelper.Lerp(w1, w2, t));
            if (w < 1) w = 1;

            HLine(cx - w / 2, cx - w / 2 + w - 1, cy, colour);
        }
    }

    /// <summary>Scanline polygon fill.</summary>
    public void Polygon(Color colour, params Point[] points)
    {
        if (points.Length < 3) return;

        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (Point p in points)
        {
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var crossings = new List<float>();

        for (int y = minY; y <= maxY; y++)
        {
            crossings.Clear();

            for (int i = 0; i < points.Length; i++)
            {
                Point a = points[i];
                Point b = points[(i + 1) % points.Length];

                if (a.Y == b.Y) continue;
                if (y < Math.Min(a.Y, b.Y) || y >= Math.Max(a.Y, b.Y)) continue;

                crossings.Add(a.X + (y - a.Y) * (b.X - a.X) / (float)(b.Y - a.Y));
            }

            crossings.Sort();

            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                HLine((int)MathF.Round(crossings[i]),
                      (int)MathF.Round(crossings[i + 1]), y, colour);
            }
        }
    }

    /// <summary>
    /// Filled ellipse drawn scanline by scanline. Used sparingly: rounded forms
    /// are the thing that makes pixel art look soft, so most shapes here are
    /// polygons instead.
    /// </summary>
    public void Ellipse(int cx, int cy, int rx, int ry, Color colour)
    {
        if (rx < 1) rx = 1;
        if (ry < 1) ry = 1;

        for (int y = -ry; y <= ry; y++)
        {
            // Half-width of the ellipse at this row.
            float t = 1f - (y * y) / (float)(ry * ry);
            if (t < 0) continue;

            int half = (int)MathF.Round(rx * MathF.Sqrt(t));
            HLine(cx - half, cx + half, cy + y, colour);
        }
    }

    /// <summary>
    /// Traces a hard outline around everything drawn so far.
    ///
    /// A dark contour is what separates the figure from the background and is
    /// present on essentially every sprite in this genre.
    /// </summary>
    public void Outline(Color colour)
    {
        var edge = new List<int>();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_filled[y * Width + x]) continue;

                bool touching =
                    IsFilled(x - 1, y) || IsFilled(x + 1, y) ||
                    IsFilled(x, y - 1) || IsFilled(x, y + 1);

                if (touching) edge.Add(y * Width + x);
            }
        }

        foreach (int i in edge)
        {
            _pixels[i] = colour;
            _filled[i] = true;
        }
    }

    public Color[] ToArray() => (Color[])_pixels.Clone();

    /// <summary>Bounding box of everything drawn, or an empty rect if nothing was.</summary>
    public Rectangle UsedBounds()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!_filled[y * Width + x]) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0) return Rectangle.Empty;
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
