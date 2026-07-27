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
/// The player: a winged knight animated from a pre-rendered sprite sheet.
///
/// The frames are produced by design/knight.py, which poses the figure as
/// geometry and runs it through a shading pass (distance-field lighting, ramp
/// quantisation, outline tracing) before exporting. Posing therefore happens at
/// design time, and the game only ever blits finished bitmaps at whole-number
/// scales, which keeps the pixel grid perfectly intact.
///
/// Sheet layout: 4 idle frames, 8 walk frames, then crouch, jump, fall, dash.
/// </summary>
public class Character : IDisposable
{
    // ---- movement tuning ---------------------------------------------------

    private const float WalkSpeed = 320f;
    private const float JumpVelocity = -880f;
    private const float Gravity = 2300f;
    private const float MaxFallSpeed = 1400f;

    private const float DashSpeed = 1500f;
    private const float DashDuration = 0.16f;
    private const float DashCooldown = 0.32f;

    /// <summary>Momentum kept when the dash ends, so it does not stop dead.</summary>
    private const float DashExitSpeed = 0.35f;

    private const float FallGravityMultiplier = 1.5f;
    private const float CoyoteTime = 0.10f;
    private const float JumpBufferTime = 0.12f;

    // ---- sheet layout ------------------------------------------------------
    // Must match the export in design/knight.py.

    private const int FrameWidth = 110;
    private const int FrameHeight = 114;

    /// <summary>Where the feet sit inside a frame.</summary>
    private const int FootX = 60;
    private const int FootY = 106;

    /// <summary>Height of the knight in art pixels, used to pick the draw scale.</summary>
    private const int ArtHeight = 100;

    private const int IdleStart = 0, IdleCount = 4;
    private const int WalkStart = 4, WalkCount = 8;
    private const int CrouchFrame = 12;
    private const int JumpFrame = 13;
    private const int FallFrame = 14;
    private const int DashFrame = 15;

    // ---- resources ---------------------------------------------------------

    private readonly Texture2D? _sheet;
    private readonly Rectangle[] _frames;
    private readonly Texture2D _shadow;

    /// <summary>Fallback box used when the sheet is missing, so the game still runs.</summary>
    private readonly Texture2D _fallback;

    // ---- state -------------------------------------------------------------

    public Vector2 Position;
    public Vector2 Velocity;

    /// <summary>Requested height in screen pixels; snapped to a whole art scale.</summary>
    public float DisplayHeight { get; set; } = 220f;

    /// <summary>-1 facing left, 1 facing right.</summary>
    public int FacingSign { get; private set; } = 1;

    public CharacterState State { get; private set; } = CharacterState.Idle;
    public bool OnGround { get; private set; } = true;
    public bool IsCrouching { get; private set; }

    public bool HasArt => _sheet != null;

    /// <summary>Ground height in world space; set by the scene each frame.</summary>
    public float GroundY { get; set; }

    private float _animTime;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private int _dashDirection = 1;

    // A dense ghost trail is the signature of the dash: several afterimages
    // that linger and fade rather than one blurred smear.
    private const int TrailLength = 10;
    private const float TrailLifetime = 0.30f;

    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private readonly float[] _trailAge = new float[TrailLength];
    private readonly int[] _trailFacing = new int[TrailLength];
    private readonly int[] _trailFrame = new int[TrailLength];
    private int _trailIndex;
    private float _trailTimer;

    public Character(GraphicsDevice device, string assetDir)
    {
        _sheet = TextureLoader.Load(device, assetDir, "knight", false);

        if (_sheet == null)
        {
            Console.WriteLine("knight.png not found; run design/knight.py to regenerate it.");
        }

        // Frame rectangles across the strip.
        int count = _sheet != null ? Math.Max(1, _sheet.Width / FrameWidth) : 16;
        _frames = new Rectangle[count];
        for (int i = 0; i < count; i++)
        {
            _frames[i] = new Rectangle(i * FrameWidth, 0, FrameWidth, FrameHeight);
        }

        _shadow = CreateShadowTexture(device, 64);

        _fallback = new Texture2D(device, 1, 1);
        _fallback.SetData(new[] { new Color(180, 60, 60) });

        for (int i = 0; i < _trailAge.Length; i++) _trailAge[i] = float.MaxValue;
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
        // Eases from full speed down over the burst, which feels like a lunge
        // rather than a constant-velocity slide.
        float progress = 1f - _dashTimer / DashDuration;
        float speedCurve = MathHelper.Lerp(1f, 0.55f, progress * progress);

        Velocity.X = _dashDirection * DashSpeed * speedCurve;

        // Weightless while dashing, which is what makes it useful over gaps.
        Velocity.Y = 0f;

        if (_dashTimer <= dt)
        {
            Velocity.X = _dashDirection * DashSpeed * DashExitSpeed;
        }

        _trailTimer -= dt;
        if (_trailTimer <= 0f)
        {
            _trailTimer = 0.015f;
            _trail[_trailIndex] = Position;
            _trailAge[_trailIndex] = 0f;
            _trailFacing[_trailIndex] = FacingSign;
            _trailFrame[_trailIndex] = DashFrame;
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
        // The walk cycle is driven by real speed, so the feet never slide.
        float speedRatio = MathF.Abs(Velocity.X) / WalkSpeed;

        _animTime += State switch
        {
            CharacterState.Walk => dt * 11f * MathF.Max(0.45f, speedRatio),
            CharacterState.Idle => dt * 4.5f,
            _ => dt * 6f
        };

        for (int i = 0; i < _trailAge.Length; i++)
        {
            if (_trailAge[i] < float.MaxValue) _trailAge[i] += dt;
        }
    }

    /// <summary>Index into the sprite sheet for the current state.</summary>
    private int CurrentFrame()
    {
        switch (State)
        {
            case CharacterState.Walk:
                return WalkStart + Wrap((int)_animTime, WalkCount);
            case CharacterState.Idle:
                return IdleStart + Wrap((int)_animTime, IdleCount);
            case CharacterState.Crouch:
                return CrouchFrame;
            case CharacterState.Jump:
                return JumpFrame;
            case CharacterState.Fall:
                return FallFrame;
            case CharacterState.Dash:
                return DashFrame;
            default:
                return IdleStart;
        }
    }

    private static int Wrap(int value, int count)
    {
        int result = value % count;
        return result < 0 ? result + count : result;
    }

    public void Draw(SpriteBatch spriteBatch, float cameraX)
    {
        // Whole-number scale only: anything else destroys the pixel grid.
        int scale = Math.Max(1, (int)MathF.Round(DisplayHeight / ArtHeight));

        float screenXf = Position.X - cameraX;

        // ---- shadow ----

        float airHeight = MathHelper.Clamp((GroundY - Position.Y) / 260f, 0f, 1f);
        float shadowScale = 1f - airHeight * 0.45f;
        float shadowAlpha = 1f - airHeight * 0.55f;
        float shadowWidth = ArtHeight * scale * 0.34f * shadowScale;
        float shadowHeight = shadowWidth * 0.26f;

        spriteBatch.Draw(_shadow,
            new Rectangle(
                (int)(screenXf - shadowWidth / 2f),
                (int)(GroundY - shadowHeight / 2f),
                (int)shadowWidth,
                (int)shadowHeight),
            Color.White * shadowAlpha);

        if (_sheet == null)
        {
            // Still show something solid so the scene is testable.
            int w = 20 * scale, h = ArtHeight * scale;
            spriteBatch.Draw(_fallback,
                new Rectangle((int)screenXf - w / 2, (int)Position.Y - h, w, h),
                Color.White);
            return;
        }

        // ---- ghost trail, drawn behind the knight ----

        for (int i = 0; i < _trail.Length; i++)
        {
            float age = _trailAge[i];
            if (age > TrailLifetime) continue;

            float fade = 1f - age / TrailLifetime;

            // Cools from near-white to a deep blue as it fades.
            Color ghost = Color.Lerp(
                new Color(120, 140, 220),
                new Color(230, 240, 255),
                fade);

            DrawFrame(spriteBatch, _trailFrame[i],
                (int)MathF.Round(_trail[i].X - cameraX),
                (int)MathF.Round(_trail[i].Y),
                scale, _trailFacing[i] < 0, ghost * (fade * fade * 0.55f));
        }

        // ---- knight ----

        DrawFrame(spriteBatch, CurrentFrame(),
            (int)MathF.Round(screenXf), (int)MathF.Round(Position.Y),
            scale, FacingSign < 0, Color.White);
    }

    /// <summary>Blits one sheet frame with its foot anchor on the given point.</summary>
    private void DrawFrame(SpriteBatch spriteBatch, int frame, int footX, int footY,
        int scale, bool flip, Color tint)
    {
        if (_sheet == null) return;

        frame = Math.Clamp(frame, 0, _frames.Length - 1);
        Rectangle source = _frames[frame];

        // Mirroring reflects the anchor across the frame, so the feet stay put.
        int anchorX = flip ? FrameWidth - FootX : FootX;

        var destination = new Rectangle(
            footX - anchorX * scale,
            footY - FootY * scale,
            FrameWidth * scale,
            FrameHeight * scale);

        spriteBatch.Draw(_sheet, destination, source, tint, 0f, Vector2.Zero,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    public void Dispose()
    {
        _sheet?.Dispose();
        _shadow?.Dispose();
        _fallback?.Dispose();
    }
}
