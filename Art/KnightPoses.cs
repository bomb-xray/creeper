using System;

namespace CreeperGame.Art;

/// <summary>
/// The pose library. Cyclic actions are functions of phase so frame counts can
/// be changed by editing one number; one-off actions are explicit key poses.
/// </summary>
public static class KnightPoses
{
    public const int IdleFrames = 6;
    public const int WalkFrames = 12;

    public const int IdleStart = 0;
    public const int WalkStart = IdleFrames;
    public const int CrouchFrame = IdleFrames + WalkFrames;
    public const int JumpFrame = CrouchFrame + 1;
    public const int FallFrame = CrouchFrame + 2;
    public const int DashFrame = CrouchFrame + 3;

    public const int TotalFrames = DashFrame + 1;

    /// <summary>
    /// Standing. Weight on the near leg, sword point down and resting. The whole
    /// figure barely moves: stillness in idle makes the walk read as faster.
    /// </summary>
    public static Pose Idle(float phase)
    {
        float a = phase * MathF.PI * 2f;
        float breathe = MathF.Sin(a);

        // The head and the sword settle a beat behind the chest, so the whole
        // figure does not pulse as one rigid block.
        float lag = MathF.Sin(a - 0.7f);

        return new Pose
        {
            HipNear = 5,
            KneeNear = -4,
            AnkleNear = 0,

            HipFar = -8,
            KneeFar = -8,
            AnkleFar = 2,

            // Sword arm hangs, blade angled forward and down.
            ShoulderNear = 14 + breathe * 1.5f,
            ElbowNear = 16,

            ShoulderFar = -8 - breathe,
            ElbowFar = 14,

            Lean = 1,
            BodyY = -breathe * 0.6f,
            BodyX = 0,
            HeadTilt = lag * 1.2f,

            CapeSway = 0.12f + breathe * 0.04f,
            WingOpen = 0.05f + breathe * 0.04f,
            SwordAngle = 12 + lag * 2.5f
        };
    }

    /// <summary>
    /// Walking. Phase 0 is the near-foot contact; the far leg runs half a cycle
    /// behind.
    ///
    /// The previous cycle used max(0, -cos) to drive the knees, which has a hard
    /// corner where it meets zero: the leg snapped straight at exactly the same
    /// instant every stride, and that discontinuity is what made the walk look
    /// stiff. Every curve here is smooth across the whole loop instead, and the
    /// phases are offset so the joints do not all peak together -- a knee lags
    /// its hip, an ankle lags its knee. That lag is most of what reads as weight.
    /// </summary>
    public static Pose Walk(float phase)
    {
        float a = phase * MathF.PI * 2f;

        // Hip swing. A touch of second harmonic makes the forward reach quicker
        // than the backward push, which is how a real stride is shaped.
        float hipNear = MathF.Sin(a) + MathF.Sin(a * 2f) * 0.12f;
        float hipFar = MathF.Sin(a + MathF.PI) + MathF.Sin((a + MathF.PI) * 2f) * 0.12f;

        // Knee bend, smooth everywhere. Peaks a little after the hip swings back,
        // so the shin trails the thigh instead of moving with it.
        float kneeNear = Fold(a - 0.55f);
        float kneeFar = Fold(a + MathF.PI - 0.55f);

        // Ankle, lagging the knee again.
        float ankleNear = Fold(a - 1.1f);
        float ankleFar = Fold(a + MathF.PI - 1.1f);

        return new Pose
        {
            HipNear = hipNear * 26f,
            KneeNear = -(kneeNear * 52f + 5f),
            AnkleNear = ankleNear * 22f - hipNear * 5f,

            HipFar = hipFar * 26f,
            KneeFar = -(kneeFar * 52f + 5f),
            AnkleFar = ankleFar * 22f - hipFar * 5f,

            // The sword arm is heavy: it barely swings, it just rides the body.
            ShoulderNear = 14f - hipNear * 5f,
            ElbowNear = 16f + kneeNear * 5f,

            // The free arm counter-swings against its own leg.
            ShoulderFar = -8f + hipFar * 20f,
            ElbowFar = 14f + kneeFar * 14f,

            Lean = 4f,

            // The body rises over each supporting leg and dips at the pass, so
            // it bobs twice per cycle. Cosine of double the phase gives that
            // without the kink an absolute value would introduce.
            BodyY = -0.9f - MathF.Cos(a * 2f) * 1.4f,
            BodyX = 0f,

            // The head counters the body, which keeps it level -- people do this
            // instinctively and its absence is very noticeable.
            HeadTilt = -hipNear * 1.6f + MathF.Cos(a * 2f) * 0.8f,

            CapeSway = 0.5f + kneeNear * 0.12f,
            WingOpen = 0.1f + kneeNear * 0.04f,
            SwordAngle = 12f - hipNear * 3f - kneeNear * 4f
        };
    }

    /// <summary>
    /// A smooth 0..1 bump peaking once per cycle, used for joints that fold.
    ///
    /// Unlike max(0, -cos) this has no corner, so the joint eases into and out of
    /// its bend rather than snapping straight.
    /// </summary>
    private static float Fold(float angle)
    {
        // (1 - cos) / 2 is a raised cosine: smooth, and exactly zero at its
        // minimum. Squaring biases the bend towards the lift, which is where a
        // real knee spends its travel.
        float raised = (1f - MathF.Cos(angle)) * 0.5f;
        return raised * raised;
    }

    public static Pose Crouch() => new Pose
    {
        HipNear = -34, KneeNear = -78, AnkleNear = 40,
        HipFar = -24, KneeFar = -72, AnkleFar = 38,
        ShoulderNear = 28, ElbowNear = 22,
        ShoulderFar = 14, ElbowFar = 30,
        Lean = 12, BodyY = 13, BodyX = -1, HeadTilt = -4,
        CapeSway = 0.2f, WingOpen = 0f, SwordAngle = 26
    };

    public static Pose Jump() => new Pose
    {
        HipNear = -28, KneeNear = -58, AnkleNear = 30,
        HipFar = 20, KneeFar = -18, AnkleFar = 6,
        ShoulderNear = -8, ElbowNear = 10,
        ShoulderFar = 34, ElbowFar = 12,
        Lean = -4, BodyY = -2, BodyX = 0, HeadTilt = 3,
        CapeSway = 0.9f, WingOpen = 1f, SwordAngle = -6
    };

    public static Pose Fall() => new Pose
    {
        HipNear = 22, KneeNear = -14, AnkleNear = 4,
        HipFar = -18, KneeFar = -40, AnkleFar = 22,
        ShoulderNear = 18, ElbowNear = 18,
        ShoulderFar = -28, ElbowFar = 18,
        Lean = 5, BodyY = 0, BodyX = 0, HeadTilt = -3,
        CapeSway = 0.75f, WingOpen = 0.6f, SwordAngle = 16
    };

    /// <summary>A low committed lunge, cape streaming flat behind.</summary>
    public static Pose Dash() => new Pose
    {
        HipNear = 42, KneeNear = -10, AnkleNear = 2,
        HipFar = -44, KneeFar = -60, AnkleFar = 34,
        ShoulderNear = -18, ElbowNear = 8,
        ShoulderFar = 40, ElbowFar = 14,
        Lean = 18, BodyY = 8, BodyX = 3, HeadTilt = -6,
        CapeSway = 1.7f, WingOpen = 0.5f, SwordAngle = -14
    };

    public static Pose ForFrame(int index)
    {
        if (index < WalkStart) return Idle(index / (float)IdleFrames);
        if (index < CrouchFrame) return Walk((index - WalkStart) / (float)WalkFrames);
        if (index == CrouchFrame) return Crouch();
        if (index == JumpFrame) return Jump();
        if (index == FallFrame) return Fall();
        return Dash();
    }

    public static string FrameName(int index)
    {
        if (index < WalkStart) return $"IDLE {index}";
        if (index < CrouchFrame) return $"WALK {index - WalkStart}";
        if (index == CrouchFrame) return "CROUCH";
        if (index == JumpFrame) return "JUMP";
        if (index == FallFrame) return "FALL";
        return "DASH";
    }
}
