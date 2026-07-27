using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace CreeperGame.Art;

/// <summary>
/// Bakes every knight frame into one texture at startup.
///
/// Rendering happens once, not per frame: each pose is rasterised and shaded on
/// the CPU, then the whole set is uploaded as a single texture so drawing is one
/// batched blit per sprite. Building at runtime rather than shipping a PNG means
/// the art is genuinely source code, and changing a proportion is a recompile
/// rather than an art pipeline round trip.
/// </summary>
public sealed class KnightSheet : IDisposable
{
    public Texture2D Texture { get; }

    public int FrameWidth { get; }
    public int FrameHeight { get; }

    /// <summary>Where the feet sit inside each frame.</summary>
    public int FootX { get; }
    public int FootY { get; }

    public int FrameCount { get; }

    private readonly Rectangle[] _sources;

    public KnightSheet(GraphicsDevice device)
    {
        var timer = Stopwatch.StartNew();

        FrameCount = KnightPoses.TotalFrames;

        int canvasW = KnightRig.CanvasWidth;
        int canvasH = KnightRig.CanvasHeight;

        // Render every pose into its own pixel buffer first, so the used area
        // across the whole set can be measured before committing to a size.
        var rendered = new Color[FrameCount][];

        int minX = canvasW, maxX = -1, minY = canvasH, maxY = -1;

        for (int i = 0; i < FrameCount; i++)
        {
            KnightPose pose = KnightPoses.ForFrame(i);
            rendered[i] = KnightRig.Build(pose).Render();

            MeasureUsed(rendered[i], canvasW, canvasH,
                ref minX, ref maxX, ref minY, ref maxY);
        }

        if (maxX < 0)
        {
            // Nothing drew, which should be impossible; fall back to the canvas.
            minX = 0; minY = 0; maxX = canvasW - 1; maxY = canvasH - 1;
        }

        // One pixel of breathing room stops the outline being clipped.
        minX = Math.Max(0, minX - 1);
        minY = Math.Max(0, minY - 1);
        maxX = Math.Min(canvasW - 1, maxX + 1);
        maxY = Math.Min(canvasH - 1, maxY + 1);

        FrameWidth = maxX - minX + 1;
        FrameHeight = maxY - minY + 1;

        // Cropping every frame by the same box keeps them registered, so the
        // figure does not jitter between poses.
        FootX = (int)(KnightRig.CanvasWidth / 2f) - minX;
        FootY = KnightRig.GroundY - minY;

        var sheet = new Color[FrameWidth * FrameCount * FrameHeight];
        int sheetWidth = FrameWidth * FrameCount;

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
            $"Knight sheet built: {FrameCount} frames of {FrameWidth}x{FrameHeight} " +
            $"in {timer.ElapsedMilliseconds}ms");
    }

    private static void MeasureUsed(Color[] pixels, int width, int height,
        ref int minX, ref int maxX, ref int minY, ref int maxY)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].A == 0) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
    }

    public Rectangle Source(int frame)
    {
        if (frame < 0) frame = 0;
        if (frame >= FrameCount) frame = FrameCount - 1;
        return _sources[frame];
    }

    public void Dispose() => Texture?.Dispose();
}
