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
/// The player character in the side-scrolling world.
///
/// The art is a single side view, so there are no hand-drawn walk frames. The
/// motion is produced procedurally instead: a bob and lean while walking, a
/// stretch on take-off, a squash on landing, a crouch scale, and a streak of
/// afterimages during a dash. That reads as animation without needing a sprite
/// sheet, and it can be swapped for real frames later without touching the
/// movement code.
/// </summary>
public class Character : IDisposable
{
    // ---- tuning ------------------------------------------------------------

    private const float WalkSpeed = 320f;
    private const float JumpVelocity = -880f;
    private const float Gravity = 2300f;
    private const float MaxFallSpeed = 1400f;

    private const float DashSpeed = 1150f;
    private const float DashDuration = 0.18f;
    private const float DashCooldown = 0.55f;

    /// <summary>Extra gravity while falling, so jumps feel snappy rather than floaty.</summary>
    private const float FallGravityMultiplier = 1.5f;

    /// <summary>Grace period after walking off a ledge where a jump still works.</summary>
    private const float CoyoteTime = 0.10f;

    /// <summary>A jump pressed this long before landing still fires on touchdown.</summary>
    private const float JumpBufferTime = 0.12f;

    // ---- art ---------------------------------------------------------------

    private readonly Texture2D? _side;
    private readonly Texture2D? _front;
    private readonly Texture2D _shadow;

    /// <summary>
    /// The side view is drawn facing left, so it is mirrored when walking right.
    /// </summary>
    private const bool SideArtFacesLeft = true;

    // ---- state -------------------------------------------------------------

    /// <summary>Feet position in world space.</summary>
    public Vector2 Position;

    public Vector2 Velocity;

    /// <summary>Height the character is drawn at, in screen pixels.</summary>
    public float DisplayHeight { get; set; } = 220f;

    /// <summary>-1 facing left, 1 facing right.</summary>
    public int FacingSign { get; private set; } = 1;

    public CharacterState State { get; private set; } = CharacterState.Idle;

    public bool OnGround { get; private set; } = true;

    public bool IsCrouching { get; private set; }

    public bool HasArt => _side != null || _front != null;

    /// <summary>Ground height in world space; set by the scene each frame.</summary>
    public float GroundY { get; set; }

    // Timers
    private float _walkCycle;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private int _dashDirection = 1;

    // Squash and stretch, eased back to 1 each frame.
    private float _squash = 1f;

    /// <summary>Recent positions used to draw the dash trail.</summary>
    private readonly Vector2[] _trail = new Vector2[6];
    private readonly float[] _trailAge = new float[6];
    private int _trailIndex;
    private float _trailTimer;

    public Character(GraphicsDevice device, string assetDir)
    {
        // Character art is authored with real alpha, but run it through the
        // colour key anyway in case a magenta-keyed version is dropped in.
        _side = TextureLoader.LoadAny(device, assetDir,
            new[] { "side", "char_side", "character_side", "player_side" }, true);

        _front = TextureLoader.LoadAny(device, assetDir,
            new[] { "front", "char_front", "character_front", "player_front" }, true);

        _shadow = CreateShadowTexture(device, 64);

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

                pixels[y * size + x] = new Color(0, 0, 0, (byte)(strength * 150));
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Everything the character reads from the player in one frame.</summary>
    public struct Input
    {
        /// <summary>-1, 0 or 1.</summary>
        public int Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool CrouchHeld;
        public bool DashPressed;
    }

    public void Update(float dt, Input input, float leftBound, float rightBound)
    {
        UpdateTimers(dt, input);

        if (_dashTimer > 0f)
        {
            UpdateDash(dt);
        }
        else
        {
            UpdateNormalMovement(dt, input);
        }

        ApplyGravity(dt, input);
        MoveAndCollide(dt, leftBound, rightBound);
        UpdateState(input);
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

        // A dash is weightless, which makes it useful for crossing gaps.
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

        // Start a dash.
        if (input.DashPressed && _dashCooldownTimer <= 0f)
        {
            _dashDirection = input.Move != 0 ? input.Move : FacingSign;
            FacingSign = _dashDirection;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown + DashDuration;
            _squash = 0.82f; // stretched thin along the dash
            return;
        }

        // Crouching pins the character in place; standing lets them walk.
        float targetSpeed = IsCrouching ? 0f : input.Move * WalkSpeed;

        // Ground movement is crisp, air movement has some drift.
        float acceleration = OnGround ? 18f : 8f;
        Velocity.X = MathHelper.Lerp(Velocity.X, targetSpeed, MathF.Min(1f, acceleration * dt));

        if (input.Move != 0 && !IsCrouching) FacingSign = input.Move;

        // Jump, honouring both the coyote window and the input buffer.
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && !IsCrouching)
        {
            Velocity.Y = JumpVelocity;
            OnGround = false;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _squash = 1.18f; // stretch upward on take-off
        }
    }

    private void ApplyGravity(float dt, Input input)
    {
        if (_dashTimer > 0f) return;

        float gravity = Gravity;

        // Releasing jump early cuts the arc short, giving variable jump height.
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
                // Heavier landings squash more.
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

    private void UpdateState(Input input)
    {
        if (_dashTimer > 0f) State = CharacterState.Dash;
        else if (!OnGround) State = Velocity.Y < 0f ? CharacterState.Jump : CharacterState.Fall;
        else if (IsCrouching) State = CharacterState.Crouch;
        else if (MathF.Abs(Velocity.X) > 25f) State = CharacterState.Walk;
        else State = CharacterState.Idle;
    }

    private void UpdateAnimation(float dt)
    {
        // The walk cycle is driven by actual speed, so it never slides.
        float speedRatio = MathF.Abs(Velocity.X) / WalkSpeed;

        _walkCycle += State switch
        {
            CharacterState.Walk => dt * 10f * MathF.Max(0.4f, speedRatio),
            CharacterState.Crouch => dt * 2.5f,
            _ => dt * 2f
        };

        // Ease squash and stretch back to neutral.
        _squash = MathHelper.Lerp(_squash, 1f, MathF.Min(1f, 9f * dt));

        for (int i = 0; i < _trailAge.Length; i++)
        {
            if (_trailAge[i] < float.MaxValue) _trailAge[i] += dt;
        }
    }

    public void Draw(SpriteBatch spriteBatch, float cameraX)
    {
        Texture2D? texture = _side ?? _front;
        if (texture == null) return;

        float screenX = Position.X - cameraX;

        // ---- procedural motion ----

        float bobPhase = MathF.Sin(_walkCycle);
        float bob = 0f;
        float lean = 0f;
        float scaleX = 1f;
        float scaleY = 1f;

        switch (State)
        {
            case CharacterState.Walk:
                bob = -MathF.Abs(bobPhase) * DisplayHeight * 0.028f;
                lean = bobPhase * 0.025f * FacingSign;
                break;

            case CharacterState.Idle:
                // Slow breathing so the character is never completely static.
                bob = -MathF.Abs(bobPhase) * DisplayHeight * 0.006f;
                scaleY = 1f + bobPhase * 0.008f;
                break;

            case CharacterState.Crouch:
                scaleY = 0.62f;
                scaleX = 1.14f;
                break;

            case CharacterState.Jump:
                scaleY = 1.10f;
                scaleX = 0.93f;
                lean = 0.05f * FacingSign;
                break;

            case CharacterState.Fall:
                scaleY = 1.05f;
                scaleX = 0.97f;
                lean = -0.04f * FacingSign;
                break;

            case CharacterState.Dash:
                // Stretched along the direction of travel.
                scaleX = 1.22f;
                scaleY = 0.86f;
                lean = 0.14f * FacingSign;
                break;
        }

        scaleY *= _squash;
        scaleX /= MathF.Max(0.4f, _squash); // conserve volume

        float drawHeight = DisplayHeight * scaleY;
        float drawWidth = texture.Width * (DisplayHeight / texture.Height) * scaleX;

        // Mirror when the art's authored direction disagrees with the facing.
        bool faceRight = FacingSign > 0;
        bool flip = SideArtFacesLeft ? faceRight : !faceRight;
        SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        var origin = new Vector2(texture.Width / 2f, texture.Height);
        var scale = new Vector2(drawWidth / texture.Width, drawHeight / texture.Height);

        // ---- dash trail ----

        if (State == CharacterState.Dash || _dashTimer > 0f)
        {
            for (int i = 0; i < _trail.Length; i++)
            {
                float age = _trailAge[i];
                if (age > 0.22f) continue;

                float fade = 1f - age / 0.22f;
                var ghostColour = new Color(150, 170, 255) * (fade * 0.35f);

                spriteBatch.Draw(texture,
                    new Vector2(_trail[i].X - cameraX, _trail[i].Y),
                    null, ghostColour, lean, origin, scale, effects, 0f);
            }
        }

        // ---- shadow ----

        // Shrinks and fades as the character rises, selling the height.
        float airHeight = MathHelper.Clamp((GroundY - Position.Y) / 260f, 0f, 1f);
        float shadowScale = 1f - airHeight * 0.45f;
        float shadowAlpha = 1f - airHeight * 0.55f;

        float shadowWidth = drawWidth * 0.5f * shadowScale;
        float shadowHeight = shadowWidth * 0.28f;

        spriteBatch.Draw(_shadow,
            new Rectangle(
                (int)(screenX - shadowWidth / 2f),
                (int)(GroundY - shadowHeight / 2f),
                (int)shadowWidth,
                (int)shadowHeight),
            Color.White * shadowAlpha);

        // ---- character ----

        spriteBatch.Draw(texture,
            new Vector2(screenX, Position.Y + bob),
            null, Color.White, lean, origin, scale, effects, 0f);
    }

    public void Dispose()
    {
        _side?.Dispose();
        _front?.Dispose();
        _shadow?.Dispose();
    }
}
