using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreeperGame;

/// <summary>
/// A single rigid segment in a skeleton.
///
/// Bones form a tree: each one is positioned and rotated relative to its parent,
/// so rotating a shoulder carries the whole arm with it. That is what makes real
/// animation possible without drawing a single frame by hand.
///
/// Angles are in radians measured from the +X axis, and because screen space has
/// Y growing downwards a positive angle turns clockwise on screen. Straight up is
/// therefore -PI/2.
/// </summary>
public class Bone
{
    public string Name { get; }
    public Bone? Parent { get; }

    /// <summary>Offset from the attachment point, expressed in the parent's frame.</summary>
    public Vector2 LocalOffset { get; set; }

    /// <summary>Rotation relative to the parent, in radians.</summary>
    public float LocalAngle { get; set; }

    /// <summary>The pose this bone returns to when nothing is driving it.</summary>
    public float RestAngle { get; set; }

    public float Length { get; set; }

    /// <summary>Width at the origin end.</summary>
    public float Thickness { get; set; }

    /// <summary>Width at the tip, allowing tapered limbs and feathers.</summary>
    public float TipThickness { get; set; }

    public Color Colour { get; set; } = Color.White;

    /// <summary>Optional lighter edge painted along the top of the bone.</summary>
    public Color? Highlight { get; set; }

    /// <summary>Lower numbers are drawn first, so they sit behind.</summary>
    public int Depth { get; set; }

    public bool Visible { get; set; } = true;

    /// <summary>When true the bone hangs off the parent's tip rather than its origin.</summary>
    public bool AttachToTip { get; set; } = true;

    // Resolved each frame.
    public Vector2 WorldOrigin { get; private set; }
    public float WorldAngle { get; private set; }

    public Vector2 WorldTip =>
        WorldOrigin + new Vector2(MathF.Cos(WorldAngle), MathF.Sin(WorldAngle)) * Length;

    public Bone(string name, Bone? parent)
    {
        Name = name;
        Parent = parent;
    }

    /// <summary>Resolves this bone's world transform from its parent's.</summary>
    public void Resolve(Vector2 rootOrigin, float rootAngle)
    {
        if (Parent == null)
        {
            WorldAngle = rootAngle + LocalAngle;
            WorldOrigin = rootOrigin + Rotate(LocalOffset, rootAngle);
            return;
        }

        WorldAngle = Parent.WorldAngle + LocalAngle;

        Vector2 anchor = AttachToTip ? Parent.WorldTip : Parent.WorldOrigin;
        WorldOrigin = anchor + Rotate(LocalOffset, Parent.WorldAngle);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}

/// <summary>
/// Holds a bone hierarchy and draws it with a single white pixel texture.
///
/// Every limb is a quad stretched along its bone, so the whole character is
/// vector-ish geometry rather than a bitmap. That means it can be posed, scaled
/// and recoloured freely, which is exactly what a character that has to act in
/// cutscenes and combat needs.
/// </summary>
public class Skeleton : IDisposable
{
    private readonly Texture2D _pixel;
    private readonly List<Bone> _bones = new List<Bone>();
    private Bone[] _drawOrder = Array.Empty<Bone>();
    private bool _orderDirty = true;

    /// <summary>Segments used to draw a tapered bone. More is smoother, and costlier.</summary>
    private const int TaperSegments = 5;

    public Skeleton(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public Bone Add(string name, Bone? parent, float length, float thickness, Color colour,
        int depth, float restAngle = 0f, Vector2 offset = default, float? tipThickness = null,
        bool attachToTip = true)
    {
        var bone = new Bone(name, parent)
        {
            Length = length,
            Thickness = thickness,
            TipThickness = tipThickness ?? thickness,
            Colour = colour,
            Depth = depth,
            RestAngle = restAngle,
            LocalAngle = restAngle,
            LocalOffset = offset,
            AttachToTip = attachToTip
        };

        _bones.Add(bone);
        _orderDirty = true;
        return bone;
    }

    public Bone this[string name] => _bones.First(b => b.Name == name);

    /// <summary>Recomputes every world transform. Parents are always resolved first.</summary>
    public void Resolve(Vector2 rootOrigin, float rootAngle = 0f)
    {
        // Bones are added parent-before-child, so a single pass is enough.
        foreach (Bone bone in _bones)
        {
            bone.Resolve(rootOrigin, rootAngle);
        }
    }

    /// <summary>
    /// Draws the skeleton. <paramref name="mirror"/> flips it horizontally about
    /// <paramref name="mirrorAxisX"/>, which is how the character faces left.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, float scale, bool mirror, float mirrorAxisX,
        Color tint, float alpha = 1f)
    {
        if (_orderDirty)
        {
            // Stable sort keeps insertion order within the same depth.
            _drawOrder = _bones.OrderBy(b => b.Depth).ToArray();
            _orderDirty = false;
        }

        foreach (Bone bone in _drawOrder)
        {
            if (!bone.Visible || bone.Length <= 0f) continue;
            DrawBone(spriteBatch, bone, scale, mirror, mirrorAxisX, tint, alpha);
        }
    }

    private void DrawBone(SpriteBatch spriteBatch, Bone bone, float scale, bool mirror,
        float mirrorAxisX, Color tint, float alpha)
    {
        Vector2 origin = bone.WorldOrigin;
        float angle = bone.WorldAngle;

        if (mirror)
        {
            // Reflecting across a vertical line negates X and turns the angle
            // about the vertical axis.
            origin.X = 2f * mirrorAxisX - origin.X;
            angle = MathF.PI - angle;
        }

        Color colour = Multiply(bone.Colour, tint) * alpha;

        bool tapered = MathF.Abs(bone.Thickness - bone.TipThickness) > 0.01f;

        if (!tapered)
        {
            DrawQuad(spriteBatch, origin, angle, bone.Length * scale,
                bone.Thickness * scale, colour);
        }
        else
        {
            // Approximate the taper with a short run of narrowing quads.
            float step = bone.Length * scale / TaperSegments;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            for (int i = 0; i < TaperSegments; i++)
            {
                float t = (i + 0.5f) / TaperSegments;
                float width = MathHelper.Lerp(bone.Thickness, bone.TipThickness, t) * scale;

                // Overlap slightly so no gaps open up between segments.
                DrawQuad(spriteBatch, origin + direction * (i * step), angle,
                    step + 1f, width, colour);
            }
        }

        if (bone.Highlight.HasValue)
        {
            // A thin bright strip along the upper edge suggests a light source
            // without needing any real shading.
            float inset = bone.Thickness * scale * 0.30f;
            var normal = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));

            DrawQuad(spriteBatch, origin - normal * inset, angle,
                bone.Length * scale, MathF.Max(1f, bone.Thickness * scale * 0.28f),
                Multiply(bone.Highlight.Value, tint) * alpha);
        }
    }

    /// <summary>Draws one rotated, centred rectangle from the 1x1 texture.</summary>
    private void DrawQuad(SpriteBatch spriteBatch, Vector2 origin, float angle,
        float length, float width, Color colour)
    {
        spriteBatch.Draw(_pixel, origin, null, colour, angle,
            new Vector2(0f, 0.5f), new Vector2(length, MathF.Max(1f, width)),
            SpriteEffects.None, 0f);
    }

    private static Color Multiply(Color a, Color b) => new Color(
        a.R * b.R / 255,
        a.G * b.G / 255,
        a.B * b.B / 255,
        a.A * b.A / 255);

    public void Dispose()
    {
        _pixel?.Dispose();
    }
}
