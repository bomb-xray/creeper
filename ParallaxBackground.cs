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
    /// The part of the texture that actually holds artwork. The source images are
    /// padded with key colour, so only this region is drawn.
    /// </summary>
    public Rectangle Source { get; }

    /// <summary>
    /// How fast this layer scrolls relative to the camera. 0 is painted on the
    /// screen and never moves, 1 keeps pace with the world.
    /// </summary>
    public float ScrollFactor { get; }

    /// <summary>Where the layer's bottom edge sits, as a fraction of screen height.</summary>
    public float BottomAnchor { get; }

    /// <summary>Height of the drawn artwork as a fraction of the screen height.</summary>
    public float HeightFactor { get; }

    /// <summary>Tint applied when drawing, used to push distant layers back.</summary>
    public Color Tint { get; }

    public ParallaxLayer(Texture2D texture, Rectangle source, float scrollFactor,
        float bottomAnchor, float heightFactor, Color tint)
    {
        Texture = texture;
        Source = source;
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
    public float GroundLine { get; private set; } = 0.86f;

    /// <summary>True when at least one layer loaded.</summary>
    public bool HasArt => _layers.Count > 0;

    /// <summary>Layer names that could not be found, for the on-screen report.</summary>
    public List<string> MissingLayers { get; } = new List<string>();

    // How tall each layer is drawn, as a fraction of the screen height.
    private const float GroundHeight = 0.17f;
    private const float WallsHeight = 0.40f;
    private const float MountainsHeight = 0.30f;

    public ParallaxBackground(GraphicsDevice device, string assetDir)
    {
        // Furthest away first, so the draw order runs back to front.
        // The scroll factors are deliberately far apart: a narrow spread reads as
        // one flat image sliding, a wide one reads as real distance.

        // Where the ground surface ends up, used to seat the layers above it.
        float groundTop = 1f - GroundHeight;

        Texture2D? sky = TextureLoader.LoadAny(device, assetDir,
            new[] { "sky", "background", "bg_sky" }, true);

        if (sky == null) MissingLayers.Add("sky");
        else
        {
            // Barely moves: the sky is effectively at infinity. It covers the
            // whole screen so nothing shows through behind the other layers.
            _layers.Add(new ParallaxLayer(sky, TextureLoader.GetOpaqueBounds(sky),
                0.05f, 1.0f, 1.0f, Color.White));
        }

        Texture2D? mountains = TextureLoader.LoadAny(device, assetDir,
            new[] { "mountains", "mountain", "bg_mountains" }, true);

        if (mountains == null) MissingLayers.Add("mountains");
        else
        {
            // Dimmed and cooled slightly so it recedes behind the ruins.
            _layers.Add(new ParallaxLayer(mountains, TextureLoader.GetOpaqueBounds(mountains),
                0.20f, groundTop + 0.02f, MountainsHeight, new Color(175, 185, 205)));
        }

        Texture2D? walls = TextureLoader.LoadAny(device, assetDir,
            new[] { "walls", "wall", "ruins", "bg_walls" }, true);

        if (walls == null) MissingLayers.Add("walls");
        else
        {
            _layers.Add(new ParallaxLayer(walls, TextureLoader.GetOpaqueBounds(walls),
                0.45f, groundTop + 0.01f, WallsHeight, new Color(225, 222, 230)));
        }

        Texture2D? ground = TextureLoader.LoadAny(device, assetDir,
            new[] { "ground", "floor", "bg_ground" }, true);

        if (ground == null) MissingLayers.Add("ground");
        else
        {
            // The ground is the play surface, so it tracks the camera exactly.
            _layers.Add(new ParallaxLayer(ground, TextureLoader.GetOpaqueBounds(ground),
                1.0f, 1.0f, GroundHeight, Color.White));

            // Feet sit just below the top edge of the stonework.
            GroundLine = groundTop + 0.03f;
        }
    }

    /// <summary>Draws every layer for the given camera position.</summary>
    public void Draw(SpriteBatch spriteBatch, float cameraX, int screenWidth, int screenHeight)
    {
        foreach (ParallaxLayer layer in _layers)
        {
            int height = (int)(screenHeight * layer.HeightFactor);
            int bottom = (int)(screenHeight * layer.BottomAnchor);
            int top = bottom - height;

            // Scale from the opaque region so padding never inflates the size.
            float scale = height / (float)layer.Source.Height;
            int width = Math.Max(1, (int)(layer.Source.Width * scale));

            // Where this layer has scrolled to, wrapped into a single tile width.
            float offset = -cameraX * layer.ScrollFactor;
            float wrapped = offset % width;
            if (wrapped > 0) wrapped -= width;

            // One extra copy past the right edge covers the seam.
            for (float x = wrapped; x < screenWidth; x += width)
            {
                spriteBatch.Draw(layer.Texture,
                    new Rectangle((int)MathF.Round(x), top, width, height),
                    layer.Source,
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
