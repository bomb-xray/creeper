using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CreeperGame.Art;

/// <summary>
/// An in-game view of every baked frame, reachable with F2.
///
/// Judging a character by watching it run past at speed is hopeless; the frames
/// need to be seen side by side and large. This draws the whole sheet, a
/// magnified single frame and a live-playing loop, so proportions and cycle
/// timing can be checked without leaving the game.
/// </summary>
public sealed class SheetInspector
{
    private readonly CharacterSheet _sheet;
    private readonly Texture2D _pixel;

    private int _selected;
    private float _playTime;

    /// <summary>Which looping animation the live preview is playing.</summary>
    private int _previewMode;

    private static readonly string[] ModeNames = { "IDLE", "WALK" };

    public SheetInspector(CharacterSheet sheet, Texture2D pixel)
    {
        _sheet = sheet;
        _pixel = pixel;
    }

    public void Update(float dt, bool nextFrame, bool prevFrame, bool cycleMode)
    {
        _playTime += dt;

        if (nextFrame) _selected = (_selected + 1) % _sheet.FrameCount;
        if (prevFrame) _selected = (_selected - 1 + _sheet.FrameCount) % _sheet.FrameCount;
        if (cycleMode) _previewMode = (_previewMode + 1) % ModeNames.Length;
    }

    public void Draw(SpriteBatch spriteBatch, TextRenderer text, int screenWidth,
        int screenHeight, float baseTextSize)
    {
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(18, 17, 24));

        float small = baseTextSize * 0.62f;

        text.DrawShadowed(spriteBatch, "SPRITE INSPECTOR", screenWidth / 2f,
            baseTextSize, baseTextSize, new Color(220, 220, 230), true);

        // ---- contact sheet across the top ----

        int stripScale = 1;
        int stripTop = (int)(baseTextSize * 2.6f);
        int totalWidth = _sheet.FrameWidth * _sheet.FrameCount * stripScale;

        // Shrink the strip until it fits, rather than letting it run off screen.
        while (totalWidth > screenWidth - 40 && stripScale > 1)
        {
            stripScale--;
            totalWidth = _sheet.FrameWidth * _sheet.FrameCount * stripScale;
        }

        int stripLeft = (screenWidth - totalWidth) / 2;

        for (int i = 0; i < _sheet.FrameCount; i++)
        {
            int x = stripLeft + i * _sheet.FrameWidth * stripScale;

            var cell = new Rectangle(x, stripTop,
                _sheet.FrameWidth * stripScale, _sheet.FrameHeight * stripScale);

            // Alternating backing makes the frame boundaries readable.
            spriteBatch.Draw(_pixel, cell,
                i == _selected ? new Color(58, 40, 40) : new Color(28, 27, 36));

            spriteBatch.Draw(_sheet.Texture, cell, _sheet.Source(i), Color.White);

            if (i == _selected)
            {
                DrawBorder(spriteBatch, cell, new Color(220, 60, 60));
            }
        }

        // ---- magnified single frame on the left ----

        int detailTop = stripTop + _sheet.FrameHeight * stripScale + 24;
        int available = screenHeight - detailTop - (int)(baseTextSize * 4f);
        int detailScale = Math.Max(1, available / _sheet.FrameHeight);

        int detailW = _sheet.FrameWidth * detailScale;
        int detailH = _sheet.FrameHeight * detailScale;
        int detailX = screenWidth / 4 - detailW / 2;

        var detailRect = new Rectangle(detailX, detailTop, detailW, detailH);
        spriteBatch.Draw(_pixel, detailRect, new Color(26, 25, 33));

        // Ground line, so foot placement can be checked frame to frame.
        int groundLine = detailTop + _sheet.FootY * detailScale;
        spriteBatch.Draw(_pixel,
            new Rectangle(detailX, groundLine, detailW, Math.Max(1, detailScale / 2)),
            new Color(90, 80, 70));

        // Centre line through the foot anchor.
        int centreLine = detailX + _sheet.FootX * detailScale;
        spriteBatch.Draw(_pixel,
            new Rectangle(centreLine, detailTop, Math.Max(1, detailScale / 2), detailH),
            new Color(70, 70, 90));

        spriteBatch.Draw(_sheet.Texture, detailRect, _sheet.Source(_selected), Color.White);

        text.DrawShadowed(spriteBatch, $"FRAME {_selected}  {PenitentPoses.FrameName(_selected)}",
            detailX + detailW / 2f, detailTop + detailH + small * 1.4f, small,
            new Color(200, 200, 210), true);

        // ---- live loop on the right ----

        int liveX = screenWidth * 3 / 4 - detailW / 2;
        var liveRect = new Rectangle(liveX, detailTop, detailW, detailH);
        spriteBatch.Draw(_pixel, liveRect, new Color(26, 25, 33));

        spriteBatch.Draw(_pixel,
            new Rectangle(liveX, groundLine, detailW, Math.Max(1, detailScale / 2)),
            new Color(90, 80, 70));

        int liveFrame = _previewMode == 0
            ? PenitentPoses.IdleStart + (int)(_playTime * 6f) % PenitentPoses.IdleFrames
            : PenitentPoses.WalkStart + (int)(_playTime * 12f) % PenitentPoses.WalkFrames;

        spriteBatch.Draw(_sheet.Texture, liveRect, _sheet.Source(liveFrame), Color.White);

        text.DrawShadowed(spriteBatch, $"PLAYING {ModeNames[_previewMode]}",
            liveX + detailW / 2f, detailTop + detailH + small * 1.4f, small,
            new Color(200, 200, 210), true);

        // ---- footer ----

        text.DrawShadowed(spriteBatch,
            $"SHEET {_sheet.Texture.Width}x{_sheet.Texture.Height}   " +
            $"FRAME {_sheet.FrameWidth}x{_sheet.FrameHeight}   " +
            $"FOOT {_sheet.FootX},{_sheet.FootY}",
            screenWidth / 2f, screenHeight - small * 4f, small * 0.9f,
            new Color(140, 140, 150), true);

        text.DrawShadowed(spriteBatch,
            "LEFT / RIGHT - FRAME     TAB - SWITCH LOOP     F2 OR ESC - BACK",
            screenWidth / 2f, screenHeight - small * 2f, small,
            new Color(170, 170, 180), true);
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color colour)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), colour);
    }
}
