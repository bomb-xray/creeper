using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;

namespace CreeperGame;

/// <summary>
/// Loads textures from the assets folder, applying a magenta colour key.
///
/// The background art uses #FF00FF as its transparency key rather than a real
/// alpha channel, so those pixels have to be knocked out after loading or the
/// sky shows up as solid pink.
/// </summary>
public static class TextureLoader
{
    /// <summary>
    /// Pixels are keyed out when magenta dominates: red and blue both high while
    /// green stays low. A plain distance-to-#FF00FF test leaves a pink fringe,
    /// because anti-aliased edges blend the key colour into the artwork and those
    /// blends sit too far from pure magenta to be caught.
    /// </summary>
    private const int KeyHighChannel = 140;   // red and blue must exceed this
    private const int KeyLowChannel = 110;    // green must stay under this

    /// <summary>
    /// Edge pixels that are partly magenta get their contamination removed rather
    /// than being deleted, which keeps the silhouette smooth instead of jagged.
    /// </summary>
    private const int FringeHighChannel = 90;
    private const int FringeMargin = 40;

    /// <summary>
    /// Loads the first image matching <paramref name="baseName"/>, keying out
    /// magenta when <paramref name="applyColourKey"/> is set. Returns null when
    /// nothing matches.
    /// </summary>
    public static Texture2D? Load(GraphicsDevice device, string dir, string baseName, bool applyColourKey)
    {
        string? path = ImageConverter.EnsureLoadableImage(dir, baseName);
        if (path == null || !File.Exists(path)) return null;

        try
        {
            Texture2D texture;
            using (var stream = File.OpenRead(path))
            {
                texture = Texture2D.FromStream(device, stream);
            }

            if (applyColourKey) ApplyColourKey(texture);

            Console.WriteLine($"Loaded {baseName}: {path} ({texture.Width}x{texture.Height})");
            return texture;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load {baseName} from {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Tries several names in order and returns the first that loads.</summary>
    public static Texture2D? LoadAny(GraphicsDevice device, string dir, string[] baseNames, bool applyColourKey)
    {
        foreach (string name in baseNames)
        {
            Texture2D? texture = Load(device, dir, name, applyColourKey);
            if (texture != null) return texture;
        }

        Console.WriteLine($"None of these were found: {string.Join(", ", baseNames)}");
        return null;
    }

    /// <summary>
    /// Finds the region of a texture that actually contains something.
    ///
    /// The backdrop art is padded with large blocks of key colour, so measuring a
    /// layer against its full image height would scale the padding as if it were
    /// artwork and leave the real content looking far too small.
    /// </summary>
    public static Rectangle GetOpaqueBounds(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        int minX = texture.Width, minY = texture.Height, maxX = -1, maxY = -1;

        for (int y = 0; y < texture.Height; y++)
        {
            int row = y * texture.Width;
            for (int x = 0; x < texture.Width; x++)
            {
                if (pixels[row + x].A <= 8) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        // Fully transparent: fall back to the whole image.
        if (maxX < 0) return new Rectangle(0, 0, texture.Width, texture.Height);

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Knocks out the magenta key colour and cleans up the fringe it leaves.</summary>
    private static void ApplyColourKey(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        int keyed = 0;
        int cleaned = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];

            bool isKey = p.R >= KeyHighChannel && p.B >= KeyHighChannel && p.G <= KeyLowChannel;

            if (isKey)
            {
                // Clear the colour too: leaving magenta behind a zero alpha still
                // bleeds pink once the texture is filtered or scaled.
                pixels[i] = Color.Transparent;
                keyed++;
                continue;
            }

            // Partially contaminated edge: red and blue clearly above green, but
            // not enough to be the key itself. Pull the excess back down to green
            // so the pixel keeps its shape without the pink cast.
            bool isFringe = p.G < FringeHighChannel &&
                            p.R > p.G + FringeMargin &&
                            p.B > p.G + FringeMargin;

            if (isFringe)
            {
                byte level = p.G;
                pixels[i] = new Color(level, level, level, p.A);
                cleaned++;
            }
        }

        if (keyed > 0 || cleaned > 0)
        {
            texture.SetData(pixels);
            Console.WriteLine($"  colour key: {keyed} pixels cleared, {cleaned} edge pixels cleaned");
        }
    }
}
