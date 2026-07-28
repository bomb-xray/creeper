using Microsoft.Xna.Framework;
using System;

namespace CreeperGame.Art;

/// <summary>
/// Joint angles and offsets for one pose, in degrees measured clockwise from
/// straight down. Zero hangs vertically, positive swings forward (to the right).
/// </summary>
public struct Pose
{
    /// <summary>
    /// Knee bend, in degrees. A human knee only folds one way -- the shin swings
    /// backwards, never forwards -- so this value must be NEGATIVE to bend the
    /// leg naturally. Positive values hyperextend the joint, which is what made
    /// the legs look broken in an earlier build.
    /// </summary>
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

    /// <summary>0 folded against the back, 1 fully spread.</summary>
    public float WingOpen;

    /// <summary>Blade angle relative to the forearm.</summary>
    public float SwordAngle;

    public static Pose Rest => new Pose
    {
        HipNear = 4, KneeNear = 4, AnkleNear = 0,
        HipFar = -7, KneeFar = 7, AnkleFar = 2,
        ShoulderNear = 10, ElbowNear = 14,
        ShoulderFar = -6, ElbowFar = 12,
        Lean = 0, BodyY = 0, BodyX = 0, HeadTilt = 0,
        CapeSway = 0.1f, WingOpen = 0.05f, SwordAngle = 8
    };
}
