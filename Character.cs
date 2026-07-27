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
/// The player: a winged knight assembled from hand-authored pixel parts.
///
/// The art lives in <see cref="KnightArt"/> as character strings, so no image
/// files are needed and every part can be posed independently. Animation is done
/// the way pixel art demands: whole-pixel offsets and frame swaps, never rotation
/// or fractional scaling, both of which would smear the pixels.
///
/// Parts are drawn back to front: far wing, cape, far arm, legs, torso, head,
/// near arm, sword, near wing.
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

    /// <summary>
    /// Height of the whole knight in art pixels, used to derive the integer draw
    /// scale from the requested on-screen height.
    /// Legs 11 + torso 13 - 2 overlap + head 11 - 2 overlap + 3 of plume = 34.
    /// </summary>
    private const int ArtHeight = 34;

    // ---- sprites -----------------------------------------------------------

    private readonly PixelSprite _head;
    private readonly PixelSprite _plume;
    private readonly PixelSprite _torso;
    private readonly PixelSprite _armNear;
    private readonly PixelSprite _armFar;
    private readonly PixelSprite _sword;

    private readonly PixelSprite _legsIdle;
    private readonly PixelSprite[] _legsWalk;
    private readonly PixelSprite _legsCrouch;
    private readonly PixelSprite _legsJump;
    private readonly PixelSprite _legsFall;

    private readonly PixelSprite _capeRest;
    private readonly PixelSprite _capeDrift;
    private readonly PixelSprite _capeStream;

    private readonly PixelSprite _wingFolded;
    private readonly PixelSprite _wingOpen;
    private readonly PixelSprite _wingSpread;

    private readonly Texture2D _shadow;

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

    /// <summary>The art is generated, so there is always something to draw.</summary>
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

    /// <summary>Wing beat strength, spiking on take-off and decaying.</summary>
    private float _wingBeat;

    private readonly Vector2[] _trail = new Vector2[5];
    private readonly float[] _trailAge = new float[5];
    private int _trailIndex;
    private float _trailTimer;

    public Character(GraphicsDevice device, string assetDir)
    {
        var p = KnightArt.Palette;

        _head = new PixelSprite(device, KnightArt.Head, p);
        _plume = new PixelSprite(device, KnightArt.Plume, p);
        _torso = new PixelSprite(device, KnightArt.Torso, p);
        _armNear = new PixelSprite(device, KnightArt.ArmNear, p);
        _armFar = new PixelSprite(device, KnightArt.ArmFar, p);
        _sword = new PixelSprite(device, KnightArt.Sword, p);

        _legsIdle = new PixelSprite(device, KnightArt.LegsIdle, p);
        _legsWalk = new[]
        {
            new PixelSprite(device, KnightArt.LegsWalk0, p),
            new PixelSprite(device, KnightArt.LegsWalk1, p),
            new PixelSprite(device, KnightArt.LegsWalk2, p),
            new PixelSprite(device, KnightArt.LegsWalk1, p)
        };
        _legsCrouch = new PixelSprite(device, KnightArt.LegsCrouch, p);
        _legsJump = new PixelSprite(device, KnightArt.LegsJump, p);
        _legsFall = new PixelSprite(device, KnightArt.LegsFall, p);

        _capeRest = new PixelSprite(device, KnightArt.CapeRest, p);
        _capeDrift = new PixelSprite(device, KnightArt.CapeDrift, p);
        _capeStream = new PixelSprite(device, KnightArt.CapeStream, p);

        _wingFolded = new PixelSprite(device, KnightArt.WingFolded, p);
        _wingOpen = new PixelSprite(device, KnightArt.WingOpen, p);
        _wingSpread = new PixelSprite(device, KnightArt.WingSpread, p);

        _shadow = CreateShadowTexture(device, 64);

        for (int i = 0; i < _trailAge.Length; i++) _trailAge[i] = float.MaxValue;

        Console.WriteLine("Knight built from pixel art data (no image files needed).");
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
            _trailTimer = 0.025f;
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
            _wingBeat = 1f;
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
        float speedRatio = MathF.Abs(Velocity.X) / WalkSpeed;

        // Tying the cycle to real speed stops the feet from sliding.
        _walkCycle += State == CharacterState.Walk
            ? dt * 9f * MathF.Max(0.45f, speedRatio)
            : dt * 2f;

        _breathCycle += dt * 1.7f;
        _wingBeat = MathHelper.Lerp(_wingBeat, 0f, MathF.Min(1f, 3f * dt));

        for (int i = 0; i < _trailAge.Length; i++)
        {
            if (_trailAge[i] < float.MaxValue) _trailAge[i] += dt;
        }
    }

    /// <summary>Chooses the leg bitmap for the current state.</summary>
    private PixelSprite CurrentLegs() => State switch
    {
        CharacterState.Walk => _legsWalk[WalkFrame()],
        CharacterState.Crouch => _legsCrouch,
        CharacterState.Jump => _legsJump,
        CharacterState.Fall => _legsFall,
        CharacterState.Dash => _legsJump,
        _ => _legsIdle
    };

    /// <summary>Current walk frame, wrapped safely for any cycle value.</summary>
    private int WalkFrame()
    {
        int frame = (int)MathF.Floor(_walkCycle) % _legsWalk.Length;
        return frame < 0 ? frame + _legsWalk.Length : frame;
    }

    /// <summary>The cape trails further the faster the knight travels.</summary>
    private PixelSprite CurrentCape()
    {
        if (State == CharacterState.Dash) return _capeStream;

        float speed = MathF.Abs(Velocity.X) / WalkSpeed;
        if (!OnGround || speed > 0.55f) return _capeStream;
        if (speed > 0.15f) return _capeDrift;

        // A slow flutter while standing still.
        return MathF.Sin(_breathCycle * 0.7f) > 0.6f ? _capeDrift : _capeRest;
    }

    /// <summary>Wings open up in the air and beat on take-off.</summary>
    private PixelSprite CurrentWing()
    {
        if (_wingBeat > 0.45f || State == CharacterState.Dash) return _wingSpread;
        if (!OnGround) return _wingOpen;
        return _wingFolded;
    }

    public void Draw(SpriteBatch spriteBatch, float cameraX)
    {
        // Whole-number scale only: anything else destroys the pixel grid.
        int scale = Math.Max(1, (int)MathF.Round(DisplayHeight / ArtHeight));

        float screenXf = Position.X - cameraX;

        // ---- dash trail ----

        if (_dashTimer > 0f)
        {
            for (int i = 0; i < _trail.Length; i++)
            {
                float age = _trailAge[i];
                if (age > 0.20f) continue;

                float fade = 1f - age / 0.20f;
                DrawKnight(spriteBatch,
                    (int)MathF.Round(_trail[i].X - cameraX),
                    (int)MathF.Round(_trail[i].Y),
                    scale, new Color(140, 165, 255) * (fade * 0.35f));
            }
        }

        // ---- shadow ----

        float airHeight = MathHelper.Clamp((GroundY - Position.Y) / 260f, 0f, 1f);
        float shadowScale = 1f - airHeight * 0.45f;
        float shadowAlpha = 1f - airHeight * 0.55f;
        float shadowWidth = ArtHeight * scale * 0.40f * shadowScale;
        float shadowHeight = shadowWidth * 0.26f;

        spriteBatch.Draw(_shadow,
            new Rectangle(
                (int)(screenXf - shadowWidth / 2f),
                (int)(GroundY - shadowHeight / 2f),
                (int)shadowWidth,
                (int)shadowHeight),
            Color.White * shadowAlpha);

        // ---- knight ----

        DrawKnight(spriteBatch, (int)MathF.Round(screenXf),
            (int)MathF.Round(Position.Y), scale, Color.White);
    }

    /// <summary>
    /// Lays out every part relative to the feet. All offsets are in art pixels
    /// and multiplied by the scale, which keeps the pixel grid intact.
    /// </summary>
    private void DrawKnight(SpriteBatch spriteBatch, int footX, int footY, int scale, Color tint)
    {
        bool flip = FacingSign < 0;

        // A one-pixel bob, applied in art pixels so it stays on the grid.
        int bob = State switch
        {
            CharacterState.Walk => MathF.Sin(_walkCycle * MathF.PI / 2f) > 0.5f ? -1 : 0,
            CharacterState.Idle => MathF.Sin(_breathCycle) > 0.7f ? -1 : 0,
            _ => 0
        };

        // Crouching drops the whole upper body.
        int crouchDrop = State == CharacterState.Crouch ? 5 : 0;

        PixelSprite legs = CurrentLegs();
        PixelSprite cape = CurrentCape();
        PixelSprite wing = CurrentWing();

        // Vertical layout, measured up from the feet.
        int legsTop = -legs.Height;
        int torsoTop = legsTop - _torso.Height + 2 + crouchDrop;
        int headTop = torsoTop - _head.Height + 2;

        // Helper that converts art-space offsets into screen pixels, mirroring
        // horizontally when the knight faces left.
        void Blit(PixelSprite sprite, int offsetX, int offsetY)
        {
            int x = flip
                ? footX - (offsetX + sprite.Width) * scale
                : footX + offsetX * scale;

            sprite.Draw(spriteBatch, x, footY + (offsetY + bob) * scale, scale, flip, tint);
        }

        // ---- back to front ----

        // Far wing, behind everything, offset back and up.
        Blit(wing, -13, torsoTop - 3);

        // Cape hangs from the shoulders.
        Blit(cape, -7, torsoTop + 2);

        // Far arm sits behind the torso.
        Blit(_armFar, -1, torsoTop + 3);

        Blit(legs, -7, legsTop);
        Blit(_torso, -6, torsoTop);

        // Head, with the plume angled back over it.
        Blit(_head, -5, headTop);
        Blit(_plume, -9, headTop - 3);

        // Near arm and the blade it holds, in front of the torso.
        Blit(_armNear, 2, torsoTop + 3);

        // The sword is held point-up, hilt at the gauntlet.
        Blit(_sword, 4, torsoTop - 9);
    }

    public void Dispose()
    {
        _head?.Dispose();
        _plume?.Dispose();
        _torso?.Dispose();
        _armNear?.Dispose();
        _armFar?.Dispose();
        _sword?.Dispose();

        _legsIdle?.Dispose();
        if (_legsWalk != null)
        {
            foreach (PixelSprite sprite in _legsWalk) sprite?.Dispose();
        }
        _legsCrouch?.Dispose();
        _legsJump?.Dispose();
        _legsFall?.Dispose();

        _capeRest?.Dispose();
        _capeDrift?.Dispose();
        _capeStream?.Dispose();

        _wingFolded?.Dispose();
        _wingOpen?.Dispose();
        _wingSpread?.Dispose();

        _shadow?.Dispose();
    }
}
