using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CreeperGame;

/// <summary>
/// A simple pixel font renderer that generates a bitmap font at runtime.
/// Uses a basic 8x8 pixel font for ASCII characters.
/// </summary>
public class PixelFont : IDisposable
{
    private Texture2D _fontTexture;
    private int _charWidth = 8;
    private int _charHeight = 8;
    private int _charsPerRow = 16;
    private Dictionary<char, Rectangle> _charMap = new Dictionary<char, Rectangle>();

    public PixelFont(GraphicsDevice device)
    {
        GenerateFontTexture(device);
    }

    private void GenerateFontTexture(GraphicsDevice device)
    {
        // Create a basic pixel font using ImageSharp
        // 16 chars per row, 8 rows = 128 characters (ASCII 0-127)
        int texWidth = _charsPerRow * _charWidth;
        int texHeight = 8 * _charHeight;

        using (var image = new Image<Rgba32>(texWidth, texHeight))
        {
            // Fill with transparent
            image.Mutate(ctx => ctx.BackgroundColor(new Rgba32(0, 0, 0, 0)));

            // Draw basic pixel characters
            for (int i = 32; i < 127; i++)
            {
                char c = (char)i;
                int col = i % _charsPerRow;
                int row = i / _charsPerRow;
                int x = col * _charWidth;
                int y = row * _charHeight;

                DrawChar(image, c, x, y);
                _charMap[c] = new Rectangle(x, y, _charWidth, _charHeight);
            }

            // Convert to MonoGame texture
            using (var ms = new MemoryStream())
            {
                image.SaveAsPng(ms);
                ms.Seek(0, SeekOrigin.Begin);
                _fontTexture = Texture2D.FromStream(device, ms);
            }
        }
    }

    private void DrawChar(Image<Rgba32> image, char c, int offsetX, int offsetY)
    {
        var white = new Rgba32(255, 255, 255, 255);
        
        // Simple pixel font patterns (8x8 grid)
        // Each pattern is an array of strings, each string is a row
        string[] pattern = GetCharPattern(c);
        
        if (pattern == null) return;

        for (int y = 0; y < pattern.Length && y < _charHeight; y++)
        {
            for (int x = 0; x < pattern[y].Length && x < _charWidth; x++)
            {
                if (pattern[y][x] == '#')
                {
                    image[offsetX + x, offsetY + y] = white;
                }
            }
        }
    }

    private string[] GetCharPattern(char c)
    {
        // Basic 5x7 pixel font patterns (centered in 8x8)
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
            case 'I': return new[] { "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  ", "  ##  " };
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
            case '-': return new[] { "      ", "      ", "      ", "######", "      ", "      ", "      " };
            case '+': return new[] { "      ", "  ##  ", "  ##  ", "######", "  ##  ", "  ##  ", "      " };
            case '(': return new[] { "   #  ", "  #   ", " #    ", " #    ", " #    ", "  #   ", "   #  " };
            case ')': return new[] { " #    ", "  #   ", "   #  ", "   #  ", "   #  ", "  #   ", " #    " };
            case '<': return new[] { "   #  ", "  #   ", " #    ", "#     ", " #    ", "  #   ", "   #  " };
            case '>': return new[] { " #    ", "  #   ", "   #  ", "    # ", "   #  ", "  #   ", " #    " };
            case '_': return new[] { "      ", "      ", "      ", "      ", "      ", "      ", "######" };
            case '/': return new[] { "     #", "    # ", "   #  ", "  #   ", " #    ", "#     ", "#     " };
            case '@': return new[] { " #### ", "#    #", "# ## #", "# ## #", "# ### ", "#     ", " #### " };
            default: return null;
        }
    }

    public void DrawText(SpriteBatch spriteBatch, string text, int x, int y, int pixelSize, Color color, bool centered = false)
    {
        if (_fontTexture == null || string.IsNullOrEmpty(text)) return;

        text = text.ToUpper();
        int totalWidth = text.Length * _charWidth * pixelSize;
        int startX = centered ? x - totalWidth / 2 : x;
        int startY = centered ? y - (_charHeight * pixelSize) / 2 : y;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (_charMap.ContainsKey(c))
            {
                Rectangle sourceRect = _charMap[c];
                Rectangle destRect = new Rectangle(
                    startX + i * _charWidth * pixelSize,
                    startY,
                    _charWidth * pixelSize,
                    _charHeight * pixelSize
                );
                spriteBatch.Draw(_fontTexture, destRect, sourceRect, color);
            }
        }
    }

    public int MeasureText(string text, int pixelSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length * _charWidth * pixelSize;
    }

    public void Dispose()
    {
        _fontTexture?.Dispose();
    }
}
