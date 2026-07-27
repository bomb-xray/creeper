using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace CreeperGame;

/// <summary>Which way the character is facing. Left reuses the side view mirrored.</summary>
public enum Direction
{
    Down,
    Up,
    Left,
    Right
}

/// <summary>
/// A character built from three turnaround views (front, back, side).
///
/// Three static drawings cannot be skeletally rigged, so movement is sold with
/// procedural motion instead: a vertical bob, a slight lean into the direction of
/// travel and a squash on footfall. That is enough to read as walking, and the
/// same code keeps working later if real frame strips are dropped in.
/// </summary>
public class Character : IDisposable
{
    /// <summary>File name stems accepted for each view, in priority order.</summary>
    private static readonly Dictionary<string, string[]> ViewNames = new()
    {
        ["front"] = new[] { "char_front", "character_front", "player_front", "front", "char_down" },
        ["back"] = new[] { "char_back", "character_back", "player_back", "back", "char_up" },
        ["side"] = new[] { "char_side", "character_side", "player_side", "side", "char_right" }
    };

    private readonly Texture2D? _front;
    private readonly Texture2D? _back;
    private readonly Texture2D? _side;
    private readonly Texture2D _shadow;

    /// <summary>Height the character is drawn at, in screen pixels.</summary>
    public float DisplayHeight { get; set; } = 200f;

    /// <summary>Position of the character's feet in world/screen space.</summary>
    public Vector2 Position;

    public Direction Facing { get; private set; } = Direction.Down;

    /// <summary>Pixels per second.</summary>
    public float Speed { get; set; } = 260f;

    /// <summary>True when at least one view loaded, so the caller can warn the player.</summary>
    public bool HasArt => _front != null || _back != null || _side != null;

    /// <summary>Names of the views that failed to load, for the on-screen hint.</summary>
    public List<string> MissingViews { get; } = new List<string>();

    private float _walkCycle;
    private bool _moving;

    public Character(GraphicsDevice device, string assetDir)
    {
        _front = LoadView(device, assetDir, "front");
        _back = LoadView(device, assetDir, "back");
        _side = LoadView(device, assetDir, "side");

        // A soft blob under the feet grounds the sprite; without it the character
        // looks like it is floating over the background.
        _shadow = CreateShadowTexture(device, 64);
    }

    private Texture2D? LoadView(GraphicsDevice device, string assetDir, string view)
    {
        foreach (string stem in ViewNames[view])
        {
            // Reuse the converter so odd JPEGs and the like are handled too.
            string? path = ImageConverter.EnsureLoadableImage(assetDir, stem);
            if (path == null || !File.Exists(path)) continue;

            try
            {
                using var stream = File.OpenRead(path);
                var texture = Texture2D.FromStream(device, stream);
                Console.WriteLine($"Character {view}: {path} ({texture.Width}x{texture.Height})");
                return texture;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Character {view} failed to load from {path}: {ex.Message}");
            }
        }

        Console.WriteLine($"Character {view}: not found (tried {string.Join(", ", ViewNames[view])})");
        MissingViews.Add(view);
        return null;
    }

    /// <summary>Builds a radial-gradient blob used as a drop shadow.</summary>
    private static Texture2D CreateShadowTexture(GraphicsDevice device, int size)
    {
        var pixels = new Color[size * size];
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - radius + 0.5f) / radius;
                float dy = (y - radius + 0.5f) / radius;
                float distance = MathF.Sqrt(dx * dx + dy * dy);

                // Fades to nothing at the rim.
                float strength = MathHelper.Clamp(1f - distance, 0f, 1f);
                strength *= strength;

                pixels[y * size + x] = new Color(0, 0, 0, (byte)(strength * 140));
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>
    /// Moves the character. <paramref name="input"/> is the raw direction from the
    /// keyboard; it is normalised so diagonals are not faster.
    /// </summary>
    public void Update(float dt, Vector2 input, Rectangle bounds)
    {
        _moving = input != Vector2.Zero;

        if (_moving)
        {
            input.Normalize();
            Position += input * Speed * dt;

            // Horizontal input wins when moving diagonally, which keeps the side
            // view on screen during diagonal walks (it reads better than the back).
            if (MathF.Abs(input.X) > 0.35f)
            {
                Facing = input.X > 0 ? Direction.Right : Direction.Left;
            }
            else if (input.Y > 0)
            {
                Facing = Direction.Down;
            }
            else if (input.Y < 0)
            {
                Facing = Direction.Up;
            }

            _walkCycle += dt * 9f;
        }
        else
        {
            // Idle breathing, much slower than the walk.
            _walkCycle += dt * 1.8f;
        }

        // Keep the feet inside the playable area.
        Position.X = MathHelper.Clamp(Position.X, bounds.Left, bounds.Right);
        Position.Y = MathHelper.Clamp(Position.Y, bounds.Top, bounds.Bottom);
    }

    /// <summary>Picks the texture for the current facing, and whether to mirror it.</summary>
    private (Texture2D? texture, bool flip) CurrentView()
    {
        return Facing switch
        {
            Direction.Up => (_back ?? _front, false),
            Direction.Left => (_side ?? _front, true),   // side view mirrored
            Direction.Right => (_side ?? _front, false),
            _ => (_front ?? _side, false)
        };
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        (Texture2D? texture, bool flip) = CurrentView();
        if (texture == null) return;

        float scale = DisplayHeight / texture.Height;
        float width = texture.Width * scale;

        // Procedural motion: bob up and down, and squash slightly on footfall.
        float bobPhase = MathF.Sin(_walkCycle);
        float amplitude = _moving ? DisplayHeight * 0.022f : DisplayHeight * 0.006f;
        float bob = -MathF.Abs(bobPhase) * amplitude;

        float squash = _moving ? 1f - MathF.Abs(bobPhase) * 0.03f : 1f;
        float lean = _moving ? bobPhase * 0.02f : 0f;

        float drawHeight = DisplayHeight * squash;
        float drawWidth = width / squash; // preserve area, so the squash looks elastic

        // Shadow first, sized to the sprite and tightening as the character rises.
        float shadowWidth = width * 0.55f;
        float shadowHeight = shadowWidth * 0.32f;
        float shadowShrink = 1f - MathF.Abs(bob) / MathF.Max(1f, amplitude) * 0.12f;

        spriteBatch.Draw(_shadow,
            new Rectangle(
                (int)(Position.X - shadowWidth * shadowShrink / 2f),
                (int)(Position.Y - shadowHeight * shadowShrink / 2f),
                (int)(shadowWidth * shadowShrink),
                (int)(shadowHeight * shadowShrink)),
            Color.White);

        // Origin at the bottom centre so the feet sit on Position.
        var origin = new Vector2(texture.Width / 2f, texture.Height);

        spriteBatch.Draw(
            texture,
            new Vector2(Position.X, Position.Y + bob),
            null,
            Color.White,
            lean,
            origin,
            new Vector2(drawWidth / texture.Width, drawHeight / texture.Height),
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }

    public void Dispose()
    {
        _front?.Dispose();
        _back?.Dispose();
        _side?.Dispose();
        _shadow?.Dispose();
    }
}
