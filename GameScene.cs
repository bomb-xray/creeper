using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace CreeperGame;

/// <summary>
/// The playable side-scrolling scene: parallax backdrop, the player character
/// and a camera that follows them.
/// </summary>
public class GameScene : IDisposable
{
    private readonly ParallaxBackground _background;
    private readonly Character _player;

    private int _screenWidth;
    private int _screenHeight;

    /// <summary>Camera position in world space (left edge of the view).</summary>
    private float _cameraX;

    /// <summary>How far the player may roam either side of the start point.</summary>
    private const float WorldHalfWidth = 6000f;

    /// <summary>Prevents a held key from repeating; only edges count.</summary>
    private KeyboardState _prevKeyboard;

    public Character Player => _player;
    public bool HasArt => _background.HasArt || _player.HasArt;

    /// <summary>Everything that failed to load, so the game can say what is missing.</summary>
    public List<string> MissingArt { get; } = new List<string>();

    /// <summary>Absolute path that was searched, shown when art is missing.</summary>
    public string AssetPath { get; }

    /// <summary>Image files actually present in that folder.</summary>
    public List<string> FoundFiles { get; } = new List<string>();

    public GameScene(GraphicsDevice device, string assetDir, int screenWidth, int screenHeight)
    {
        _background = new ParallaxBackground(device, assetDir);
        _player = new Character(device, assetDir);

        AssetPath = Path.GetFullPath(assetDir);

        MissingArt.AddRange(_background.MissingLayers);
        if (!_player.HasArt) MissingArt.Add("side");

        // List what is really there, which is the fastest way to spot a wrong
        // folder or a misspelled file name.
        try
        {
            foreach (string file in Directory.GetFiles(AssetPath))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                {
                    FoundFiles.Add(Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not list the assets folder: {ex.Message}");
        }

        Console.WriteLine($"Scene assets: {AssetPath}");
        Console.WriteLine($"  images present: {(FoundFiles.Count > 0 ? string.Join(", ", FoundFiles) : "none")}");
        Console.WriteLine($"  missing: {(MissingArt.Count > 0 ? string.Join(", ", MissingArt) : "nothing")}");

        Resize(screenWidth, screenHeight);

        // Start on the left of the map, as requested.
        _player.Position = new Vector2(-WorldHalfWidth + _screenWidth * 0.25f, _player.GroundY);
        _cameraX = _player.Position.X - _screenWidth * 0.35f;
    }

    public void Resize(int screenWidth, int screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        // Feet rest slightly into the ground layer so the character is not
        // floating on top of the tiles.
        _player.GroundY = screenHeight * _background.GroundLine;
        _player.DisplayHeight = screenHeight * 0.30f;
    }

    public void Update(float dt, KeyboardState kb)
    {
        var input = new Character.Input
        {
            Move = 0,
            JumpHeld = kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up),
            CrouchHeld = kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down),
            // Right shift dashes, as requested; left shift works too.
            DashPressed = WasPressed(kb, Keys.RightShift) || WasPressed(kb, Keys.LeftShift)
        };

        if (kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A)) input.Move -= 1;
        if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) input.Move += 1;

        input.JumpPressed = WasPressed(kb, Keys.Space)
                         || WasPressed(kb, Keys.W)
                         || WasPressed(kb, Keys.Up);

        _player.Update(dt, input, -WorldHalfWidth, WorldHalfWidth);

        UpdateCamera(dt);

        _prevKeyboard = kb;
    }

    private bool WasPressed(KeyboardState kb, Keys key)
        => kb.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);

    private void UpdateCamera(float dt)
    {
        // Aim to keep the player a little left of centre so there is more room
        // to see what is coming when walking right.
        float targetX = _player.Position.X - _screenWidth * 0.42f;

        // Critically damped follow: quick but never jittery.
        _cameraX = MathHelper.Lerp(_cameraX, targetX, MathF.Min(1f, 6f * dt));

        _cameraX = MathHelper.Clamp(_cameraX, -WorldHalfWidth, WorldHalfWidth - _screenWidth);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _background.Draw(spriteBatch, _cameraX, _screenWidth, _screenHeight);
        _player.Draw(spriteBatch, _cameraX);
    }

    public void Dispose()
    {
        _background?.Dispose();
        _player?.Dispose();
    }
}
