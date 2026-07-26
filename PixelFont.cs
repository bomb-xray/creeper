using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CreeperGame;

/// <summary>
/// A tiny bitmap font that is generated at runtime, with no external font files
/// and no third-party imaging dependencies (avoids Color/Rectangle name clashes).
/// Glyphs are 6x7 pixels stored inside 8x8 cells of a 128x64 atlas texture.
/// </summary>
public class PixelFont : IDisposable
{
    private const int CellSize = 8;      // Size of one cell in the atlas
    private const int GlyphWidth = 6;    // Visible width of a glyph
    private const int GlyphHeight = 7;   // Visible height of a glyph
    private const int Advance = 7;       // Horizontal step between glyphs (glyph + 1px gap)
    private const int Columns = 16;      // Cells per atlas row
    private const int Rows = 8;          // Atlas rows (ASCII 0-127)

    private readonly Texture2D _fontTexture;
    private readonly Dictionary<char, Rectangle> _charMap = new Dictionary<char, Rectangle>();

    public PixelFont(GraphicsDevice device)
    {
        int texWidth = Columns * CellSize;
        int texHeight = Rows * CellSize;

        // Transparent by default (Color default value is 0,0,0,0)
        var pixels = new Color[texWidth * texHeight];

        for (int code = 32; code < 127; code++)
        {
            char c = (char)code;
            string[]? pattern = GetCharPattern(c);
            if (pattern == null) continue;

            int cellX = (code % Columns) * CellSize;
            int cellY = (code / Columns) * CellSize;

            for (int y = 0; y < pattern.Length && y < GlyphHeight; y++)
            {
                string row = pattern[y];
                for (int x = 0; x < row.Length && x < GlyphWidth; x++)
                {
                    if (row[x] == '#')
                    {
                        pixels[(cellY + y) * texWidth + (cellX + x)] = Color.White;
                    }
                }
            }

            _charMap[c] = new Rectangle(cellX, cellY, GlyphWidth, GlyphHeight);
        }

        _fontTexture = new Texture2D(device, texWidth, texHeight);
        _fontTexture.SetData(pixels);
    }

    /// <summary>Width in screen pixels that the given text will occupy.</summary>
    public int MeasureText(string text, int pixelSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (text.Length * Advance - 1) * pixelSize;
    }

    /// <summary>Height in screen pixels of a single line of text.</summary>
    public int LineHeight(int pixelSize) => GlyphHeight * pixelSize;

    /// <summary>
    /// Draws text. When <paramref name="centered"/> is true the given position is
    /// treated as the center of the text block instead of its top-left corner.
    /// </summary>
    public void DrawText(SpriteBatch spriteBatch, string text, int x, int y, int pixelSize, Color color, bool centered = false)
    {
        if (_fontTexture == null || string.IsNullOrEmpty(text)) return;
        if (pixelSize < 1) pixelSize = 1;

        text = text.ToUpperInvariant();

        int startX = centered ? x - MeasureText(text, pixelSize) / 2 : x;
        int startY = centered ? y - LineHeight(pixelSize) / 2 : y;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ' ') continue;
            if (!_charMap.TryGetValue(c, out Rectangle source)) continue;

            var dest = new Rectangle(
                startX + i * Advance * pixelSize,
                startY,
                GlyphWidth * pixelSize,
                GlyphHeight * pixelSize);

            spriteBatch.Draw(_fontTexture, dest, source, color);
        }
    }

    private string[]? GetCharPattern(char c)
    {
        switch (c)
        {
            case 'A': return new[] { "  ##  ", " #  # ", "#    #", "#    #", "######", "#    #", "#    #" };
            case 'B': return new[] { "##### ", "#    #", "#    #", "##### ", "#    #", "#    #", "##### " };
            case 'C': return new[] { " #### ", "#    #", "#     ", "#     ", "#     ", "#    #", " #### " };
            case 'D': return new[] { "####  ", "#   # ", "#    #", "#    #", "#    #", "#   # ", "####  " };
            case 'E': return new[] { "######", "#     ", "#     ", "####  ", "#     ", "#     ", "######" };
            case 'F': return new[] { "######", "#     ", "#     ", "####  ", "#     ", "#     ", "#     " };
            case 'G': return new[] { " #### ", "#    #", "#     ", "# ### ", "#   # ", "#   # ", " ### #" };
            case 'H': return new[] { "#    #", "#    #", "#    #", "######", "#    #", "#    #", "#    #" };
            case 'I': return new[] { " #### ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", " #### " };
            case 'J': return new[] { "     #", "     #", "     #", "     #", "     #", "#    #", " #### " };
            case 'K': return new[] { "#   # ", "#  #  ", "# #   ", "##    ", "# #   ", "#  #  ", "#   # " };
            case 'L': return new[] { "#     ", "#     ", "#     ", "#     ", "#     ", "#     ", "######" };
            case 'M': return new[] { "#    #", "##  ##", "# ## #", "# ## #", "#    #", "#    #", "#    #" };
            case 'N': return new[] { "#    #", "##   #", "# #  #", "#  # #", "#   ##", "#    #", "#    #" };
            case 'O': return new[] { " #### ", "#    #", "#    #", "#    #", "#    #", "#    #", " #### " };
            case 'P': return new[] { "##### ", "#    #", "#    #", "##### ", "#     ", "#     ", "#     " };
            case 'Q': return new[] { " #### ", "#    #", "#    #", "#    #", "# #  #", "#  # #", " ### #" };
            case 'R': return new[] { "##### ", "#    #", "#    #", "##### ", "#  #  ", "#   # ", "#    #" };
            case 'S': return new[] { " #### ", "#    #", "#     ", " #### ", "     #", "#    #", " #### " };
            case 'T': return new[] { "######", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  " };
            case 'U': return new[] { "#    #", "#    #", "#    #", "#    #", "#    #", "#    #", " #### " };
            case 'V': return new[] { "#    #", "#    #", "#    #", "#    #", "#    #", " #  # ", "  ##  " };
            case 'W': return new[] { "#    #", "#    #", "#    #", "# ## #", "# ## #", "##  ##", "#    #" };
            case 'X': return new[] { "#    #", " #  # ", "  ##  ", "  ##  ", "  ##  ", " #  # ", "#    #" };
            case 'Y': return new[] { "#    #", " #  # ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  " };
            case 'Z': return new[] { "######", "    # ", "   #  ", "  #   ", " #    ", "#     ", "######" };
            case '0': return new[] { " #### ", "#   ##", "#  # #", "# #  #", "##   #", "#    #", " #### " };
            case '1': return new[] { "  ##  ", " ###  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "######" };
            case '2': return new[] { " #### ", "#    #", "     #", "   ## ", " ##   ", "#     ", "######" };
            case '3': return new[] { " #### ", "#    #", "     #", "  ### ", "     #", "#    #", " #### " };
            case '4': return new[] { "   #  ", "  ##  ", " # #  ", "#  #  ", "######", "   #  ", "   #  " };
            case '5': return new[] { "######", "#     ", "##### ", "     #", "     #", "#    #", " #### " };
            case '6': return new[] { " #### ", "#    #", "#     ", "##### ", "#    #", "#    #", " #### " };
            case '7': return new[] { "######", "    # ", "   #  ", "  #   ", " #    ", " #    ", " #    " };
            case '8': return new[] { " #### ", "#    #", "#    #", " #### ", "#    #", "#    #", " #### " };
            case '9': return new[] { " #### ", "#    #", "#    #", " #####", "     #", "#    #", " #### " };
            case ' ': return new[] { "      ", "      ", "      ", "      ", "      ", "      ", "      " };
            case '.': return new[] { "      ", "      ", "      ", "      ", "      ", "  ##  ", "  ##  " };
            case ',': return new[] { "      ", "      ", "      ", "      ", "  ##  ", "  ##  ", " #    " };
            case '!': return new[] { "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "      ", "  ##  " };
            case '?': return new[] { " #### ", "#    #", "     #", "   ## ", "  ##  ", "      ", "  ##  " };
            case ':': return new[] { "      ", "  ##  ", "  ##  ", "      ", "  ##  ", "  ##  ", "      " };
            case ';': return new[] { "      ", "  ##  ", "  ##  ", "      ", "  ##  ", "  ##  ", " #    " };
            case '\'': return new[] { "  ##  ", "  ##  ", "  #   ", "      ", "      ", "      ", "      " };
            case '"': return new[] { " ## ##", " ## ##", " #  # ", "      ", "      ", "      ", "      " };
            case '-': return new[] { "      ", "      ", "      ", "######", "      ", "      ", "      " };
            case '+': return new[] { "      ", "  ##  ", "  ##  ", "######", "  ##  ", "  ##  ", "      " };
            case '=': return new[] { "      ", "      ", "######", "      ", "######", "      ", "      " };
            case '*': return new[] { "      ", "#  #  ", " ###  ", "##### ", " ###  ", "#  #  ", "      " };
            case '%': return new[] { "##   #", "##  # ", "   #  ", "  #   ", " #  ##", "#   ##", "      " };
            case '(': return new[] { "   #  ", "  #   ", " #    ", " #    ", " #    ", "  #   ", "   #  " };
            case ')': return new[] { " #    ", "  #   ", "   #  ", "   #  ", "   #  ", "  #   ", " #    " };
            case '[': return new[] { "  ### ", "  #   ", "  #   ", "  #   ", "  #   ", "  #   ", "  ### " };
            case ']': return new[] { " ###  ", "   #  ", "   #  ", "   #  ", "   #  ", "   #  ", " ###  " };
            case '<': return new[] { "   #  ", "  #   ", " #    ", "#     ", " #    ", "  #   ", "   #  " };
            case '>': return new[] { " #    ", "  #   ", "   #  ", "    # ", "   #  ", "  #   ", " #    " };
            case '_': return new[] { "      ", "      ", "      ", "      ", "      ", "      ", "######" };
            case '/': return new[] { "     #", "    # ", "   #  ", "  #   ", " #    ", "#     ", "#     " };
            case '\\': return new[] { "#     ", "#     ", " #    ", "  #   ", "   #  ", "    # ", "     #" };
            case '@': return new[] { " #### ", "#    #", "# ## #", "# ## #", "# ### ", "#     ", " #### " };
            case '#': return new[] { " #  # ", "######", " #  # ", " #  # ", "######", " #  # ", "      " };
            case '&': return new[] { " ##   ", "#  #  ", " ##   ", " ## # ", "#  ## ", "#   # ", " ### #" };
            default: return null;
        }
    }

    public void Dispose()
    {
        _fontTexture?.Dispose();
    }
}
