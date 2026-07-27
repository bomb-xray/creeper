using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CreeperGame;

/// <summary>What the character is currently doing. Drives the animation.</summary>
public enum CharacterState
{
    Idle,
    Walk,
    Crouch,
    Jump,
    Fall,
    Dash
}

/// <summary>
/// The player: a winged knight built from a bone hierarchy rather than a sprite.
///
/// Everything is drawn from geometry, so poses are generated at runtime. That is
/// the whole point of doing it this way: new actions (attacking, casting, being
/// hit, cutscene acting) are a handful of angle curves rather than a new sheet of
/// artwork, and the rig can be re-coloured, damaged or scaled on the fly.
///
/// The palette follows the concept art: steel plate, a deep red cape and cloth,
/// and large pale wings.
/// </summary>
public class Character : IDisposable
{
    // ---- movement tuning ---------------------------------------------------

    private const float WalkSpeed = 320f;
    private const float JumpVelocity = -880f;
    private const float Gravity = 2300f;
    private const float MaxFallSpeed = 1400f;

    private const float DashSpeed = 1150f;
    private const float DashDuration = 0.18f;
    private const float DashCooldown = 0.55f;

    private const float FallGravityMultiplier = 1.5f;
    private const float CoyoteTime = 0.10f;
    private const float JumpBufferTime = 0.12f;

    // ---- proportions -------------------------------------------------------
    // Authored in "rig units": the figure is about 100 tall, and everything is
    // scaled to DisplayHeight when drawn. Working in fixed units keeps the maths
    // readable and independent of resolution.

    private const float RigHeight = 100f;

    // ---- palette -----------------------------------------------------------

    private static readonly Color SteelDark = new Color(88, 92, 104);
    private static readonly Color Steel = new Color(138, 143, 158);
    private static readonly Color SteelLight = new Color(196, 201, 214);
    private static readonly Color CapeRed = new Color(122, 26, 32);
    private static readonly Color CapeRedDark = new Color(78, 16, 22);
    private static readonly Color ClothDark = new Color(46, 44, 52);
    private static readonly Color WingPale = new Color(226, 228, 236);
    private static readonly Color WingShade = new Color(176, 180, 196);
    private static readonly Color BladeSteel = new Color(170, 176, 190);

    // ---- rig ---------------------------------------------------------------

    private readonly Skeleton _skeleton;

    private readonly Bone _pelvis;
    private readonly Bone _torso;
    private readonly Bone _chest;
    private readonly Bone _neck;
    private readonly Bone _head;
    private readonly Bone _crest;

    private readonly Bone _armFarUpper;
    private readonly Bone _armFarLower;
    private readonly Bone _armNearUpper;
    private readonly Bone _armNearLower;
    private readonly Bone _sword;

    private readonly Bone _legFarUpper;
    private readonly Bone _legFarLower;
    private readonly Bone _legFarFoot;
    private readonly Bone _legNearUpper;
    private readonly Bone _legNearLower;
    private readonly Bone _legNearFoot;

    private readonly Bone[] _capeSegments;
    private readonly Bone[] _wingFar;
    private readonly Bone[] _wingNear;

    private readonly Texture2D _shadow;

    // ---- state -------------------------------------------------------------

    public Vector2 Position;
    public Vector2 Velocity;

    /// <summary>Height the character is drawn at, in screen pixels.</summary>
    public float DisplayHeight { get; set; } = 220f;

    /// <summary>-1 facing left, 1 facing right.</summary>
    public int FacingSign { get; private set; } = 1;

    public CharacterState State { get; private set; } = CharacterState.Idle;
    public bool OnGround { get; private set; } = true;
    public bool IsCrouching { get; private set; }

    /// <summary>The rig is generated, so there is always something to draw.</summary>
    public bool HasArt => true;

    /// <summary>Ground height in world space; set by the scene each frame.</summary>
    public float GroundY { get; set; }

    private float _walkCycle;
    private float _breathCycle;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private int _dashDirection = 1;
    private float _squash = 1f;

    /// <summary>Cape and wing motion lags the body, which is what sells the weight.</summary>
    private float _capeSway;
    private float _wingFlap;

    private readonly Vector2[] _trail = new Vector2[6];
    private readonly float[] _trailAge = new float[6];
    private int _trailIndex;
    private float _trailTimer;

    public Character(GraphicsDevice device, string assetDir)
    {
        _skeleton = new Skeleton(device);

        // Depth ordering, back to front:
        //   0 far wing, 1 cape, 2 far limbs, 3 body, 4 head, 5 near limbs,
        //   6 near wing, 7 sword
        const int dFarWing = 0, dCape = 1, dFarLimb = 2, dBody = 3;
        const int dHead = 4, dNearLimb = 5, dNearWing = 6, dSword = 7;

        // Angles: -PI/2 is straight up, +PI/2 straight down, 0 is to the right.
        const float up = -MathF.PI / 2f;
        const float down = MathF.PI / 2f;

        // ---- spine ----

        _pelvis = _skeleton.Add("pelvis", null, 6f, 15f, ClothDark, dBody, up);
        _torso = _skeleton.Add("torso", _pelvis, 17f, 17f, Steel, dBody, 0f,
            tipThickness: 20f);
        _torso.Highlight = SteelLight;

        _chest = _skeleton.Add("chest", _torso, 11f, 21f, SteelDark, dBody, 0f,
            tipThickness: 17f);

        _neck = _skeleton.Add("neck", _chest, 3.5f, 7f, ClothDark, dBody);
        _head = _skeleton.Add("head", _neck, 13f, 12f, Steel, dHead, 0f,
            tipThickness: 10f);
        _head.Highlight = SteelLight;

        // The red plume on the helmet.
        _crest = _skeleton.Add("crest", _head, 9f, 6f, CapeRed, dHead, -0.25f,
            tipThickness: 2f);

        // ---- far side limbs (drawn behind the torso) ----

        _armFarUpper = _skeleton.Add("armFarUpper", _chest, 13f, 8f, SteelDark, dFarLimb,
            down * 0.82f, offset: new Vector2(0f, 2f), attachToTip: false);
        _armFarLower = _skeleton.Add("armFarLower", _armFarUpper, 12f, 6.5f, SteelDark,
            dFarLimb, 0.35f, tipThickness: 5.5f);

        _legFarUpper = _skeleton.Add("legFarUpper", _pelvis, 16f, 10f, SteelDark, dFarLimb,
            down, offset: new Vector2(0f, -3f), attachToTip: false);
        _legFarLower = _skeleton.Add("legFarLower", _legFarUpper, 16f, 8f, SteelDark,
            dFarLimb, 0f, tipThickness: 6.5f);
        _legFarFoot = _skeleton.Add("legFarFoot", _legFarLower, 8f, 6f, ClothDark,
            dFarLimb, -down * 0.9f);

        // ---- near side limbs ----

        _armNearUpper = _skeleton.Add("armNearUpper", _chest, 13f, 9f, Steel, dNearLimb,
            down * 0.78f, offset: new Vector2(0f, 2f), attachToTip: false);
        _armNearUpper.Highlight = SteelLight;

        _armNearLower = _skeleton.Add("armNearLower", _armNearUpper, 12f, 7f, Steel,
            dNearLimb, 0.30f, tipThickness: 6f);

        // Held out in front, as in the turnaround.
        _sword = _skeleton.Add("sword", _armNearLower, 42f, 3.4f, BladeSteel, dSword,
            -1.35f, tipThickness: 1.6f);
        _sword.Highlight = SteelLight;

        _legNearUpper = _skeleton.Add("legNearUpper", _pelvis, 16f, 11f, Steel, dNearLimb,
            down, offset: new Vector2(0f, 3f), attachToTip: false);
        _legNearUpper.Highlight = SteelLight;

        _legNearLower = _skeleton.Add("legNearLower", _legNearUpper, 16f, 9f, Steel,
            dNearLimb, 0f, tipThickness: 7f);
        _legNearFoot = _skeleton.Add("legNearFoot", _legNearLower, 9f, 6.5f, ClothDark,
            dNearLimb, -down * 0.9f);

        // ---- cape: a chain that trails behind ----

        _capeSegments = new Bone[4];
        Bone capeParent = _chest;

        for (int i = 0; i < _capeSegments.Length; i++)
        {
            // Tapers and darkens towards the hem.
            float t = i / (float)_capeSegments.Length;
            Color shade = Color.Lerp(CapeRed, CapeRedDark, t);

            _capeSegments[i] = _skeleton.Add($"cape{i}", capeParent, 13f,
                17f - i * 1.6f, shade, dCape,
                i == 0 ? down * 0.92f : 0.12f,
                offset: i == 0 ? new Vector2(-3f, 0f) : Vector2.Zero,
                tipThickness: 15.5f - i * 1.6f,
                attachToTip: i != 0);

            capeParent = _capeSegments[i];
        }

        // ---- wings ----

        _wingFar = BuildWing("wingFar", dFarWing, WingShade);
        _wingNear = BuildWing("wingNear", dNearWing, WingPale);

        _shadow = CreateShadowTexture(device, 64);

        for (int i = 0; i < _trailAge.Length; i++) _trailAge[i] = float.MaxValue;

        Console.WriteLine("Character rig built procedurally (no sprite needed).");
    }

    /// <summary>Builds one wing as an upper arm, forearm and a fan of feathers.</summary>
    private Bone[] BuildWing(string prefix, int depth, Color colour)
    {
        var bones = new Bone[8];

        // Shoulder joint sits high on the back.
        bones[0] = _skeleton.Add($"{prefix}Upper", _chest, 20f, 9f, colour, depth,
            -2.5f, offset: new Vector2(-2f, 0f), tipThickness: 7f, attachToTip: false);

        bones[1] = _skeleton.Add($"{prefix}Fore", bones[0], 22f, 7f, colour, depth,
            0.85f, tipThickness: 5f);

        // Six primary feathers fanning out from the forearm.
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float spread = MathHelper.Lerp(-0.55f, 1.15f, t);
            float length = MathHelper.Lerp(30f, 17f, t * t);

            bones[2 + i] = _skeleton.Add($"{prefix}Feather{i}", bones[1], length, 6.5f,
                Color.Lerp(colour, WingShade, t * 0.45f), depth, spread,
                tipThickness: 2f);
        }

        return bones;
    }

    private static Texture2D CreateShadowTexture(GraphicsDevice device, int size)
    {
        var pixels = new Color[size * size];
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - radius + 0.5f) / radius;
                float dy = (y - radius + 0.5f) / radius;
                float distance = MathF.Sqrt(dx * dx + dy * dy);

                float strength = MathHelper.Clamp(1f - distance, 0f, 1f);
                strength *= strength;

                var pixel = Color.Black;
                pixel.A = (byte)(strength * 150);
                pixels[y * size + x] = pixel;
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Everything the character reads from the player in one frame.</summary>
    public struct Input
    {
        public int Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool CrouchHeld;
        public bool DashPressed;
    }

    public void Update(float dt, Input input, float leftBound, float rightBound)
    {
        UpdateTimers(dt, input);

        if (_dashTimer > 0f) UpdateDash(dt);
        else UpdateNormalMovement(dt, input);

        ApplyGravity(dt, input);
        MoveAndCollide(dt, leftBound, rightBound);
        UpdateState();
        UpdateAnimation(dt);
    }

    private void UpdateTimers(float dt, Input input)
    {
        _coyoteTimer = OnGround ? CoyoteTime : MathF.Max(0f, _coyoteTimer - dt);

        _jumpBufferTimer = input.JumpPressed
            ? JumpBufferTime
            : MathF.Max(0f, _jumpBufferTimer - dt);

        _dashTimer = MathF.Max(0f, _dashTimer - dt);
        _dashCooldownTimer = MathF.Max(0f, _dashCooldownTimer - dt);
    }

    private void UpdateDash(float dt)
    {
        Velocity.X = _dashDirection * DashSpeed;
        Velocity.Y = 0f;

        _trailTimer -= dt;
        if (_trailTimer <= 0f)
        {
            _trailTimer = 0.02f;
            _trail[_trailIndex] = Position;
            _trailAge[_trailIndex] = 0f;
            _trailIndex = (_trailIndex + 1) % _trail.Length;
        }
    }

    private void UpdateNormalMovement(float dt, Input input)
    {
        IsCrouching = input.CrouchHeld && OnGround;

        if (input.DashPressed && _dashCooldownTimer <= 0f)
        {
            _dashDirection = input.Move != 0 ? input.Move : FacingSign;
            FacingSign = _dashDirection;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown + DashDuration;
            _squash = 0.82f;
            return;
        }

        float targetSpeed = IsCrouching ? 0f : input.Move * WalkSpeed;
        float acceleration = OnGround ? 18f : 8f;
        Velocity.X = MathHelper.Lerp(Velocity.X, targetSpeed, MathF.Min(1f, acceleration * dt));

        if (input.Move != 0 && !IsCrouching) FacingSign = input.Move;

        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && !IsCrouching)
        {
            Velocity.Y = JumpVelocity;
            OnGround = false;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _squash = 1.18f;
            _wingFlap = 1f; // wings snap open on take-off
        }
    }

    private void ApplyGravity(float dt, Input input)
    {
        if (_dashTimer > 0f) return;

        float gravity = Gravity;
        if (Velocity.Y < 0f && !input.JumpHeld) gravity *= 2.2f;
        else if (Velocity.Y > 0f) gravity *= FallGravityMultiplier;

        Velocity.Y = MathF.Min(MaxFallSpeed, Velocity.Y + gravity * dt);
    }

    private void MoveAndCollide(float dt, float leftBound, float rightBound)
    {
        Position += Velocity * dt;

        if (Position.Y >= GroundY)
        {
            if (!OnGround && Velocity.Y > 400f)
            {
                _squash = MathHelper.Clamp(1f - Velocity.Y / 6000f, 0.78f, 0.97f);
            }

            Position.Y = GroundY;
            Velocity.Y = 0f;
            OnGround = true;
        }
        else
        {
            OnGround = false;
        }

        Position.X = MathHelper.Clamp(Position.X, leftBound, rightBound);
    }

    private void UpdateState()
    {
        if (_dashTimer > 0f) State = CharacterState.Dash;
        else if (!OnGround) State = Velocity.Y < 0f ? CharacterState.Jump : CharacterState.Fall;
        else if (IsCrouching) State = CharacterState.Crouch;
        else if (MathF.Abs(Velocity.X) > 25f) State = CharacterState.Walk;
        else State = CharacterState.Idle;
    }

    private void UpdateAnimation(float dt)
    {
        float speedRatio = MathF.Abs(Velocity.X) / WalkSpeed;

        // Tying the cycle to real speed stops the feet from sliding.
        _walkCycle += State switch
        {
            CharacterState.Walk => dt * 8.5f * MathF.Max(0.45f, speedRatio),
            CharacterState.Crouch => dt * 2f,
            _ => dt * 2f
        };

        _breathCycle += dt * 1.6f;
        _squash = MathHelper.Lerp(_squash, 1f, MathF.Min(1f, 9f * dt));
        _wingFlap = MathHelper.Lerp(_wingFlap, 0f, MathF.Min(1f, 3.5f * dt));

        // Cape lag is driven by horizontal speed, so it streams out when running.
        float targetSway = -Velocity.X / WalkSpeed * 0.55f;
        if (!OnGround) targetSway -= 0.25f;
        _capeSway = MathHelper.Lerp(_capeSway, targetSway, MathF.Min(1f, 5f * dt));

        for (int i = 0; i < _trailAge.Length; i++)
        {
            if (_trailAge[i] < float.MaxValue) _trailAge[i] += dt;
        }

        PoseSkeleton();
    }

    /// <summary>Writes the current pose into the bone angles.</summary>
    private void PoseSkeleton()
    {
        const float down = MathF.PI / 2f;

        float swing = MathF.Sin(_walkCycle);
        float swingOpposite = MathF.Sin(_walkCycle + MathF.PI);
        float breath = MathF.Sin(_breathCycle);

        // Reset to rest, then layer the state's pose on top.
        _torso.LocalAngle = 0f;
        _chest.LocalAngle = 0f;
        _head.LocalAngle = 0f;

        switch (State)
        {
            case CharacterState.Idle:
                // Breathing, a slow drift in the arms, and settled legs.
                _chest.LocalAngle = breath * 0.02f;
                _head.LocalAngle = breath * -0.03f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot, down + 0.03f, 0.02f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot, down - 0.05f, 0.05f);

                _armNearUpper.LocalAngle = down * 0.72f + breath * 0.03f;
                _armNearLower.LocalAngle = 0.42f;
                _armFarUpper.LocalAngle = down * 0.86f - breath * 0.02f;
                _armFarLower.LocalAngle = 0.30f;

                _sword.LocalAngle = -1.30f + breath * 0.02f;
                break;

            case CharacterState.Walk:
                // Counter-rotating limbs, torso leaning into the stride.
                _torso.LocalAngle = 0.06f;
                _chest.LocalAngle = swing * 0.05f;
                _head.LocalAngle = -swing * 0.04f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot,
                    down + swing * 0.62f, MathF.Max(0f, -swing) * 0.85f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot,
                    down + swingOpposite * 0.62f, MathF.Max(0f, -swingOpposite) * 0.85f);

                // Arms swing opposite their leg, but the sword arm stays composed.
                _armNearUpper.LocalAngle = down * 0.74f + swingOpposite * 0.24f;
                _armNearLower.LocalAngle = 0.40f + MathF.Max(0f, swing) * 0.18f;
                _armFarUpper.LocalAngle = down * 0.84f + swing * 0.34f;
                _armFarLower.LocalAngle = 0.34f + MathF.Max(0f, swingOpposite) * 0.22f;

                _sword.LocalAngle = -1.32f - swingOpposite * 0.06f;
                break;

            case CharacterState.Crouch:
                // Deep knee bend, torso folded forward, sword drawn in.
                _torso.LocalAngle = 0.34f;
                _chest.LocalAngle = 0.16f;
                _head.LocalAngle = -0.34f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot, down - 0.85f, 1.75f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot, down - 0.70f, 1.60f);

                _armNearUpper.LocalAngle = down * 0.52f;
                _armNearLower.LocalAngle = 0.85f;
                _armFarUpper.LocalAngle = down * 0.66f;
                _armFarLower.LocalAngle = 0.75f;

                _sword.LocalAngle = -1.05f;
                break;

            case CharacterState.Jump:
                // Trailing legs, sword raised, chest opened up.
                _torso.LocalAngle = -0.10f;
                _chest.LocalAngle = -0.06f;
                _head.LocalAngle = 0.08f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot, down - 0.52f, 1.15f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot, down + 0.30f, 0.42f);

                _armNearUpper.LocalAngle = down * 0.42f;
                _armNearLower.LocalAngle = 0.30f;
                _armFarUpper.LocalAngle = down * 1.18f;
                _armFarLower.LocalAngle = 0.22f;

                _sword.LocalAngle = -1.55f;
                break;

            case CharacterState.Fall:
                // Legs reaching for the ground, arms out for balance.
                _torso.LocalAngle = 0.08f;
                _chest.LocalAngle = 0.04f;
                _head.LocalAngle = -0.10f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot, down + 0.34f, 0.30f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot, down - 0.22f, 0.62f);

                _armNearUpper.LocalAngle = down * 0.58f;
                _armNearLower.LocalAngle = 0.48f;
                _armFarUpper.LocalAngle = down * 1.32f;
                _armFarLower.LocalAngle = 0.36f;

                _sword.LocalAngle = -1.20f;
                break;

            case CharacterState.Dash:
                // Everything streams backwards; the sword leads the charge.
                _torso.LocalAngle = 0.30f;
                _chest.LocalAngle = 0.14f;
                _head.LocalAngle = -0.24f;

                SetLeg(_legNearUpper, _legNearLower, _legNearFoot, down + 0.78f, 0.30f);
                SetLeg(_legFarUpper, _legFarLower, _legFarFoot, down - 0.62f, 1.25f);

                _armNearUpper.LocalAngle = down * 0.30f;
                _armNearLower.LocalAngle = 0.12f;
                _armFarUpper.LocalAngle = down * 1.45f;
                _armFarLower.LocalAngle = 0.30f;

                _sword.LocalAngle = -1.62f;
                break;
        }

        PoseCape();
        PoseWings();
    }

    /// <summary>Sets a leg from a hip angle and a knee bend.</summary>
    private static void SetLeg(Bone upper, Bone lower, Bone foot, float hipAngle, float kneeBend)
    {
        upper.LocalAngle = hipAngle;
        lower.LocalAngle = kneeBend;

        // Keep the foot roughly flat to the ground regardless of the leg pose.
        foot.LocalAngle = -(hipAngle - MathF.PI / 2f) - kneeBend - MathF.PI / 2f;
    }

    private void PoseCape()
    {
        const float down = MathF.PI / 2f;

        // Each segment lags a little more than the one above, so the cape
        // ripples rather than swinging as a rigid board.
        float flutter = MathF.Sin(_walkCycle * 1.6f) * 0.05f;

        for (int i = 0; i < _capeSegments.Length; i++)
        {
            float lag = 1f + i * 0.55f;

            if (i == 0)
            {
                _capeSegments[i].LocalAngle = down * 0.92f + _capeSway * 0.35f;
            }
            else
            {
                _capeSegments[i].LocalAngle = 0.10f + _capeSway * 0.30f * lag + flutter * i;
            }
        }
    }

    private void PoseWings()
    {
        // A slow idle drift, overridden by a strong beat when airborne.
        float idle = MathF.Sin(_breathCycle * 0.9f) * 0.05f;
        float airborne = OnGround ? 0f : 0.30f;
        float beat = _wingFlap * 0.55f;

        float openFar = -2.45f - idle - airborne - beat;
        float openNear = -2.30f + idle - airborne * 0.85f - beat * 0.9f;

        _wingFar[0].LocalAngle = openFar;
        _wingFar[1].LocalAngle = 0.85f + beat * 0.35f + idle;

        _wingNear[0].LocalAngle = openNear;
        _wingNear[1].LocalAngle = 0.80f + beat * 0.30f - idle;

        // Feathers splay wider as the wing opens.
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float baseSpread = MathHelper.Lerp(-0.55f, 1.15f, t);
            float splay = (beat + airborne) * MathHelper.Lerp(0.10f, 0.32f, t);
            float ripple = MathF.Sin(_breathCycle * 1.2f + i * 0.5f) * 0.025f;

            _wingFar[2 + i].LocalAngle = baseSpread + splay + ripple;
            _wingNear[2 + i].LocalAngle = baseSpread + splay - ripple;
        }
    }

    public void Draw(SpriteBatch spriteBatch, float cameraX)
    {
        float screenX = Position.X - cameraX;

        // Rig units to screen pixels.
        float scale = DisplayHeight / RigHeight;

        float bob = 0f;
        float scaleY = _squash;
        float lean = 0f;

        switch (State)
        {
            case CharacterState.Walk:
                bob = -MathF.Abs(MathF.Sin(_walkCycle)) * DisplayHeight * 0.018f;
                lean = 0.02f;
                break;
            case CharacterState.Idle:
                bob = -MathF.Abs(MathF.Sin(_breathCycle)) * DisplayHeight * 0.004f;
                break;
            case CharacterState.Dash:
                lean = 0.10f;
                break;
            case CharacterState.Jump:
                lean = 0.04f;
                break;
            case CharacterState.Fall:
                lean = -0.03f;
                break;
        }

        bool mirror = FacingSign < 0;

        // ---- dash trail ----

        if (_dashTimer > 0f)
        {
            for (int i = 0; i < _trail.Length; i++)
            {
                float age = _trailAge[i];
                if (age > 0.22f) continue;

                float fade = 1f - age / 0.22f;
                float ghostX = _trail[i].X - cameraX;

                _skeleton.Resolve(new Vector2(ghostX, _trail[i].Y), lean * FacingSign);
                _skeleton.Draw(spriteBatch, scale * scaleY, mirror, ghostX,
                    new Color(150, 170, 255), fade * 0.30f);
            }
        }

        // ---- shadow ----

        float airHeight = MathHelper.Clamp((GroundY - Position.Y) / 260f, 0f, 1f);
        float shadowScale = 1f - airHeight * 0.45f;
        float shadowAlpha = 1f - airHeight * 0.55f;
        float shadowWidth = DisplayHeight * 0.42f * shadowScale;
        float shadowHeight = shadowWidth * 0.26f;

        spriteBatch.Draw(_shadow,
            new Rectangle(
                (int)(screenX - shadowWidth / 2f),
                (int)(GroundY - shadowHeight / 2f),
                (int)shadowWidth,
                (int)shadowHeight),
            Color.White * shadowAlpha);

        // ---- character ----

        // Bones are authored pointing up from the feet, so the root sits at the
        // ground contact point and the whole rig grows upward from there.
        var root = new Vector2(screenX, Position.Y + bob);

        _skeleton.Resolve(root, lean * FacingSign);
        _skeleton.Draw(spriteBatch, scale * scaleY, mirror, screenX, Color.White);
    }

    public void Dispose()
    {
        _skeleton?.Dispose();
        _shadow?.Dispose();
    }
}
