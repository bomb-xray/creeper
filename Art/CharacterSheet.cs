using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace CreeperGame.Art;

/// <summary>
/// Bakes every character frame into one texture at startup.
///
/// Rendering happens once rather than per frame: each pose is drawn on the CPU,
/// then the whole set is uploaded as a single texture so drawing costs one
/// batched blit. Building at runtime instead of shipping a PNG keeps the art as
/// source code, so changing a proportion is a recompile rather than a round trip
/// through an external tool.
/// </summary>
public sealed class CharacterSheet : IDisposable
{
    public Texture2D Texture { get; }

    public int FrameWidth { get; }
    public int FrameHeight { get; }

    /// <summary>Where the feet sit inside each frame.</summary>
    public int FootX { get; }
    public int FootY { get; }

    public int FrameCount { get; }

    private readonly Rectangle[] _sources;

    public CharacterSheet(GraphicsDevice device)
    {
        var timer = Stopwatch.StartNew();

        FrameCount = PenitentPoses.TotalFrames;

        int canvasW = PenitentRig.CanvasWidth;
        int canvasH = PenitentRig.CanvasHeight;

        // Render every pose first, so the union of used area can be measured
        // before committing to a frame size.
        var rendered = new Color[FrameCount][];
        var used = Rectangle.Empty;

        for (int i = 0; i < FrameCount; i++)
        {
            PixelCanvas canvas = PenitentRig.Build(PenitentPoses.ForFrame(i));
            rendered[i] = canvas.ToArray();

            Rectangle bounds = canvas.UsedBounds();
            if (bounds.IsEmpty) continue;

            used = used.IsEmpty ? bounds : Rectangle.Union(used, bounds);
        }

        if (used.IsEmpty) used = new Rectangle(0, 0, canvasW, canvasH);

        // One pixel of margin so the outline is never clipped.
        int minX = Math.Max(0, used.Left - 1);
        int minY = Math.Max(0, used.Top - 1);
        int maxX = Math.Min(canvasW - 1, used.Right);
        int maxY = Math.Min(canvasH - 1, used.Bottom);

        FrameWidth = maxX - minX + 1;
        FrameHeight = maxY - minY + 1;

        // A shared crop box keeps every frame registered to the same origin, so
        // the figure does not jitter between poses.
        FootX = (int)(PenitentRig.CanvasWidth / 2f) - minX;
        FootY = PenitentRig.GroundY - minY;

        int sheetWidth = FrameWidth * FrameCount;
        var sheet = new Color[sheetWidth * FrameHeight];

        for (int i = 0; i < FrameCount; i++)
        {
            Color[] src = rendered[i];
            int originX = i * FrameWidth;

            for (int y = 0; y < FrameHeight; y++)
            {
                int srcRow = (minY + y) * canvasW;
                int dstRow = y * sheetWidth;

                for (int x = 0; x < FrameWidth; x++)
                {
                    sheet[dstRow + originX + x] = src[srcRow + minX + x];
                }
            }
        }

        Texture = new Texture2D(device, sheetWidth, FrameHeight);
        Texture.SetData(sheet);

        _sources = new Rectangle[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            _sources[i] = new Rectangle(i * FrameWidth, 0, FrameWidth, FrameHeight);
        }

        timer.Stop();
        Console.WriteLine(
            $"Character sheet built: {FrameCount} frames of {FrameWidth}x{FrameHeight} " +
            $"in {timer.ElapsedMilliseconds}ms");
    }

    public Rectangle Source(int frame) =>
        _sources[Math.Clamp(frame, 0, FrameCount - 1)];

    public void Dispose() => Texture?.Dispose();
}
