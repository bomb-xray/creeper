using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreeperGame;

/// <summary>
/// A hand-authored pixel bitmap, built at runtime from character-art strings.
///
/// Rotating or smoothly scaling pixel art destroys it, so sprites here are only
/// ever drawn at whole-number scales on integer pixel positions. Animation comes
/// from swapping frames and shifting parts by whole pixels, which is how the
/// look is kept honest.
/// </summary>
public class PixelSprite : IDisposable
{
    public Texture2D Texture { get; }
    public int Width { get; }
    public int Height { get; }

    public PixelSprite(GraphicsDevice device, string[] rows, IReadOnlyDictionary<char, Color> palette)
    {
        Height = rows.Length;
        Width = rows.Max(r => r.Length);

        var pixels = new Color[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                char c = row[x];
                if (c == '.' || c == ' ') continue;   // transparent

                if (palette.TryGetValue(c, out Color colour))
                {
                    pixels[y * Width + x] = colour;
                }
                else
                {
                    // Loud magenta makes a typo in the art data obvious.
                    pixels[y * Width + x] = new Color(255, 0, 255);
                }
            }
        }

        Texture = new Texture2D(device, Width, Height);
        Texture.SetData(pixels);
    }

    /// <summary>
    /// Draws the sprite with its top-left at the given screen pixel, scaled by a
    /// whole number. <paramref name="flip"/> mirrors it horizontally.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, int screenX, int screenY, int scale,
        bool flip, Color tint)
    {
        spriteBatch.Draw(Texture,
            new Rectangle(screenX, screenY, Width * scale, Height * scale),
            null, tint, 0f, Vector2.Zero,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    public void Dispose() => Texture?.Dispose();
}

/// <summary>
/// The pixel artwork for the winged knight, transcribed from the concept art:
/// steel plate, a red helmet plume, a long blade held upright, a trailing red
/// cape and a large pale wing.
///
/// Every part is authored facing right and mirrored when the knight turns left.
/// Parts are separate so they can be posed independently.
/// </summary>
public static class KnightArt
{
    public static readonly Dictionary<char, Color> Palette = new()
    {
        ['k'] = new Color(16, 15, 20),      // outline
        ['d'] = new Color(58, 62, 74),      // deep steel shadow
        ['s'] = new Color(104, 110, 126),   // steel
        ['l'] = new Color(158, 166, 184),   // lit steel
        ['w'] = new Color(214, 220, 232),   // steel highlight
        ['r'] = new Color(72, 14, 20),      // cape shadow
        ['R'] = new Color(122, 24, 32),     // cape
        ['B'] = new Color(164, 38, 44),     // cape highlight
        ['F'] = new Color(232, 235, 242),   // feather lit
        ['f'] = new Color(184, 190, 204),   // feather mid
        ['g'] = new Color(132, 139, 156),   // feather shadow
        ['b'] = new Color(32, 31, 38),      // boots and leather
        ['h'] = new Color(88, 40, 44),      // dark red leather
    };

    // ---- head ------------------------------------------------------------
    // Closed helm with a brow ridge and a dark visor slit.

    public static readonly string[] Head =
    {
        "...kkkkk..",
        "..kddddsk.",
        ".kdsslllsk",
        ".kdslwwlsk",
        "kdsllwwlsk",
        "kdskkkklsk",   // visor slit
        "kdsllllsdk",
        "kdsllllsdk",
        ".kdssssdk.",
        "..kddddk..",
        "...kkkk...",
    };

    /// <summary>The red plume, angled back over the helm.</summary>
    public static readonly string[] Plume =
    {
        ".....kBk",
        "....kBRk",
        "...kBRrk",
        "..kBRrk.",
        ".kBRrk..",
        "kBRrk...",
        "kRrk....",
        ".kk.....",
    };

    // ---- torso -----------------------------------------------------------
    // Breastplate, pauldrons and a red waist sash.

    public static readonly string[] Torso =
    {
        "..kkkkkkk...",
        ".kdsssssdk..",
        "kdsllllllsk.",
        "kdslwwwwlsk.",
        "kdslwwwwlsk.",
        "kdsllwwllsk.",
        "kdsllllllsk.",
        ".kdsllllsdk.",
        ".kdsllllsdk.",
        "..kdssssdk..",
        "..kRRBBRRk..",   // sash
        "..krRBBRrk..",
        "...kdssdk...",
    };

    // ---- arms ------------------------------------------------------------
    // Authored as one piece: pauldron, upper arm, gauntlet.

    public static readonly string[] ArmNear =
    {
        ".kkkk.",
        "kdsslk",
        "kdsllk",
        "kdsslk",
        ".kdslk",
        ".kdslk",
        ".kdslk",
        ".kwwlk",   // gauntlet
        ".kwlsk",
        "..kkk.",
    };

    public static readonly string[] ArmFar =
    {
        ".kkkk.",
        "kddssk",
        "kddssk",
        "kddssk",
        ".kdssk",
        ".kdssk",
        ".kdssk",
        ".kdlsk",
        ".kdssk",
        "..kkk.",
    };

    // ---- sword -----------------------------------------------------------
    // Long straight blade with a crossguard, held point-up.

    public static readonly string[] Sword =
    {
        "..k..",
        ".klk.",
        ".klk.",
        ".klk.",
        ".kwk.",
        ".klk.",
        ".klk.",
        ".kwk.",
        ".klk.",
        ".klk.",
        ".kwk.",
        ".klk.",
        ".klk.",
        ".kwk.",
        ".klk.",
        ".klk.",
        ".kwk.",
        ".klk.",
        "kkkkk",   // crossguard
        "khRhk",
        "kkkkk",
        ".khk.",   // grip
        ".khk.",
        ".khk.",
        ".klk.",   // pommel
        ".kkk.",
    };

    // ---- legs ------------------------------------------------------------
    // Full leg pairs, one bitmap per pose. Authoring the pair together is the
    // only reliable way to keep the silhouette right in pixel art.

    public static readonly string[] LegsIdle =
    {
        "..kddk..kddk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kdsk..kdsk..",   // knee
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kbbk..kbbk..",
        ".kbbbk.kbbbk..",
        ".kkkkk.kkkkk..",
    };

    public static readonly string[] LegsWalk0 =
    {
        "...kddk.kddk..",
        "..kslk...kslk.",
        "..kslk...kslk.",
        ".kslk.....kslk",
        ".kdsk.....kdsk",
        ".kslk.....kslk",
        "kslk.......kbk",
        "kslk.......kbk",
        "kbbk......kbbk",
        "kbbbk....kbbbk",
        "kkkkk....kkkkk",
    };

    public static readonly string[] LegsWalk1 =
    {
        "..kddk..kddk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kdsk..kdsk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kslk..kslk..",
        "..kbbk..kbbk..",
        ".kbbbk.kbbbk..",
        ".kkkkk.kkkkk..",
    };

    public static readonly string[] LegsWalk2 =
    {
        "..kddk.kddk...",
        ".kslk...kslk..",
        ".kslk...kslk..",
        "kslk.....kslk.",
        "kdsk.....kdsk.",
        "kslk.....kslk.",
        "kbk.......kslk",
        "kbk.......kslk",
        "kbbk.....kbbk.",
        "kbbbk...kbbbk.",
        "kkkkk...kkkkk.",
    };

    public static readonly string[] LegsCrouch =
    {
        "..............",
        "..............",
        "..............",
        "..kddk..kddk..",
        ".kslsk.kslsk..",
        ".kslsk.kslsk..",
        "kdslk...kdslk.",
        "kslk.....kslk.",
        "kbbbk...kbbbk.",
        "kbbbbk.kbbbbk.",
        "kkkkkk.kkkkkk.",
    };

    public static readonly string[] LegsJump =
    {
        "...kddk.kddk..",
        "..kslk...kslk.",
        "..kslk...kslk.",
        "..kdsk....kslk",
        "..kslk....kdsk",
        ".kslk......kbk",
        ".kbbk......kbk",
        "kbbbk.....kbbk",
        "kkkkk....kbbbk",
        ".........kkkkk",
        "..............",
    };

    public static readonly string[] LegsFall =
    {
        "..kddk..kddk..",
        "..kslk..kslk..",
        ".kslk....kslk.",
        ".kdsk....kdsk.",
        ".kslk....kslk.",
        "kslk......kslk",
        "kslk......kslk",
        "kbbk......kbbk",
        "kbbbk....kbbbk",
        "kkkkk....kkkkk",
        "..............",
    };

    // ---- cape ------------------------------------------------------------
    // Three sway states: at rest, drifting, and streaming out behind.

    public static readonly string[] CapeRest =
    {
        "..kkkkkk..",
        ".kRRBBRRk.",
        ".kRRBBRRk.",
        "kRRRBBRRRk",
        "kRRRBBRRRk",
        "kRrRBBRrRk",
        "kRrRBBRrRk",
        "kRrrRBRrRk",
        "kRrrRBRrRk",
        "kRrrRBRrRk",
        ".krrRBRrk.",
        ".krrRBRrk.",
        ".krrrBrrk.",
        "..krrBrk..",
        "..krrrrk..",
        "...kkkk...",
    };

    public static readonly string[] CapeDrift =
    {
        "...kkkkkk.",
        "..kRRBBRRk",
        "..kRRBBRRk",
        ".kRRRBBRRk",
        ".kRRRBBRRk",
        ".kRrRBBRrk",
        "kRrRBBRrk.",
        "kRrrRBRrk.",
        "kRrrRBRrk.",
        "krrrRBRrk.",
        "krrRBRrk..",
        "krrRBRrk..",
        "krrrBrk...",
        "krrrrk....",
        "kkrrk.....",
        ".kkk......",
    };

    public static readonly string[] CapeStream =
    {
        ".....kkkkk",
        "....kRRBBR",
        "...kRRBBRk",
        "..kRRRBBRk",
        ".kRRRBBRk.",
        ".kRrRBBRk.",
        "kRrRBBRk..",
        "kRrrRBRk..",
        "krrrRBRk..",
        "krrRBRk...",
        "krrRBk....",
        "krrRk.....",
        "krrk......",
        "krk.......",
        "kk........",
        "..........",
    };

    // ---- wing ------------------------------------------------------------
    // Folded, half open and spread. Authored pointing back and up.

    public static readonly string[] WingFolded =
    {
        "......kkk.",
        ".....kFFfk",
        "....kFFffk",
        "...kFFfffk",
        "...kFffffk",
        "..kFfffggk",
        "..kFffgggk",
        ".kFffggggk",
        ".kFfggggkk",
        "kFfgggggk.",
        "kfggggggk.",
        "kfgggggk..",
        ".kggggk...",
        ".kgggk....",
        "..kggk....",
        "..kkk.....",
    };

    public static readonly string[] WingOpen =
    {
        "........kkkk...",
        "......kkFFFfk..",
        ".....kFFFFffk..",
        "...kkFFFFfffk..",
        "..kFFFFFffffk..",
        ".kFFFFffffggk..",
        "kFFFffffgggk...",
        "kFFffffgggk....",
        "kFfffgggkk.....",
        "kfffgggk.......",
        "kffgggk........",
        ".kfggk.........",
        ".kfgk..........",
        "..kgk..........",
        "..kk...........",
        "...............",
    };

    public static readonly string[] WingSpread =
    {
        "..........kkkkk",
        ".......kkkFFFFk",
        ".....kkFFFFFFfk",
        "...kkFFFFFFfffk",
        "..kFFFFFFffffgk",
        ".kFFFFFfffgggk.",
        "kFFFFffffgggk..",
        "kFFfffffgggk...",
        "kFffffgggkk....",
        "kfffgggkk......",
        "kffggkk........",
        ".kfgk..........",
        ".kgk...........",
        "..kk...........",
        "...............",
        "...............",
    };
}
