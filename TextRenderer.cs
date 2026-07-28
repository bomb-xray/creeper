using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CreeperGame;

/// <summary>
/// Single entry point for every piece of text the game draws.
///
/// If a TrueType/OpenType font is found in the assets folder it is rasterised on
/// demand with FontStashSharp; otherwise the built-in <see cref="PixelFont"/> is
/// used, so the game still runs when no font file has been supplied.
///
/// Sizes are always given in screen pixels (the intended cap/line height), which
/// keeps the call sites identical for both backends.
/// </summary>
public class TextRenderer : IDisposable
{
    /// <summary>Font file names that are preferred, in order, when several exist.</summary>
    private static readonly string[] PreferredNames = { "fenglish", "font", "main" };

    private static readonly string[] FontExtensions = { ".ttf", ".otf" };

    private readonly FontSystem? _fontSystem;
    private readonly PixelFont? _pixelFont;

    /// <summary>Cache of already requested sizes, since GetFont allocates an atlas page.</summary>
    private readonly Dictionary<int, SpriteFontBase> _sizeCache = new Dictionary<int, SpriteFontBase>();

    public bool UsingTrueType => _fontSystem != null;

    /// <summary>Name of the font file in use, for logging.</summary>
    public string SourceName { get; } = "built-in pixel font";

    public TextRenderer(GraphicsDevice device, string assetDir)
    {
        string? fontPath = FindFontFile(assetDir);

        if (fontPath != null)
        {
            try
            {
                // The sprite batch runs with BlendState.NonPremultiplied, so the
                // glyph atlas has to be rasterised the same way or the text comes
                // out with dark fringes.
                var settings = new FontSystemSettings
                {
                    GlyphRenderResult = GlyphRenderResult.NonPremultiplied,
                    TextureWidth = 1024,
                    TextureHeight = 1024
                };

                _fontSystem = new FontSystem(settings);
                _fontSystem.AddFont(File.ReadAllBytes(fontPath));
                SourceName = Path.GetFileName(fontPath);
                Console.WriteLine($"Loaded font: {fontPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load font {fontPath}: {ex.Message}");
                _fontSystem?.Dispose();
                _fontSystem = null;
            }
        }
        else
        {
            Console.WriteLine("No .ttf/.otf found in assets; using the built-in pixel font.");
        }

        // The pixel font is always built: it costs almost nothing and acts as a
        // guaranteed fallback if the TrueType path fails at runtime.
        _pixelFont = new PixelFont(device);
    }

    private static string? FindFontFile(string assetDir)
    {
        if (!Directory.Exists(assetDir)) return null;

        var candidates = FontExtensions
            .SelectMany(ext => Directory.GetFiles(assetDir, "*" + ext, SearchOption.TopDirectoryOnly))
            .Distinct()
            .ToList();

        if (candidates.Count == 0) return null;

        foreach (string preferred in PreferredNames)
        {
            string? match = candidates.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return candidates.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).First();
    }

    private SpriteFontBase? GetFont(float size)
    {
        if (_fontSystem == null) return null;

        int key = Math.Max(4, (int)MathF.Round(size));
        if (!_sizeCache.TryGetValue(key, out SpriteFontBase? font))
        {
            font = _fontSystem.GetFont(key);
            _sizeCache[key] = font;
        }
        return font;
    }

    /// <summary>Width and height in pixels that the text will occupy at the given size.</summary>
    public Vector2 Measure(string text, float size)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        SpriteFontBase? font = GetFont(size);
        if (font != null) return font.MeasureString(text);

        int pixelSize = PixelSizeFor(size);
        return new Vector2(_pixelFont!.MeasureText(text, pixelSize), _pixelFont.LineHeight(pixelSize));
    }

    /// <summary>
    /// Draws text at the given size. When <paramref name="centered"/> is true the
    /// position is the center of the text block rather than its top-left corner.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, string text, float x, float y, float size, Color color, bool centered = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        SpriteFontBase? font = GetFont(size);
        if (font != null)
        {
            Vector2 position = new Vector2(x, y);
            if (centered)
            {
                Vector2 bounds = font.MeasureString(text);
                position.X -= bounds.X / 2f;
                position.Y -= bounds.Y / 2f;
            }

            // Rounding to whole pixels avoids blurry sub-pixel glyph sampling.
            position.X = MathF.Round(position.X);
            position.Y = MathF.Round(position.Y);

            spriteBatch.DrawString(font, text, position, color);
            return;
        }

        _pixelFont!.DrawText(spriteBatch, text, (int)MathF.Round(x), (int)MathF.Round(y),
            PixelSizeFor(size), color, centered);
    }

    /// <summary>Draws text with a hard drop shadow so it stays readable over artwork.</summary>
    public void DrawShadowed(SpriteBatch spriteBatch, string text, float x, float y, float size, Color color, bool centered = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        float offset = MathF.Max(1f, MathF.Round(size / 14f));

        Color shadow = Color.Black;
        shadow.A = color.A;

        Draw(spriteBatch, text, x + offset, y + offset, size, shadow, centered);
        Draw(spriteBatch, text, x, y, size, color, centered);
    }

    /// <summary>Maps a pixel height onto the closest whole scale of the 7px pixel font.</summary>
    private static int PixelSizeFor(float size) => Math.Max(1, (int)MathF.Round(size / 7f));

    public void Dispose()
    {
        _sizeCache.Clear();
        _fontSystem?.Dispose();
        _pixelFont?.Dispose();
    }
}
