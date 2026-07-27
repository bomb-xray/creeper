using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CreeperGame;

/// <summary>
/// One scrolling layer of the backdrop.
/// </summary>
public class ParallaxLayer
{
    public Texture2D Texture { get; }

    /// <summary>
    /// How fast this layer scrolls relative to the camera. 0 is painted on the
    /// screen and never moves, 1 keeps pace with the world.
    /// </summary>
    public float ScrollFactor { get; }

    /// <summary>Fraction of the screen height the layer's bottom edge sits at.</summary>
    public float BottomAnchor { get; }

    /// <summary>Height of the layer as a fraction of the screen height.</summary>
    public float HeightFactor { get; }

    /// <summary>Tint applied when drawing, used to push distant layers back.</summary>
    public Color Tint { get; }

    public ParallaxLayer(Texture2D texture, float scrollFactor, float bottomAnchor,
        float heightFactor, Color tint)
    {
        Texture = texture;
        ScrollFactor = scrollFactor;
        BottomAnchor = bottomAnchor;
        HeightFactor = heightFactor;
        Tint = tint;
    }
}

/// <summary>
/// Draws the layered backdrop: sky, mountains, walls and ground, each scrolling
/// at its own rate so the scene reads as having depth.
///
/// Layers tile horizontally without end, so the player can walk as far as they
/// like in either direction.
/// </summary>
public class ParallaxBackground : IDisposable
{
    private readonly List<ParallaxLayer> _layers = new List<ParallaxLayer>();

    /// <summary>Where the ground surface sits, as a fraction of the screen height.</summary>
    public float GroundLine { get; private set; } = 0.82f;

    /// <summary>True when at least one layer loaded.</summary>
    public bool HasArt => _layers.Count > 0;

    public ParallaxBackground(GraphicsDevice device, string assetDir)
    {
        // Furthest away first, so the draw order is back to front.
        // The scroll factors are deliberately far apart: a small spread reads as
        // a flat image sliding, a wide one reads as distance.

        Texture2D? sky = TextureLoader.LoadAny(device, assetDir,
            new[] { "sky", "background", "bg_sky" }, true);
        if (sky != null)
        {
            // Barely moves: the sky is effectively at infinity.
            _layers.Add(new ParallaxLayer(sky, 0.05f, 1.0f, 1.0f, Color.White));
        }

        Texture2D? mountains = TextureLoader.LoadAny(device, assetDir,
            new[] { "mountains", "mountain", "bg_mountains" }, true);
        if (mountains != null)
        {
            // Dimmed slightly so it recedes behind the walls.
            _layers.Add(new ParallaxLayer(mountains, 0.20f, 0.88f, 0.52f,
                new Color(190, 195, 210)));
        }

        Texture2D? walls = TextureLoader.LoadAny(device, assetDir,
            new[] { "walls", "wall", "ruins", "bg_walls" }, true);
        if (walls != null)
        {
            _layers.Add(new ParallaxLayer(walls, 0.45f, 0.86f, 0.42f,
                new Color(225, 225, 235)));
        }

        Texture2D? ground = TextureLoader.LoadAny(device, assetDir,
            new[] { "ground", "floor", "bg_ground" }, true);
        if (ground != null)
        {
            // The ground is the play surface, so it tracks the camera exactly.
            _layers.Add(new ParallaxLayer(ground, 1.0f, 1.0f, 0.26f, Color.White));
            GroundLine = 1.0f - 0.26f + 0.02f;
        }
    }

    /// <summary>
    /// Draws every layer for the given camera position.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, float cameraX, int screenWidth, int screenHeight)
    {
        foreach (ParallaxLayer layer in _layers)
        {
            int height = (int)(screenHeight * layer.HeightFactor);
            int bottom = (int)(screenHeight * layer.BottomAnchor);
            int top = bottom - height;

            // Preserve the source aspect ratio so nothing looks stretched.
            float scale = height / (float)layer.Texture.Height;
            int width = Math.Max(1, (int)(layer.Texture.Width * scale));

            // Where this layer has scrolled to, wrapped into one tile width.
            float offset = -cameraX * layer.ScrollFactor;
            float wrapped = offset % width;
            if (wrapped > 0) wrapped -= width;

            // Enough copies to cover the screen, plus one for the seam.
            for (float x = wrapped; x < screenWidth; x += width)
            {
                spriteBatch.Draw(layer.Texture,
                    new Rectangle((int)MathF.Round(x), top, width, height),
                    layer.Tint);
            }
        }
    }

    public void Dispose()
    {
        foreach (ParallaxLayer layer in _layers)
        {
            layer.Texture?.Dispose();
        }
        _layers.Clear();
    }
}
