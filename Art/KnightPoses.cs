using Microsoft.Xna.Framework;
using System;

namespace CreeperGame.Art;

/// <summary>
/// The pose library. Cyclic actions are functions of phase so the frame count
/// can be changed by editing one number, and one-off actions are authored as
/// explicit key poses.
///
/// The walk is built from the standard four contact points animators use:
/// contact, down, passing, up. Driving the hips with a sine and the knees with a
/// rectified cosine reproduces that cycle without hand-keying every frame, and
/// keeps the two legs exactly half a cycle apart so the gait never limps.
/// </summary>
public static class KnightPoses
{
    /// <summary>Frames exported for each looping animation.</summary>
    public const int IdleFrames = 6;
    public const int WalkFrames = 10;

    /// <summary>Total frames written to the sheet.</summary>
    public const int TotalFrames = IdleFrames + WalkFrames + 4;

    // Frame indices, shared with the runtime.
    public const int IdleStart = 0;
    public const int WalkStart = IdleFrames;
    public const int CrouchFrame = IdleFrames + WalkFrames;
    public const int JumpFrame = CrouchFrame + 1;
    public const int FallFrame = CrouchFrame + 2;
    public const int DashFrame = CrouchFrame + 3;

    /// <summary>
    /// Standing. Weight is on the near leg, the far leg is relaxed, and the
    /// blade is held upright in front. Breathing drives a slow rise and fall.
    /// </summary>
    public static KnightPose Idle(float phase)
    {
        float a = phase * MathF.PI * 2f;
        float breathe = MathF.Sin(a);

        // A second harmonic keeps the loop from feeling metronomic.
        float settle = MathF.Sin(a * 2f) * 0.35f;

        return new KnightPose
        {
            HipNear = 5f,
            KneeNear = 4f + breathe * 1.2f,
            AnkleNear = 0f,

            HipFar = -9f,
            KneeFar = 9f - breathe * 1f,
            AnkleFar = 2f,

            // Sword arm held forward and slightly out, so the blade reads
            // clear of the torso instead of overlapping it.
            ShoulderNear = 22f + breathe * 2f,
            ElbowNear = -38f - breathe * 1.5f,

            ShoulderFar = -5f - breathe * 1.6f,
            ElbowFar = 14f + settle,

            Lean = 1f,
            BodyY = -breathe * 0.9f,
            BodyX = 0f,
            HeadTilt = breathe * 1.4f,

            CapeSway = 0.14f + breathe * 0.05f,
            WingOpen = 0.06f + breathe * 0.05f,
            SwordAngle = -6f + breathe * 1.8f
        };
    }

    /// <summary>
    /// Walking. Phase 0 is the near-foot contact. The far leg runs half a cycle
    /// behind, and the arms counter-swing against their opposite leg.
    /// </summary>
    public static KnightPose Walk(float phase)
    {
        float a = phase * MathF.PI * 2f;

        // Hip swing: forward at contact, back at push-off.
        float swingNear = MathF.Sin(a);
        float swingFar = MathF.Sin(a + MathF.PI);

        // Knees only bend on the recovery half of the stride, which is what the
        // rectified cosine gives: zero while the foot is planted, rising as it
        // lifts. Without this the legs look like they are wading.
        float liftNear = MathF.Max(0f, -MathF.Cos(a));
        float liftFar = MathF.Max(0f, -MathF.Cos(a + MathF.PI));

        // The body dips twice per cycle, at each push-off.
        float bob = -MathF.Abs(MathF.Sin(a)) * 1.8f;

        return new KnightPose
        {
            HipNear = swingNear * 27f,
            KneeNear = liftNear * 46f + 5f,
            // Toe points down as the leg swings through, flattens on contact.
            AnkleNear = liftNear * 16f - swingNear * 6f,

            HipFar = swingFar * 27f,
            KneeFar = liftFar * 46f + 5f,
            AnkleFar = liftFar * 16f - swingFar * 6f,

            // The sword arm stays composed; it only breathes with the stride.
            ShoulderNear = 22f - swingNear * 8f,
            ElbowNear = -38f + liftNear * 6f,

            // The free arm swings properly, opposite its own leg.
            ShoulderFar = -5f + swingFar * 22f,
            ElbowFar = 14f + liftFar * 16f,

            Lean = 4f,
            BodyY = bob,
            BodyX = 0f,
            HeadTilt = -swingNear * 1.8f,

            CapeSway = 0.55f + liftNear * 0.12f,
            WingOpen = 0.09f,
            SwordAngle = -6f - swingNear * 3f
        };
    }

    /// <summary>Crouched: knees deeply folded, body dropped and tipped forward.</summary>
    public static KnightPose Crouch() => new KnightPose
    {
        HipNear = -32f,
        KneeNear = 78f,
        AnkleNear = -34f,

        HipFar = -22f,
        KneeFar = 72f,
        AnkleFar = -32f,

        ShoulderNear = 24f,
        ElbowNear = -6f,
        ShoulderFar = 12f,
        ElbowFar = 32f,

        Lean = 10f,
        BodyY = 16f,
        BodyX = -1f,
        HeadTilt = -5f,

        CapeSway = 0.22f,
        WingOpen = 0.0f,
        SwordAngle = 10f
    };

    /// <summary>
    /// Rising. Legs trail, chest opens, wings snap wide, blade sweeps up. The
    /// wings being fully open is what sells the lift.
    /// </summary>
    public static KnightPose Jump() => new KnightPose
    {
        HipNear = -28f,
        KneeNear = 58f,
        AnkleNear = -18f,

        HipFar = 20f,
        KneeFar = 18f,
        AnkleFar = 10f,

        ShoulderNear = -16f,
        ElbowNear = -12f,
        ShoulderFar = 36f,
        ElbowFar = 14f,

        Lean = -5f,
        BodyY = -2f,
        BodyX = 0f,
        HeadTilt = 4f,

        CapeSway = 0.95f,
        WingOpen = 1.0f,
        SwordAngle = -20f
    };

    /// <summary>Descending. Legs reach for the ground, wings half-braked.</summary>
    public static KnightPose Fall() => new KnightPose
    {
        HipNear = 22f,
        KneeNear = 14f,
        AnkleNear = 12f,

        HipFar = -18f,
        KneeFar = 38f,
        AnkleFar = -8f,

        ShoulderNear = 16f,
        ElbowNear = -16f,
        ShoulderFar = -30f,
        ElbowFar = 20f,

        Lean = 5f,
        BodyY = 0f,
        BodyX = 0f,
        HeadTilt = -4f,

        CapeSway = 0.78f,
        WingOpen = 0.6f,
        SwordAngle = -4f
    };

    /// <summary>
    /// Dashing: a low committed lunge. The torso drops and drives forward, the
    /// trailing leg stretches right out behind, and the cape streams flat.
    /// </summary>
    public static KnightPose Dash() => new KnightPose
    {
        HipNear = 42f,
        KneeNear = 10f,
        AnkleNear = 14f,

        HipFar = -44f,
        KneeFar = 60f,
        AnkleFar = -20f,

        ShoulderNear = -24f,
        ElbowNear = -4f,
        ShoulderFar = 42f,
        ElbowFar = 16f,

        Lean = 16f,
        BodyY = 9f,
        BodyX = 3f,
        HeadTilt = -7f,

        CapeSway = 1.7f,
        WingOpen = 0.55f,
        SwordAngle = -32f
    };

    /// <summary>Returns the pose for a given sheet frame index.</summary>
    public static KnightPose ForFrame(int index)
    {
        if (index < WalkStart)
        {
            return Idle(index / (float)IdleFrames);
        }

        if (index < CrouchFrame)
        {
            return Walk((index - WalkStart) / (float)WalkFrames);
        }

        if (index == CrouchFrame) return Crouch();
        if (index == JumpFrame) return Jump();
        if (index == FallFrame) return Fall();
        return Dash();
    }
}
