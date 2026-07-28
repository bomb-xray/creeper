using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CreeperGame.Art;

/// <summary>
/// Assembles the knight from hand-drawn parts plus procedurally drawn limbs.
///
/// The split is deliberate. Distinctive shapes -- the helm, breastplate, wing,
/// cape, sword -- are hand-authored pixel art, because those are what make the
/// character recognisable and no formula produces them. Arms and legs are drawn
/// procedurally, because they are simple tapered tubes that must bend to
/// arbitrary angles, and hand-drawing them would mean redrawing every limb for
/// every frame of every action.
///
/// That combination is what makes dozens of animations affordable: a new action
/// costs a handful of joint angles and no new artwork at all.
/// </summary>
public static class KnightFigure
{
    public const int CanvasWidth = 140;
    public const int CanvasHeight = 136;
    public const int GroundY = 126;
    private const int CentreX = 70;

    /// <summary>
    /// Height of the figure in pixels, heel to plume tip. Used to derive the
    /// whole-number draw scale, so it must match what the layout actually
    /// produces: ground 126 minus the plume top at 49.
    /// </summary>
    public const int FigureHeight = 77;

    // Limb lengths in pixels.
    private const int ThighLength = 17;
    private const int ShinLength = 17;
    private const int UpperArmLength = 14;
    private const int ForearmLength = 13;

    // Palette shortcuts for the procedural limbs, matching the drawn parts.
    private static Color Steel1 => KnightArt.Palette['1'];
    private static Color Steel2 => KnightArt.Palette['2'];
    private static Color Steel3 => KnightArt.Palette['3'];
    private static Color Steel4 => KnightArt.Palette['4'];
    private static Color Steel5 => KnightArt.Palette['5'];
    private static Color Outline => KnightArt.Palette['K'];
    private static Color Leather => KnightArt.Palette['r'];

    private static float Rad(float degrees) => degrees * MathF.PI / 180f;

    private static Point Project(Point origin, float angleDegrees, int length)
    {
        float r = Rad(angleDegrees);
        return new Point(
            origin.X + (int)MathF.Round(MathF.Sin(r) * length),
            origin.Y + (int)MathF.Round(MathF.Cos(r) * length));
    }

    /// <summary>Draws one pose and returns the finished canvas.</summary>
    public static PixelCanvas Build(Pose pose)
    {
        var c = new PixelCanvas(CanvasWidth, CanvasHeight);

        int hipX = CentreX + (int)MathF.Round(pose.BodyX);
        int hipY = GroundY - 36 + (int)MathF.Round(pose.BodyY);

        int lean = (int)MathF.Round(pose.Lean * 0.3f);
        var hip = new Point(hipX, hipY);
        var shoulder = new Point(hipX + lean, hipY - 22);

        // Back to front.
        DrawWing(c, shoulder, pose);
        DrawCape(c, shoulder, pose);
        DrawLimb(c, hip, pose.HipFar, pose.KneeFar, pose.AnkleFar, -2, false, true, out _);
        DrawLimb(c, shoulder, pose.ShoulderFar, pose.ElbowFar, 0, -3, false, false, out _);
        DrawTorso(c, hip, shoulder, pose);
        DrawHead(c, shoulder, pose);
        DrawLimb(c, hip, pose.HipNear, pose.KneeNear, pose.AnkleNear, 2, true, true, out _);
        DrawLimb(c, shoulder, pose.ShoulderNear, pose.ElbowNear, 0, 2, true, false, out Point hand);
        DrawPauldron(c, shoulder);
        DrawSword(c, hand, pose);

        c.Outline(Outline);
        return c;
    }

    /// <summary>Stamps a hand-drawn part with its top-left at the given point.</summary>
    private static void Stamp(PixelCanvas c, string[] art, int x, int y)
    {
        for (int row = 0; row < art.Length; row++)
        {
            string line = art[row];
            for (int col = 0; col < line.Length; col++)
            {
                char ch = line[col];
                if (ch == '.' || ch == ' ') continue;
                if (!KnightArt.Palette.TryGetValue(ch, out Color colour)) continue;

                c.Plot(x + col, y + row, colour);
            }
        }
    }

    /// <summary>Width of a hand-drawn part.</summary>
    private static int ArtWidth(string[] art)
    {
        int w = 0;
        foreach (string row in art)
        {
            if (row.Length > w) w = row.Length;
        }
        return w;
    }

    // =================================================================== limbs

    /// <summary>
    /// A two-segment limb drawn as stacked horizontal runs, with a lit strip
    /// down one side and a shadow down the other.
    ///
    /// Shading a tube is the one case where a rule beats hand-drawing: the lit
    /// side is always the leading edge, so it can be placed by offsetting the
    /// run rather than by redrawing the limb for every angle.
    /// </summary>
    private static void DrawLimb(PixelCanvas c, Point root, float upperAngle,
        float lowerBend, float endAngle, int offsetX, bool near, bool isLeg,
        out Point tip)
    {
        int upperLength = isLeg ? ThighLength : UpperArmLength;
        int lowerLength = isLeg ? ShinLength : ForearmLength;

        int wTop = isLeg ? (near ? 9 : 8) : (near ? 8 : 7);
        int wMid = isLeg ? (near ? 7 : 6) : (near ? 6 : 5);
        int wEnd = isLeg ? (near ? 6 : 5) : (near ? 5 : 4);

        // The far side of the body sits in shadow, one ramp step darker.
        Color body = near ? Steel3 : Steel2;
        Color lit = near ? Steel5 : Steel3;
        Color dark = near ? Steel2 : Steel1;

        var origin = new Point(root.X + offsetX, root.Y + (isLeg ? 2 : 2));
        Point joint = Project(origin, upperAngle, upperLength);
        tip = Project(joint, upperAngle + lowerBend, lowerLength);

        Segment(c, origin, joint, wTop, wMid, body, lit, dark);
        Segment(c, joint, tip, wMid, wEnd, body, lit, dark);

        // Joint cop: a small disc so the knee or elbow does not pinch.
        int copR = isLeg ? (near ? 4 : 3) : (near ? 3 : 3);
        c.Ellipse(joint.X, joint.Y, copR, copR, near ? Steel4 : Steel3);
        c.HLine(joint.X - copR + 1, joint.X + copR - 2, joint.Y - copR + 1, lit);

        if (isLeg)
        {
            // Sabaton: a wedge pointing the way the knight faces.
            float footAngle = upperAngle + lowerBend + endAngle;
            Point toe = Project(tip, footAngle + 90f, near ? 7 : 6);

            c.Polygon(near ? Steel2 : Steel1,
                new Point(tip.X - 3, tip.Y - 2),
                new Point(toe.X + 1, tip.Y - 1),
                new Point(toe.X + 1, tip.Y + 2),
                new Point(tip.X - 3, tip.Y + 2));

            c.HLine(tip.X - 2, toe.X, tip.Y - 2, near ? Steel3 : Steel2);
        }
        else
        {
            // Gauntlet.
            c.Ellipse(tip.X, tip.Y, near ? 4 : 3, near ? 4 : 3, near ? Steel4 : Steel3);
            c.HLine(tip.X - 2, tip.X + 1, tip.Y - 2, lit);
        }
    }

    /// <summary>One tapered segment with its lit and shadowed edges.</summary>
    private static void Segment(PixelCanvas c, Point a, Point b, int w1, int w2,
        Color body, Color lit, Color dark)
    {
        int steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        if (steps == 0) steps = 1;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int cx = (int)MathF.Round(a.X + (b.X - a.X) * t);
            int cy = (int)MathF.Round(a.Y + (b.Y - a.Y) * t);
            int w = Math.Max(2, (int)MathF.Round(MathHelper.Lerp(w1, w2, t)));

            int left = cx - w / 2;
            int right = left + w - 1;

            c.HLine(left, right, cy, body);

            // Light comes from the upper left, so the left edge catches it.
            c.Plot(left, cy, dark);
            c.Plot(left + 1, cy, lit);
            c.Plot(right, cy, dark);
        }
    }

    // =================================================================== torso

    private static void DrawTorso(PixelCanvas c, Point hip, Point shoulder, Pose pose)
    {
        int w = ArtWidth(KnightArt.Torso);
        Stamp(c, KnightArt.Torso, shoulder.X - w / 2, shoulder.Y - 2);
    }

    // ==================================================================== head

    private static void DrawHead(PixelCanvas c, Point shoulder, Pose pose)
    {
        int tilt = (int)MathF.Round(pose.HeadTilt * 0.3f);

        // Helm sits on the shoulders; the plume overlaps its crown.
        int helmX = shoulder.X - 10 + tilt;
        int helmY = shoulder.Y - 18;

        Stamp(c, KnightArt.Plume, helmX - 7, helmY - 1);
        Stamp(c, KnightArt.Helmet, helmX, helmY);
    }

    private static void DrawPauldron(PixelCanvas c, Point shoulder)
    {
        int w = ArtWidth(KnightArt.Pauldron);
        Stamp(c, KnightArt.Pauldron, shoulder.X - w / 2 + 2, shoulder.Y - 1);
    }

    // ==================================================================== wing

    private static void DrawWing(PixelCanvas c, Point shoulder, Pose pose)
    {
        string[] art =
            pose.WingOpen > 0.66f ? KnightArt.WingSpread :
            pose.WingOpen > 0.25f ? KnightArt.WingOpen :
            KnightArt.WingFolded;

        int w = ArtWidth(art);

        // Anchored at the shoulder blade. The art is drawn with its root at the
        // lower right, so the stamp is offset left by almost the full width.
        Stamp(c, art, shoulder.X - w + 8, shoulder.Y - 20);
    }

    // ==================================================================== cape

    private static void DrawCape(PixelCanvas c, Point shoulder, Pose pose)
    {
        string[] art =
            pose.CapeSway > 0.9f ? KnightArt.CapeStream :
            pose.CapeSway > 0.3f ? KnightArt.CapeDrift :
            KnightArt.CapeRest;

        int w = ArtWidth(art);

        // Hangs from the collar. Offset back so the torso covers its front edge
        // and it reads as being behind the body rather than beside it.
        Stamp(c, art, shoulder.X - w + 7, shoulder.Y - 2);
    }

    // =================================================================== sword

    private static void DrawSword(PixelCanvas c, Point hand, Pose pose)
    {
        string[] art = KnightArt.Sword;
        int w = ArtWidth(art);

        // The grip sits four rows up from the bottom of the sprite, so the art
        // is offset to put that on the fist.
        const int gripRow = 36;

        Stamp(c, art, hand.X - w / 2, hand.Y - gripRow);
    }
}
