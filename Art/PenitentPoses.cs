using System;

namespace CreeperGame.Art;

/// <summary>
/// The pose library. Cyclic actions are functions of phase so frame counts can
/// be changed by editing one number; one-off actions are explicit key poses.
/// </summary>
public static class PenitentPoses
{
    public const int IdleFrames = 4;
    public const int WalkFrames = 8;

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

        return new Pose
        {
            HipNear = 5,
            KneeNear = 4,
            AnkleNear = 0,

            HipFar = -8,
            KneeFar = 8,
            AnkleFar = 2,

            // Sword arm hangs, blade angled forward and down.
            ShoulderNear = 14 + breathe * 1.5f,
            ElbowNear = 16,

            ShoulderFar = -8 - breathe,
            ElbowFar = 14,

            Lean = 1,
            BodyY = -breathe * 0.6f,
            BodyX = 0,
            HeadTilt = breathe,

            CapeSway = 0.12f + breathe * 0.04f,
            SwordAngle = 12 + breathe * 2f
        };
    }

    /// <summary>
    /// Walking. Phase 0 is the near-foot contact; the far leg runs half a cycle
    /// behind. Knees bend only on the recovery half, which is what stops the
    /// gait looking like wading.
    /// </summary>
    public static Pose Walk(float phase)
    {
        float a = phase * MathF.PI * 2f;

        float swingNear = MathF.Sin(a);
        float swingFar = MathF.Sin(a + MathF.PI);

        float liftNear = MathF.Max(0f, -MathF.Cos(a));
        float liftFar = MathF.Max(0f, -MathF.Cos(a + MathF.PI));

        return new Pose
        {
            HipNear = swingNear * 24f,
            KneeNear = liftNear * 42f + 4f,
            AnkleNear = liftNear * 14f - swingNear * 5f,

            HipFar = swingFar * 24f,
            KneeFar = liftFar * 42f + 4f,
            AnkleFar = liftFar * 14f - swingFar * 5f,

            // The sword arm stays heavy and mostly still; the free arm swings.
            ShoulderNear = 14 - swingNear * 6f,
            ElbowNear = 16 + liftNear * 4f,

            ShoulderFar = -8 + swingFar * 18f,
            ElbowFar = 14 + liftFar * 12f,

            Lean = 4,
            // Two dips per cycle, at each push-off.
            BodyY = -MathF.Abs(swingNear) * 1.5f,
            BodyX = 0,
            HeadTilt = -swingNear * 1.5f,

            CapeSway = 0.5f + liftNear * 0.1f,
            SwordAngle = 12 - swingNear * 4f
        };
    }

    public static Pose Crouch() => new Pose
    {
        HipNear = -30, KneeNear = 74, AnkleNear = -32,
        HipFar = -20, KneeFar = 68, AnkleFar = -30,
        ShoulderNear = 28, ElbowNear = 22,
        ShoulderFar = 14, ElbowFar = 30,
        Lean = 12, BodyY = 13, BodyX = -1, HeadTilt = -4,
        CapeSway = 0.2f, SwordAngle = 26
    };

    public static Pose Jump() => new Pose
    {
        HipNear = -26, KneeNear = 54, AnkleNear = -16,
        HipFar = 18, KneeFar = 16, AnkleFar = 8,
        ShoulderNear = -8, ElbowNear = 10,
        ShoulderFar = 34, ElbowFar = 12,
        Lean = -4, BodyY = -2, BodyX = 0, HeadTilt = 3,
        CapeSway = 0.9f, SwordAngle = -6
    };

    public static Pose Fall() => new Pose
    {
        HipNear = 20, KneeNear = 12, AnkleNear = 10,
        HipFar = -16, KneeFar = 36, AnkleFar = -8,
        ShoulderNear = 18, ElbowNear = 18,
        ShoulderFar = -28, ElbowFar = 18,
        Lean = 5, BodyY = 0, BodyX = 0, HeadTilt = -3,
        CapeSway = 0.75f, SwordAngle = 16
    };

    /// <summary>A low committed lunge, cape streaming flat behind.</summary>
    public static Pose Dash() => new Pose
    {
        HipNear = 40, KneeNear = 8, AnkleNear = 12,
        HipFar = -42, KneeFar = 56, AnkleFar = -18,
        ShoulderNear = -18, ElbowNear = 8,
        ShoulderFar = 40, ElbowFar = 14,
        Lean = 18, BodyY = 8, BodyX = 3, HeadTilt = -6,
        CapeSway = 1.7f, SwordAngle = -14
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
