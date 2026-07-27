using Microsoft.Xna.Framework;
using System;

namespace CreeperGame.Art;

/// <summary>
/// Joint angles and offsets for one pose, in degrees measured clockwise from
/// straight down. Zero hangs vertically, positive swings forward (to the right).
/// </summary>
public struct Pose
{
    public float HipNear, KneeNear, AnkleNear;
    public float HipFar, KneeFar, AnkleFar;
    public float ShoulderNear, ElbowNear;
    public float ShoulderFar, ElbowFar;

    public float Lean;
    public float BodyY;
    public float BodyX;
    public float HeadTilt;

    /// <summary>0 hangs straight, 1 streams fully back.</summary>
    public float CapeSway;

    /// <summary>Blade angle relative to the forearm.</summary>
    public float SwordAngle;

    public static Pose Rest => new Pose
    {
        HipNear = 4, KneeNear = 4, AnkleNear = 0,
        HipFar = -7, KneeFar = 7, AnkleFar = 2,
        ShoulderNear = 10, ElbowNear = 14,
        ShoulderFar = -6, ElbowFar = 12,
        Lean = 0, BodyY = 0, BodyX = 0, HeadTilt = 0,
        CapeSway = 0.1f, SwordAngle = 8
    };
}

/// <summary>
/// A hooded penitent knight, drawn in profile with flat shading.
///
/// The design language this is aiming at is built on a few specific things, and
/// each one is a deliberate decision here rather than an accident:
///
///  - A tall, narrow silhouette dominated by a pointed capirote hood, so the
///    character is recognisable purely as a shape.
///  - Very dark clothing against a small number of bright accents, so the eye
///    goes straight to the face opening and the blade.
///  - Flat colour regions with hard edges. No gradients, no soft rims. Light is
///    described by placing a lighter region, not by blending towards one.
///  - A heavy black contour around the whole figure.
///
/// Everything is drawn facing right and mirrored at draw time.
/// </summary>
public static class PenitentRig
{
    // ---- canvas ------------------------------------------------------------

    public const int CanvasWidth = 96;
    public const int CanvasHeight = 112;
    public const int GroundY = 102;
    private const float CentreX = 48f;

    /// <summary>Height of the figure in pixels, heel to hood tip.</summary>
    public const int FigureHeight = 86;

    // ---- palette -----------------------------------------------------------
    // Deliberately tiny. A limited palette is most of what makes pixel art read
    // as cohesive; every extra colour is a chance to muddy the silhouette.

    private static readonly Color Outline = new Color(11, 9, 14);

    // Robe: near-black with two lifts, so folds read without going grey.
    private static readonly Color RobeDark = new Color(26, 23, 33);
    private static readonly Color RobeMid = new Color(41, 37, 51);
    private static readonly Color RobeLit = new Color(58, 53, 71);

    // Cape and hood lining: deep crimson, the one saturated colour.
    private static readonly Color ClothDark = new Color(58, 14, 22);
    private static readonly Color ClothMid = new Color(92, 21, 30);
    private static readonly Color ClothLit = new Color(128, 32, 40);

    // Armour: cold steel, used only on small pieces so it stays an accent.
    private static readonly Color SteelDark = new Color(56, 60, 74);
    private static readonly Color SteelMid = new Color(92, 98, 118);
    private static readonly Color SteelLit = new Color(148, 156, 178);
    private static readonly Color SteelHot = new Color(206, 214, 232);

    // Skin in shadow under the hood: barely visible, which is the point.
    private static readonly Color FaceDark = new Color(48, 34, 32);
    private static readonly Color FaceLit = new Color(96, 72, 64);

    private static readonly Color Gold = new Color(168, 130, 54);

    // ---- proportions -------------------------------------------------------
    // The hood is the signature, so it gets a big share of the total height.

    private const int ThighLength = 15;
    private const int ShinLength = 15;
    private const int UpperArm = 12;
    private const int Forearm = 12;

    private static float Rad(float degrees) => degrees * MathF.PI / 180f;

    private static Point Project(Point origin, float angleDegrees, int length)
    {
        float r = Rad(angleDegrees);
        return new Point(
            origin.X + (int)MathF.Round(MathF.Sin(r) * length),
            origin.Y + (int)MathF.Round(MathF.Cos(r) * length));
    }

    /// <summary>Draws the whole figure for one pose and returns the canvas.</summary>
    public static PixelCanvas Build(Pose pose)
    {
        var c = new PixelCanvas(CanvasWidth, CanvasHeight);

        // ---- skeleton anchors ----

        int hipX = (int)MathF.Round(CentreX + pose.BodyX);
        int hipY = (int)MathF.Round(GroundY - 32 + pose.BodyY);

        float leanShift = pose.Lean * 0.3f;
        var hip = new Point(hipX, hipY);
        var chest = new Point(hipX + (int)MathF.Round(leanShift), hipY - 15);
        var shoulder = new Point(hipX + (int)MathF.Round(leanShift * 1.2f), hipY - 19);
        var neck = new Point(hipX + (int)MathF.Round(leanShift * 1.4f), hipY - 23);

        // Back to front.
        DrawCape(c, shoulder, pose);
        DrawLeg(c, hip, pose.HipFar, pose.KneeFar, pose.AnkleFar, -2, false);
        DrawArm(c, shoulder, pose.ShoulderFar, pose.ElbowFar, -3, false, out _);
        DrawRobe(c, hip, chest, shoulder, pose);
        DrawHood(c, neck, pose);
        DrawLeg(c, hip, pose.HipNear, pose.KneeNear, pose.AnkleNear, 1, true);
        DrawArm(c, shoulder, pose.ShoulderNear, pose.ElbowNear, 1, true, out Point hand);
        DrawSword(c, hand, pose);

        c.Outline(Outline);
        return c;
    }

    // ==================================================================== hood

    /// <summary>
    /// The capirote: a tall cone over the head with a dark face opening. This is
    /// the single most identifying shape, so it is drawn first in importance and
    /// given generous height.
    /// </summary>
    private static void DrawHood(PixelCanvas c, Point neck, Pose pose)
    {
        int tilt = (int)MathF.Round(pose.HeadTilt * 0.3f);
        int cx = neck.X + tilt;

        // Head sits just above the neck; the cone rises well above that.
        int headY = neck.Y - 5;
        int tipY = headY - 20;

        // Cone: narrow at the top, flaring to the shoulders. Drawn as a polygon
        // so the sides are clean diagonals rather than a stack of ellipses.
        c.Polygon(RobeMid,
            new Point(cx + 1, tipY),          // tip, offset forward slightly
            new Point(cx + 7, headY + 2),     // front flare
            new Point(cx + 8, headY + 9),     // shoulder line, front
            new Point(cx - 8, headY + 10),    // shoulder line, back
            new Point(cx - 8, headY + 2));    // back flare

        // Lit face of the cone: a narrow wedge down the front-left, since the
        // light is above and in front. A flat region, not a gradient.
        c.Polygon(RobeLit,
            new Point(cx + 1, tipY + 1),
            new Point(cx + 5, headY + 3),
            new Point(cx + 5, headY + 8),
            new Point(cx + 1, headY + 8));

        // Shadowed back of the cone.
        c.Polygon(RobeDark,
            new Point(cx - 3, tipY + 4),
            new Point(cx - 5, headY + 3),
            new Point(cx - 7, headY + 9),
            new Point(cx - 3, headY + 9));

        // Face opening: a dark void with only a hint of a face. Keeping this
        // almost black is what gives the character its anonymity.
        c.Polygon(FaceDark,
            new Point(cx + 2, headY + 1),
            new Point(cx + 7, headY + 3),
            new Point(cx + 7, headY + 7),
            new Point(cx + 2, headY + 7));

        // A single lit pixel run suggesting a brow, and nothing else.
        c.HLine(cx + 4, cx + 6, headY + 3, FaceLit);

        // Crimson band around the base of the hood, tying it to the cape.
        c.HLine(cx - 8, cx + 8, headY + 10, ClothMid);
        c.HLine(cx - 7, cx + 7, headY + 11, ClothDark);
    }

    // ==================================================================== robe

    /// <summary>
    /// Torso as a robe: narrow at the shoulders, widening to the hem. Drawn as
    /// flat panels with a hard-edged lit side.
    /// </summary>
    private static void DrawRobe(PixelCanvas c, Point hip, Point chest, Point shoulder, Pose pose)
    {
        // Main body of the robe.
        c.Polygon(RobeMid,
            new Point(shoulder.X - 6, shoulder.Y),
            new Point(shoulder.X + 6, shoulder.Y),
            new Point(chest.X + 7, chest.Y + 6),
            new Point(hip.X + 8, hip.Y + 8),
            new Point(hip.X - 8, hip.Y + 8),
            new Point(chest.X - 7, chest.Y + 6));

        // Lit panel down the front. A single flat region with a hard boundary,
        // which is the core of this shading style.
        c.Polygon(RobeLit,
            new Point(chest.X + 2, shoulder.Y + 1),
            new Point(chest.X + 6, chest.Y + 6),
            new Point(hip.X + 7, hip.Y + 7),
            new Point(hip.X + 2, hip.Y + 7));

        // Shadowed panel down the back.
        c.Polygon(RobeDark,
            new Point(chest.X - 4, shoulder.Y + 2),
            new Point(chest.X - 7, chest.Y + 6),
            new Point(hip.X - 7, hip.Y + 7),
            new Point(hip.X - 3, hip.Y + 7));

        // Two hard fold lines. Vertical runs, not blends.
        c.VLine(chest.X, shoulder.Y + 3, hip.Y + 6, RobeDark);
        c.VLine(chest.X + 4, chest.Y + 2, hip.Y + 5, RobeDark);

        // Steel gorget at the collar, one of the few metal accents.
        c.Rect(shoulder.X - 5, shoulder.Y - 1, 11, 3, SteelDark);
        c.HLine(shoulder.X - 4, shoulder.X + 3, shoulder.Y - 1, SteelMid);
        c.HLine(shoulder.X - 1, shoulder.X + 2, shoulder.Y - 1, SteelLit);

        // Crimson sash at the waist.
        c.Rect(hip.X - 8, hip.Y + 1, 16, 3, ClothMid);
        c.HLine(hip.X - 7, hip.X + 2, hip.Y + 1, ClothLit);
        c.HLine(hip.X - 8, hip.X + 7, hip.Y + 3, ClothDark);

        // Small gold buckle: a two-pixel accent, enough to catch the eye.
        c.Rect(hip.X + 1, hip.Y + 1, 2, 2, Gold);
    }

    // ==================================================================== cape

    /// <summary>
    /// The cape hangs from the shoulders to below the knee. Its lower edge is
    /// ragged rather than straight, which reads as worn cloth and breaks up what
    /// would otherwise be a dull rectangle in the silhouette.
    /// </summary>
    private static void DrawCape(PixelCanvas c, Point shoulder, Pose pose)
    {
        const int length = 42;

        for (int i = 0; i <= length; i++)
        {
            float t = i / (float)length;
            int y = shoulder.Y + i;

            // Quadratic drift: the collar stays put while the hem trails back.
            int drift = (int)MathF.Round(t * t * pose.CapeSway * 22f + t * 2f);
            int cx = shoulder.X - 2 - drift;

            // Widens towards the hem, then frays.
            int half = 6 + (int)(t * 5);

            // Ragged edge: a deterministic wobble so it does not shimmer between
            // frames the way random values would.
            int fray = 0;
            if (t > 0.82f)
            {
                int phase = (i * 7) % 5;
                fray = phase < 2 ? 0 : (phase < 4 ? 2 : 4);
                if (i > length - 2) fray += 2;
            }

            int left = cx - half;
            int right = cx + half - fray;
            if (right < left) continue;

            c.HLine(left, right, y, ClothMid);

            // Lit edge along the front, shadow along the back. Hard boundaries,
            // one pixel each, which is all this style needs.
            c.Plot(right, y, ClothLit);
            c.Plot(right - 1, y, ClothLit);
            c.Plot(left, y, ClothDark);
            c.Plot(left + 1, y, ClothDark);

            // A vertical fold running down the middle.
            if (i > 4) c.Plot(cx - 1, y, ClothDark);
        }
    }

    // ==================================================================== legs

    private static void DrawLeg(PixelCanvas c, Point hip, float hipAngle, float kneeBend,
        float ankleAngle, int offsetX, bool near)
    {
        Color cloth = near ? RobeMid : RobeDark;
        Color boot = near ? SteelDark : new Color(30, 28, 36);
        Color bootLit = near ? SteelMid : SteelDark;

        var origin = new Point(hip.X + offsetX, hip.Y + 4);
        Point knee = Project(origin, hipAngle, ThighLength);
        Point ankle = Project(knee, hipAngle + kneeBend, ShinLength);

        // Leg under the robe: cloth down to the boot.
        c.Limb(origin.X, origin.Y, knee.X, knee.Y, near ? 8 : 7, near ? 6 : 5, cloth);
        c.Limb(knee.X, knee.Y, ankle.X, ankle.Y, near ? 6 : 5, near ? 5 : 4, cloth);

        // Boot: a solid dark block, wider than the shin.
        float footAngle = hipAngle + kneeBend + ankleAngle;
        Point toe = Project(ankle, footAngle + 90, near ? 6 : 5);

        c.Limb(ankle.X, ankle.Y - 3, ankle.X, ankle.Y, near ? 7 : 6, near ? 7 : 6, boot);

        c.Polygon(boot,
            new Point(ankle.X - 3, ankle.Y - 2),
            new Point(toe.X + 1, ankle.Y - 1),
            new Point(toe.X + 1, ankle.Y + 2),
            new Point(ankle.X - 3, ankle.Y + 2));

        // A lit strip on top of the boot so it does not merge into the shadow.
        c.HLine(ankle.X - 2, toe.X, ankle.Y - 2, bootLit);
    }

    // ==================================================================== arms

    private static void DrawArm(PixelCanvas c, Point shoulder, float shoulderAngle,
        float elbowBend, int offsetX, bool near, out Point hand)
    {
        Color sleeve = near ? RobeMid : RobeDark;
        Color sleeveLit = near ? RobeLit : RobeMid;

        var origin = new Point(shoulder.X + offsetX, shoulder.Y + 2);
        Point elbow = Project(origin, shoulderAngle, UpperArm);
        hand = Project(elbow, shoulderAngle + elbowBend, Forearm);

        c.Limb(origin.X, origin.Y, elbow.X, elbow.Y, near ? 7 : 6, near ? 6 : 5, sleeve);
        c.Limb(elbow.X, elbow.Y, hand.X, hand.Y, near ? 6 : 5, near ? 5 : 4, sleeve);

        // Lit edge along the upper arm, one pixel wide.
        if (near)
        {
            c.Line(origin.X + 2, origin.Y, elbow.X + 2, elbow.Y, sleeveLit);
        }

        // Gauntlet: a small steel block at the wrist.
        c.Rect(hand.X - 2, hand.Y - 2, 5, 5, near ? SteelMid : SteelDark);
        if (near)
        {
            c.HLine(hand.X - 1, hand.X + 1, hand.Y - 2, SteelLit);
        }
    }

    // =================================================================== sword

    /// <summary>
    /// A large straight sword. Oversized weapons are part of the genre's visual
    /// language, and the blade doubles as a strong vertical in the silhouette.
    /// </summary>
    private static void DrawSword(PixelCanvas c, Point hand, Pose pose)
    {
        float a = Rad(pose.SwordAngle);
        float dx = MathF.Sin(a);
        float dy = -MathF.Cos(a);

        const int bladeLength = 40;

        var tip = new Point(
            hand.X + (int)MathF.Round(dx * bladeLength),
            hand.Y + (int)MathF.Round(dy * bladeLength));

        // Blade body, tapering to the point.
        c.Limb(hand.X, hand.Y, tip.X, tip.Y, 5, 2, SteelMid);

        // Lit edge down one side and a dark edge down the other: two hard runs,
        // which is how flat-shaded metal is read as metal.
        var baseFront = new Point(hand.X + (int)(dx * 4) + 1, hand.Y + (int)(dy * 4));
        var tipFront = new Point(tip.X + 1, tip.Y);
        c.Line(baseFront.X, baseFront.Y, tipFront.X, tipFront.Y, SteelHot);

        var baseBack = new Point(hand.X + (int)(dx * 4) - 2, hand.Y + (int)(dy * 4));
        var tipBack = new Point(tip.X - 1, tip.Y);
        c.Line(baseBack.X, baseBack.Y, tipBack.X, tipBack.Y, SteelDark);

        // Crossguard, perpendicular to the blade.
        float px = MathF.Cos(a), py = MathF.Sin(a);
        var guard = new Point(
            hand.X + (int)MathF.Round(dx * 3),
            hand.Y + (int)MathF.Round(dy * 3));

        c.Line(
            guard.X - (int)MathF.Round(px * 6), guard.Y - (int)MathF.Round(py * 6),
            guard.X + (int)MathF.Round(px * 6), guard.Y + (int)MathF.Round(py * 6),
            SteelDark);

        c.Line(
            guard.X - (int)MathF.Round(px * 6), guard.Y - (int)MathF.Round(py * 6) - 1,
            guard.X + (int)MathF.Round(px * 6), guard.Y + (int)MathF.Round(py * 6) - 1,
            SteelMid);

        // Grip and pommel below the hand.
        var pommel = new Point(
            hand.X - (int)MathF.Round(dx * 6),
            hand.Y - (int)MathF.Round(dy * 6));

        c.Limb(hand.X, hand.Y, pommel.X, pommel.Y, 3, 3, ClothDark);
        c.Rect(pommel.X - 1, pommel.Y - 1, 3, 3, Gold);
    }
}
