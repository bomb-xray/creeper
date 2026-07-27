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
    /// <summary>The colour treated as fully transparent.</summary>
    private static readonly Color KeyColour = new Color(255, 0, 255, 255);

    /// <summary>How far a pixel may stray from pure magenta and still be keyed out.</summary>
    private const int KeyTolerance = 12;

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

    /// <summary>Replaces every magenta pixel with a fully transparent one, in place.</summary>
    private static void ApplyColourKey(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        int keyed = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];

            if (Math.Abs(p.R - KeyColour.R) <= KeyTolerance &&
                Math.Abs(p.G - KeyColour.G) <= KeyTolerance &&
                Math.Abs(p.B - KeyColour.B) <= KeyTolerance)
            {
                // Zero the colour as well as the alpha, so no pink haloes bleed
                // out of the edges when the texture is filtered or scaled.
                pixels[i] = Color.Transparent;
                keyed++;
            }
        }

        if (keyed > 0) texture.SetData(pixels);
    }
}
