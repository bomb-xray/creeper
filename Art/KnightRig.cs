using Microsoft.Xna.Framework;
using System;

namespace CreeperGame.Art;

/// <summary>
/// A complete set of joint angles and offsets describing one pose.
///
/// Angles are degrees measured clockwise from straight down, so 0 is a limb
/// hanging vertically and positive values swing it forward (to the right, the
/// direction the knight faces).
/// </summary>
public struct KnightPose
{
    public float HipNear, KneeNear, AnkleNear;
    public float HipFar, KneeFar, AnkleFar;
    public float ShoulderNear, ElbowNear;
    public float ShoulderFar, ElbowFar;

    /// <summary>Forward tilt of the whole upper body, in degrees.</summary>
    public float Lean;

    /// <summary>Vertical offset of the hips. Negative lifts the figure.</summary>
    public float BodyY;

    /// <summary>Horizontal offset of the hips, for lunges.</summary>
    public float BodyX;

    public float HeadTilt;

    /// <summary>0 hangs straight down, 1 streams fully back.</summary>
    public float CapeSway;

    /// <summary>0 folded against the back, 1 fully spread.</summary>
    public float WingOpen;

    /// <summary>Blade angle relative to the forearm.</summary>
    public float SwordAngle;

    public static KnightPose Default => new KnightPose
    {
        HipNear = 4, KneeNear = 5, AnkleNear = 0,
        HipFar = -6, KneeFar = 7, AnkleFar = 0,
        ShoulderNear = 22, ElbowNear = -38,
        ShoulderFar = -4, ElbowFar = 12,
        Lean = 0, BodyY = 0, BodyX = 0, HeadTilt = 0,
        CapeSway = 0.12f, WingOpen = 0.04f, SwordAngle = -6
    };

    /// <summary>Blends two poses, used to smooth transitions between frames.</summary>
    public static KnightPose Lerp(KnightPose a, KnightPose b, float t) => new KnightPose
    {
        HipNear = MathHelper.Lerp(a.HipNear, b.HipNear, t),
        KneeNear = MathHelper.Lerp(a.KneeNear, b.KneeNear, t),
        AnkleNear = MathHelper.Lerp(a.AnkleNear, b.AnkleNear, t),
        HipFar = MathHelper.Lerp(a.HipFar, b.HipFar, t),
        KneeFar = MathHelper.Lerp(a.KneeFar, b.KneeFar, t),
        AnkleFar = MathHelper.Lerp(a.AnkleFar, b.AnkleFar, t),
        ShoulderNear = MathHelper.Lerp(a.ShoulderNear, b.ShoulderNear, t),
        ElbowNear = MathHelper.Lerp(a.ElbowNear, b.ElbowNear, t),
        ShoulderFar = MathHelper.Lerp(a.ShoulderFar, b.ShoulderFar, t),
        ElbowFar = MathHelper.Lerp(a.ElbowFar, b.ElbowFar, t),
        Lean = MathHelper.Lerp(a.Lean, b.Lean, t),
        BodyY = MathHelper.Lerp(a.BodyY, b.BodyY, t),
        BodyX = MathHelper.Lerp(a.BodyX, b.BodyX, t),
        HeadTilt = MathHelper.Lerp(a.HeadTilt, b.HeadTilt, t),
        CapeSway = MathHelper.Lerp(a.CapeSway, b.CapeSway, t),
        WingOpen = MathHelper.Lerp(a.WingOpen, b.WingOpen, t),
        SwordAngle = MathHelper.Lerp(a.SwordAngle, b.SwordAngle, t)
    };
}

/// <summary>
/// Builds the winged knight as shaded geometry, drawn strictly in profile.
///
/// Profile is the whole point and the thing the earlier attempt got wrong: seen
/// from the side a human is narrow. The torso is about half as wide as it is
/// from the front, only one pauldron is visible in full (the far one peeks
/// behind the neck), and the two legs overlap rather than sitting side by side.
/// Every measurement below is chosen to preserve that read.
///
/// The figure is authored facing right at roughly 100 px heel to helm.
/// </summary>
public static class KnightRig
{
    // ---- materials -------------------------------------------------------
    // Ramps run darkest to brightest. Steel is cool and desaturated so the red
    // of the cape and plume stays the only saturated thing in the silhouette.

    public static readonly Material Steel = new Material("steel",
        (20, 22, 30), (38, 43, 56), (60, 68, 86), (88, 97, 118),
        (120, 130, 152), (155, 165, 188), (194, 203, 221), (232, 238, 248));

    public static readonly Material DarkSteel = new Material("darksteel",
        (14, 15, 21), (26, 30, 40), (42, 48, 62), (60, 68, 86),
        (80, 89, 110), (102, 112, 134), (126, 136, 160), (152, 162, 186));

    public static readonly Material Cape = new Material("cape",
        (28, 6, 12), (50, 10, 18), (74, 14, 22), (100, 20, 30),
        (128, 27, 37), (158, 38, 46), (188, 55, 60), (214, 82, 84));

    public static readonly Material Cloth = new Material("cloth",
        (10, 9, 13), (18, 17, 23), (28, 27, 35), (40, 38, 48),
        (54, 52, 64), (70, 68, 82), (88, 86, 102), (108, 106, 124));

    public static readonly Material Feather = new Material("feather",
        (78, 84, 104), (108, 116, 138), (138, 147, 170), (168, 177, 200),
        (196, 204, 224), (218, 225, 240), (236, 241, 250), (252, 253, 255));

    public static readonly Material Gold = new Material("gold",
        (46, 30, 8), (76, 52, 14), (110, 78, 22), (146, 108, 32),
        (182, 142, 48), (210, 174, 74), (232, 205, 118), (250, 232, 172));

    public static readonly Material Blade = new Material("blade",
        (24, 26, 34), (50, 54, 66), (80, 86, 102), (114, 121, 140),
        (152, 160, 180), (190, 198, 216), (222, 229, 242), (252, 253, 255));

    // ---- canvas ----------------------------------------------------------

    public const int CanvasWidth = 128;
    public const int CanvasHeight = 132;

    /// <summary>Where the feet rest on the canvas.</summary>
    public const int GroundY = 120;

    /// <summary>Horizontal centre the figure is built around.</summary>
    private const float CentreX = 66f;

    // ---- proportions -----------------------------------------------------
    // A profile figure is narrow. These are the numbers that carry that read,
    // so they are named rather than scattered as literals.

    private const float ThighLength = 21f;
    private const float ShinLength = 21f;
    private const float UpperArmLength = 16f;
    private const float ForearmLength = 15f;

    /// <summary>Half-width of the torso in profile. Deliberately slim.</summary>
    private const float TorsoHalfWidth = 7f;

    /// <summary>
    /// Profile depth cues. The far side of the body is not beside the near side,
    /// it is behind it: shifted back a little, never mirrored across the centre.
    /// Getting this wrong is what makes a side view collapse into a front view.
    /// </summary>
    private const float FarLegOffset = -2.5f;
    private const float NearLegOffset = 1.5f;

    /// <summary>Far shoulder sits back and slightly high, mostly hidden.</summary>
    private const float FarArmOffset = -4.5f;
    private const float NearArmOffset = 1.5f;

    // ---- depths ----------------------------------------------------------
    // Back to front. Gaps left between values so parts can be inserted later.

    private const int DepthWing = 10;
    private const int DepthFarLeg = 20;
    private const int DepthFarArm = 25;
    private const int DepthCape = 30;
    private const int DepthTorso = 40;
    private const int DepthSash = 45;
    private const int DepthHead = 50;
    private const int DepthPlume = 48;
    private const int DepthNearLeg = 60;
    private const int DepthNearArm = 70;
    private const int DepthSword = 75;
    private const int DepthGuard = 78;

    private static float Rad(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>
    /// Projects a joint chain. Angles are clockwise from straight down, so
    /// sin drives X (forward) and cos drives Y (down the screen).
    /// </summary>
    private static Vector2 Project(Vector2 origin, float angleDegrees, float length)
    {
        float r = Rad(angleDegrees);
        return new Vector2(origin.X + MathF.Sin(r) * length,
                           origin.Y + MathF.Cos(r) * length);
    }

    /// <summary>Builds every shape for one pose and returns the finished canvas.</summary>
    public static ShadedCanvas Build(KnightPose pose)
    {
        var canvas = new ShadedCanvas(CanvasWidth, CanvasHeight);

        // ---- skeleton anchors ----
        // Leaning rotates the upper body about the hips, so the offsets grow
        // with height up the spine.

        float hipX = CentreX + pose.BodyX;
        float hipY = GroundY - 42f + pose.BodyY;

        float leanShift = pose.Lean * 0.32f;
        var hip = new Vector2(hipX, hipY);
        var waist = new Vector2(hipX + leanShift * 0.4f, hipY - 8f);
        var chest = new Vector2(hipX + leanShift, hipY - 20f);
        var shoulder = new Vector2(hipX + leanShift * 1.2f, hipY - 25f);
        var neck = new Vector2(hipX + leanShift * 1.35f, hipY - 30f);

        BuildWing(canvas, chest, pose);
        BuildLeg(canvas, hip, pose.HipFar, pose.KneeFar, pose.AnkleFar,
                 FarLegOffset, DarkSteel, DepthFarLeg, false);
        BuildArm(canvas, shoulder, pose.ShoulderFar, pose.ElbowFar,
                 FarArmOffset, DarkSteel, DepthFarArm, false, out _);
        BuildCape(canvas, shoulder, pose);
        BuildTorso(canvas, hip, waist, chest, shoulder, pose);
        BuildHead(canvas, neck, pose);

        BuildLeg(canvas, hip, pose.HipNear, pose.KneeNear, pose.AnkleNear,
                 NearLegOffset, Steel, DepthNearLeg, true);
        BuildArm(canvas, shoulder, pose.ShoulderNear, pose.ElbowNear,
                 NearArmOffset, Steel, DepthNearArm, true, out Vector2 hand);

        BuildSword(canvas, hand, pose);

        return canvas;
    }

    // ==================================================================== legs

    private static void BuildLeg(ShadedCanvas canvas, Vector2 hip, float hipAngle,
        float kneeBend, float ankleAngle, float offsetX, Material material,
        int depth, bool near)
    {
        Shape leg = canvas.AddShape(material, depth);

        var origin = new Vector2(hip.X + offsetX, hip.Y);
        Vector2 knee = Project(origin, hipAngle, ThighLength);
        Vector2 ankle = Project(knee, hipAngle + kneeBend, ShinLength);

        // Thigh: heavy at the hip, tapering into the knee.
        float thighTop = near ? 12f : 11f;
        float thighBottom = near ? 9f : 8f;
        leg.Limb(origin.X, origin.Y, knee.X, knee.Y, thighTop, thighBottom);

        // Shin: narrower, tapering to the ankle.
        leg.Limb(knee.X, knee.Y, ankle.X, ankle.Y, near ? 9f : 8f, near ? 6.5f : 6f);

        // Knee cop, the domed plate over the joint.
        leg.Circle(knee.X, knee.Y, near ? 5.2f : 4.6f);

        // Sabaton: a wedge pointing the way the knight faces.
        float footAngle = hipAngle + kneeBend + ankleAngle;
        Vector2 toe = Project(ankle, footAngle + 90f, near ? 10f : 9f);
        Vector2 heel = Project(ankle, footAngle - 90f, 4.5f);

        leg.Polygon(
            new Vector2(heel.X, ankle.Y - 3f),
            new Vector2(toe.X, ankle.Y - 2.5f),
            new Vector2(toe.X, ankle.Y + 3f),
            new Vector2(heel.X, ankle.Y + 3f));

        // Plate banding on the shin reads as articulated armour.
        for (int i = 1; i <= 3; i++)
        {
            float t = i / 4f;
            var band = Vector2.Lerp(knee, ankle, t);
            leg.Shade(band.X - 5f, band.Y - 1f, 10f, 1f, -1.5f);
        }

        // A lit edge down the front of the thigh gives the form volume.
        if (near)
        {
            leg.ShadeLine(origin.X - 3f, origin.Y, knee.X - 3f, knee.Y, 2f, 0.9f);
        }
    }

    // ==================================================================== arms

    private static void BuildArm(ShadedCanvas canvas, Vector2 shoulder, float shoulderAngle,
        float elbowBend, float offsetX, Material material, int depth, bool near,
        out Vector2 hand)
    {
        Shape arm = canvas.AddShape(material, depth);

        var origin = new Vector2(shoulder.X + offsetX, shoulder.Y);
        Vector2 elbow = Project(origin, shoulderAngle, UpperArmLength);
        hand = Project(elbow, shoulderAngle + elbowBend, ForearmLength);

        arm.Limb(origin.X, origin.Y, elbow.X, elbow.Y, near ? 9f : 8f, near ? 7.5f : 6.5f);
        arm.Limb(elbow.X, elbow.Y, hand.X, hand.Y, near ? 7.5f : 6.5f, near ? 6f : 5.5f);

        // Couter over the elbow.
        arm.Circle(elbow.X, elbow.Y, near ? 4.4f : 3.8f);

        // Gauntlet: slightly wider than the forearm so the grip reads.
        arm.Circle(hand.X, hand.Y, near ? 4.6f : 4f);

        // The pauldron belongs to the arm so it swings with the shoulder.
        //
        // In profile only the near pauldron is really seen; the far one is a
        // sliver behind the neck. Drawing them the same size is exactly what
        // turns a side view into a front view, so the far one is deliberately
        // much smaller and pushed back.
        Shape pauldron = canvas.AddShape(material, depth + 1);

        if (near)
        {
            // Seen almost edge-on: taller than it is wide, and swept back over
            // the shoulder rather than sticking out sideways.
            pauldron.Ellipse(origin.X - 0.5f, origin.Y - 1.5f, 6.5f, 7.5f);

            // Second, smaller lame overlapping the first.
            pauldron.Ellipse(origin.X - 2.5f, origin.Y + 2.5f, 5.5f, 4.5f);

            pauldron.Shade(origin.X - 7f, origin.Y + 4f, 13f, 2f, -1.7f);
            pauldron.ShadeLine(origin.X - 5f, origin.Y - 6f,
                               origin.X + 3f, origin.Y - 4f, 2f, 1.2f);
        }
        else
        {
            // Just enough to break the outline behind the neck.
            pauldron.Ellipse(origin.X + 1f, origin.Y - 1f, 4f, 5f);
            pauldron.Shade(origin.X - 3f, origin.Y + 2f, 8f, 2f, -1.4f);
        }
    }

    // =================================================================== torso

    private static void BuildTorso(ShadedCanvas canvas, Vector2 hip, Vector2 waist,
        Vector2 chest, Vector2 shoulder, KnightPose pose)
    {
        Shape torso = canvas.AddShape(Steel, DepthTorso);

        // In profile the breastplate is a narrow shield: it bulges forward at
        // the chest and tucks in at the waist. Front and back edges are handled
        // separately so the curve is asymmetric, the way real plate is.
        float w = TorsoHalfWidth;

        // The two edges are deliberately different curves. The chest bulges
        // forward and the belly tucks in sharply, while the back stays much
        // straighter and only rounds at the shoulder blade. A symmetric outline
        // here is the single biggest thing that flattens a profile into a front
        // view, so the asymmetry is worth the extra points.
        torso.Polygon(
            // Front edge, hip up to collar.
            new Vector2(hip.X + w - 1.5f, hip.Y + 3f),
            new Vector2(waist.X + w - 2f, waist.Y + 1f),     // tucked waist
            new Vector2(chest.X + w + 2.5f, chest.Y + 4f),   // chest bulge
            new Vector2(chest.X + w + 2f, chest.Y - 1f),
            new Vector2(shoulder.X + w - 1f, shoulder.Y - 1f),
            // Back edge, coming down again: straighter, with a shoulder blade.
            new Vector2(shoulder.X - w - 0.5f, shoulder.Y - 1f),
            new Vector2(chest.X - w - 2f, chest.Y + 1f),     // shoulder blade
            new Vector2(waist.X - w - 0.5f, waist.Y + 1f),
            new Vector2(hip.X - w, hip.Y + 3f));

        // Hip plates flare out below the waist.
        torso.Polygon(
            new Vector2(hip.X - w - 1f, hip.Y - 4f),
            new Vector2(hip.X + w + 1f, hip.Y - 4f),
            new Vector2(hip.X + w, hip.Y + 6f),
            new Vector2(hip.X - w, hip.Y + 6f));

        // Neck opening.
        torso.Circle(shoulder.X, shoulder.Y - 2f, 5f);

        // A lit edge down the chest, and a dark crease at the back, which is
        // what makes the profile curve read as a curve.
        torso.ShadeLine(chest.X + w - 1f, chest.Y, waist.X + w - 2f, waist.Y, 2f, 1.2f);
        torso.ShadeLine(chest.X - w, chest.Y, waist.X - w + 1f, waist.Y, 2f, -1.3f);

        // Fluting on the breastplate.
        torso.ShadeLine(chest.X + 1f, chest.Y - 2f, chest.X + 3f, chest.Y + 8f, 1f, -1.0f);

        // ---- sash ----
        Shape sash = canvas.AddShape(Cape, DepthSash);
        sash.Polygon(
            new Vector2(hip.X - w - 1f, hip.Y - 7f),
            new Vector2(hip.X + w + 1f, hip.Y - 7f),
            new Vector2(hip.X + w, hip.Y - 1f),
            new Vector2(hip.X - w, hip.Y - 1f));

        // ---- belt buckle ----
        Shape buckle = canvas.AddShape(Gold, DepthSash + 1);
        buckle.Rect(hip.X - 2.5f, hip.Y - 6f, 5f, 4f);
    }

    // ==================================================================== head

    private static void BuildHead(ShadedCanvas canvas, Vector2 neck, KnightPose pose)
    {
        Shape head = canvas.AddShape(Steel, DepthHead);

        float tilt = pose.HeadTilt * 0.25f;
        float cx = neck.X + tilt;
        float cy = neck.Y - 7f;

        // Seen from the side a helm is longer front-to-back than it is tall,
        // and the skull sits back over the neck while the face juts forward.
        // A circle here would read as a ball, which is the front-view mistake.
        head.Ellipse(cx - 1.5f, cy, 8f, 8.5f);

        // Back of the skull, swept down towards the nape.
        head.Ellipse(cx - 6f, cy + 1.5f, 5f, 6f);

        // The snout: a tapering beak carrying the visor. This single wedge does
        // more than anything else to say "helmet in profile".
        head.Polygon(
            new Vector2(cx + 1f, cy - 5f),
            new Vector2(cx + 9f, cy - 1f),
            new Vector2(cx + 11f, cy + 2f),
            new Vector2(cx + 8f, cy + 5.5f),
            new Vector2(cx + 1f, cy + 7f));

        // Gorget below the jaw.
        head.Ellipse(cx - 0.5f, cy + 9f, 6f, 4f);

        // Visor slit, carved so it is a true hole in the silhouette. It follows
        // the snout, sloping slightly down towards the point.
        head.Carve(cx + 1f, cy - 1f, 5f, 2f);
        head.Carve(cx + 5f, cy - 0.5f, 4f, 2f);

        // Breath holes below the slit.
        head.Carve(cx + 6f, cy + 3f, 1f, 1f);
        head.Carve(cx + 8f, cy + 3f, 1f, 1f);

        // Brow highlight and a shaded cheek.
        head.ShadeLine(cx - 6f, cy - 6f, cx + 5f, cy - 5f, 2f, 1.3f);
        head.Shade(cx - 7f, cy + 2f, 5f, 5f, -1.2f);

        // ---- plume ----
        // Sweeps back from the crown; each blob is smaller than the last so it
        // tapers to a point.
        Shape plume = canvas.AddShape(Cape, DepthPlume);

        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            float px = cx - 1f - t * 17f;
            float py = cy - 10f + t * t * 11f;
            plume.Circle(px, py, 4.2f - t * 2.8f);
        }

        // Crest base clamping the plume to the helm.
        Shape crest = canvas.AddShape(Gold, DepthPlume + 1);
        crest.Ellipse(cx - 1f, cy - 8.5f, 3f, 2f);
    }

    // ==================================================================== cape

    private static void BuildCape(ShadedCanvas canvas, Vector2 shoulder, KnightPose pose)
    {
        Shape cape = canvas.AddShape(Cape, DepthCape);

        // The cloth is a ribbon whose centre line drifts further back the lower
        // it hangs. Quadratic drift means the collar stays put while the hem
        // trails, which is how fabric actually behaves.
        const int segments = 12;
        var back = new Vector2[segments + 1];
        var front = new Vector2[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            float cy = shoulder.Y + 1f + t * 46f;
            float cx = shoulder.X - 4f - t * t * pose.CapeSway * 26f - t * 3f;

            // Narrows towards the hem, with a slight wave for cloth movement.
            float half = 8.5f - t * 3f + MathF.Sin(t * 6f) * 1.1f;

            back[i] = new Vector2(cx - half, cy);
            front[i] = new Vector2(cx + half, cy);
        }

        // Stitch the two edges into one closed outline.
        var outline = new Vector2[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++) outline[i] = back[i];
        for (int i = 0; i <= segments; i++) outline[segments + 1 + i] = front[segments - i];
        cape.Polygon(outline);

        // Vertical folds: a dark crease and a lit ridge, following the drift so
        // they bend with the cloth instead of running straight down.
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float cy = shoulder.Y + 1f + t * 46f;
            float cx = shoulder.X - 4f - t * t * pose.CapeSway * 26f - t * 3f;

            cape.Shade(cx - 5f, cy, 2f, 4f, -1.7f);
            cape.Shade(cx + 1f, cy, 2f, 4f, -1.1f);
            cape.Shade(cx - 2f, cy, 1f, 4f, 0.8f);
        }
    }

    // ==================================================================== wing

    private static void BuildWing(ShadedCanvas canvas, Vector2 chest, KnightPose pose)
    {
        Shape wing = canvas.AddShape(Feather, DepthWing);

        float open = pose.WingOpen;

        // Wing shoulder sits on the shoulder blade, behind and above the chest.
        // Rooting it too far forward makes the wing look strapped to the ribs.
        var root = new Vector2(chest.X - 7f, chest.Y - 9f);

        // Leading edge: folded wings lie back along the spine, opening swings
        // them up and out.
        float span = 20f + open * 22f;
        float rise = 10f + open * 26f;

        var elbow = new Vector2(root.X - span * 0.5f, root.Y - rise * 0.62f);
        var tip = new Vector2(root.X - span, root.Y - rise);

        // Two bones so the leading edge can curve rather than being a straight
        // stick, which is what makes a wing look like a wing.
        wing.Limb(root.X, root.Y, elbow.X, elbow.Y, 8f, 5.5f);
        wing.Limb(elbow.X, elbow.Y, tip.X, tip.Y, 5.5f, 3f);

        // Coverts: the short feathers packing the shoulder.
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            var origin = Vector2.Lerp(root, elbow, t);
            float angle = 250f - t * 20f - open * 18f;
            float length = 9f + t * 4f;

            var end = new Vector2(
                origin.X + MathF.Cos(Rad(angle)) * length,
                origin.Y - MathF.Sin(Rad(angle)) * length);

            wing.Limb(origin.X, origin.Y, end.X, end.Y, 5f, 3f);
        }

        // Primaries: the long feathers that define the shape. Separating them
        // with a dark line is essential, otherwise the quantiser fuses them
        // into one white blob.
        const int primaries = 8;
        for (int i = 0; i < primaries; i++)
        {
            float t = i / (float)(primaries - 1);

            // Origins walk out along the outer half of the leading edge.
            var origin = Vector2.Lerp(elbow, tip, t);

            // Outer feathers are longer and sweep further back.
            float length = (13f + t * 19f) * (0.72f + open * 0.5f);
            float angle = 258f - t * 34f - open * 24f;

            var end = new Vector2(
                origin.X + MathF.Cos(Rad(angle)) * length,
                origin.Y - MathF.Sin(Rad(angle)) * length);

            wing.Limb(origin.X, origin.Y, end.X, end.Y, 4.6f - t * 1.6f, 1.8f);

            // Shadow along the trailing side of each quill.
            var mid = Vector2.Lerp(origin, end, 0.55f);
            wing.Shade(mid.X - 1f, mid.Y, 2f, 2f, -1.9f);

            // Lit edge on the leading side.
            var near = Vector2.Lerp(origin, end, 0.3f);
            wing.Shade(near.X - 1f, near.Y - 1f, 1f, 2f, 0.9f);
        }
    }

    // =================================================================== sword

    private static void BuildSword(ShadedCanvas canvas, Vector2 hand, KnightPose pose)
    {
        Shape blade = canvas.AddShape(Blade, DepthSword);

        float angle = Rad(pose.SwordAngle);
        float dx = MathF.Sin(angle);
        float dy = -MathF.Cos(angle);

        // Blade rises out of the fist. Slightly wider at the base than the tip,
        // with the taper concentrated in the last third.
        var tip = new Vector2(hand.X + dx * 54f, hand.Y + dy * 54f);
        var shoulder = new Vector2(hand.X + dx * 36f, hand.Y + dy * 36f);

        blade.Limb(hand.X, hand.Y, shoulder.X, shoulder.Y, 5.5f, 4.5f);
        blade.Limb(shoulder.X, shoulder.Y, tip.X, tip.Y, 4.5f, 1.5f);

        // Fuller: the groove down the centre of the blade.
        var fullerStart = new Vector2(hand.X + dx * 8f, hand.Y + dy * 8f);
        var fullerEnd = new Vector2(hand.X + dx * 42f, hand.Y + dy * 42f);
        blade.ShadeLine(fullerStart.X, fullerStart.Y, fullerEnd.X, fullerEnd.Y, 1f, -1.4f);

        // Lit edge along the front of the blade.
        blade.ShadeLine(fullerStart.X + 1.5f, fullerStart.Y, fullerEnd.X + 1.5f, fullerEnd.Y,
            1f, 1.0f);

        // ---- crossguard and pommel ----
        Shape guard = canvas.AddShape(Gold, DepthGuard);

        // Perpendicular to the blade.
        float px = MathF.Cos(angle);
        float py = MathF.Sin(angle);

        var guardCentre = new Vector2(hand.X + dx * 3f, hand.Y + dy * 3f);
        guard.Limb(
            guardCentre.X - px * 7f, guardCentre.Y - py * 7f,
            guardCentre.X + px * 7f, guardCentre.Y + py * 7f, 3.2f);

        // Grip below the hand, then the pommel.
        Shape grip = canvas.AddShape(Cloth, DepthGuard - 1);
        grip.Limb(hand.X, hand.Y, hand.X - dx * 7f, hand.Y - dy * 7f, 3.4f);

        guard.Circle(hand.X - dx * 8.5f, hand.Y - dy * 8.5f, 2.8f);
    }
}
