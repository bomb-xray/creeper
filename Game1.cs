using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

namespace CreeperGame;

public enum GameState
{
    IntroFadeIn,
    IntroWaiting,
    Transition,
    MainMenu
}

public enum MenuOption
{
    Play = 0,
    Options = 1,
    Exit = 2
}

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    // Textures
    private Texture2D? _introTexture;
    private Texture2D? _mainTexture;
    private Texture2D _pixel = null!;   // 1x1 white texture used for overlays/bars

    // Audio
    private SoundEffect? _clickSound;
    private SoundEffect? _musicSound;
    private SoundEffectInstance? _musicInstance;

    // State
    private GameState _state = GameState.IntroFadeIn;
    private MenuOption _selectedOption = MenuOption.Play;

    // Fade values (0..1)
    private float _introAlpha;
    private float _mainAlpha;
    private const float FadeSpeed = 0.8f;
    private float _transitionTimer;
    private const float TransitionDuration = 1.6f;

    // Blinking "press any key" text
    private float _blinkTimer;
    private bool _blinkVisible = true;

    // Menu reveal animation
    private float _menuAlpha;
    private readonly float[] _optionAlphas = new float[3];
    private const float OptionStaggerDelay = 0.18f;

    // "Coming soon" toast shown when PLAY is chosen
    private float _toastTimer;
    private string _toastText = string.Empty;

    // Options screen
    private bool _showingOptions;
    private int _optionsSelected;
    private float _masterVolume = 0.7f;

    // Custom pixel font
    private PixelFont _font = null!;

    // Input
    private KeyboardState _prevKeyboardState;
    private MouseState _prevMouseState;

    // Screen metrics
    private int _screenWidth;
    private int _screenHeight;

    // Font scale (pixels per font pixel), derived from the screen height so that
    // the UI keeps the same proportions on 768p and 1080p displays.
    private int _uiScale = 3;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        IsFixedTimeStep = true;
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = false;
        Window.Title = "CREEPER";

        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        _graphics.PreferredBackBufferWidth = display.Width;
        _graphics.PreferredBackBufferHeight = display.Height;
        // Borderless fullscreen: much safer than a hardware mode switch on old
        // GPUs/drivers, and alt-tab keeps working.
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.SynchronizeWithVerticalRetrace = true;

        try
        {
            _graphics.ApplyChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fullscreen failed, falling back to a window: {ex.Message}");
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
        }

        UpdateScreenMetrics();

        base.Initialize();
    }

    private void UpdateScreenMetrics()
    {
        _screenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
        _screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
        // 768p -> 3, 1080p -> 4, 1440p -> 5 ...
        _uiScale = Math.Max(2, (int)MathF.Round(_screenHeight / 260f));
        Console.WriteLine($"Screen: {_screenWidth}x{_screenHeight} (ui scale {_uiScale})");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = new PixelFont(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        string assetDir = FindAssetDirectory();
        LoadAssets(assetDir);
    }

    private string FindAssetDirectory()
    {
        string[] candidates =
        {
            "assets",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"),
            "../../../assets",
            "../../assets",
            "../assets"
        };

        foreach (string path in candidates)
        {
            if (Directory.Exists(path))
            {
                Console.WriteLine($"Assets directory: {Path.GetFullPath(path)}");
                return path;
            }
        }

        Console.WriteLine("WARNING: assets directory not found!");
        return "assets";
    }

    private void LoadAssets(string assetDir)
    {
        _introTexture = LoadTexture(assetDir, "image") ?? LoadTexture(assetDir, "negro");
        _mainTexture = LoadTexture(assetDir, "negro") ?? _introTexture;

        _clickSound = LoadSound(assetDir, "click");

        _musicSound = LoadSound(assetDir, "untrust");
        if (_musicSound != null)
        {
            try
            {
                _musicInstance = _musicSound.CreateInstance();
                _musicInstance.IsLooped = true;
                _musicInstance.Volume = _masterVolume;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not create the music instance: {ex.Message}");
                _musicInstance = null;
            }
        }
    }

    /// <summary>Loads an image, converting it to PNG first when the format is risky.</summary>
    private Texture2D? LoadTexture(string dir, string baseName)
    {
        string? imagePath = ImageConverter.EnsureLoadableImage(dir, baseName);
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Console.WriteLine($"No image found for '{baseName}'");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(imagePath);
            var texture = Texture2D.FromStream(GraphicsDevice, stream);
            Console.WriteLine($"Loaded image {imagePath} ({texture.Width}x{texture.Height})");
            return texture;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load image {imagePath}: {ex.Message}");

            // Last resort: force a re-encode through ImageSharp and try again.
            string repaired = Path.Combine(dir, baseName + "_converted.png");
            try
            {
                if (ImageConverter.ConvertToPng(imagePath, repaired))
                {
                    using var stream = File.OpenRead(repaired);
                    var texture = Texture2D.FromStream(GraphicsDevice, stream);
                    Console.WriteLine($"Loaded repaired image {repaired} ({texture.Width}x{texture.Height})");
                    return texture;
                }
            }
            catch (Exception inner)
            {
                Console.WriteLine($"Repair attempt failed: {inner.Message}");
            }

            return null;
        }
    }

    /// <summary>Loads audio, converting MP3/M4A/OGG/... to WAV because DesktopGL only decodes WAV.</summary>
    private SoundEffect? LoadSound(string dir, string baseName)
    {
        string? soundPath = AudioConverter.EnsurePlayableAudio(dir, baseName);
        if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
        {
            Console.WriteLine($"No audio found for '{baseName}'");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(soundPath);
            var sound = SoundEffect.FromStream(stream);
            Console.WriteLine($"Loaded audio {soundPath} ({sound.Duration.TotalSeconds:0.0}s)");
            return sound;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load audio {soundPath}: {ex.Message}");
            return null;
        }
    }

    private void PlayClick()
    {
        try
        {
            _clickSound?.Play(_masterVolume, 0f, 0f);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Click sound failed: {ex.Message}");
        }
    }

    private void PlayMusic()
    {
        if (_musicInstance == null) return;
        try
        {
            if (_musicInstance.State != SoundState.Playing)
            {
                _musicInstance.Volume = _masterVolume;
                _musicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Music playback failed: {ex.Message}");
            _musicInstance = null;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var kb = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (WasKeyPressed(kb, Keys.Escape))
        {
            // Inside the options panel ESC goes back, everywhere else it quits.
            if (_showingOptions) _showingOptions = false;
            else Exit();
        }

        if (WasKeyPressed(kb, Keys.F11))
        {
            ToggleFullscreen();
        }

        if (_toastTimer > 0f) _toastTimer -= dt;

        switch (_state)
        {
            case GameState.IntroFadeIn:
                UpdateIntroFadeIn(dt);
                break;
            case GameState.IntroWaiting:
                UpdateIntroWaiting(dt, kb, mouse);
                break;
            case GameState.Transition:
                UpdateTransition(dt);
                break;
            case GameState.MainMenu:
                UpdateMainMenu(dt, kb);
                break;
        }

        _prevKeyboardState = kb;
        _prevMouseState = mouse;
        base.Update(gameTime);
    }

    private void ToggleFullscreen()
    {
        _graphics.IsFullScreen = !_graphics.IsFullScreen;
        if (_graphics.IsFullScreen)
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = display.Width;
            _graphics.PreferredBackBufferHeight = display.Height;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
        }
        _graphics.ApplyChanges();
        UpdateScreenMetrics();
    }

    private void UpdateIntroFadeIn(float dt)
    {
        _introAlpha += FadeSpeed * dt;
        if (_introAlpha >= 1f)
        {
            _introAlpha = 1f;
            _state = GameState.IntroWaiting;
        }
    }

    private void UpdateIntroWaiting(float dt, KeyboardState kb, MouseState mouse)
    {
        _blinkTimer += dt;
        if (_blinkTimer >= 0.55f)
        {
            _blinkTimer = 0f;
            _blinkVisible = !_blinkVisible;
        }

        bool anyKey = kb.GetPressedKeys().Length > 0 && _prevKeyboardState.GetPressedKeys().Length == 0;
        bool anyClick = mouse.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;

        if (anyKey || anyClick)
        {
            PlayClick();
            _state = GameState.Transition;
            _transitionTimer = 0f;
        }
    }

    private void UpdateTransition(float dt)
    {
        _transitionTimer += dt;

        // First image fades out over the first 60% of the transition.
        float outProgress = MathF.Min(1f, _transitionTimer / (TransitionDuration * 0.6f));
        _introAlpha = 1f - outProgress;

        // Second image starts fading in slightly before the first one is gone.
        const float mainFadeStart = TransitionDuration * 0.35f;
        if (_transitionTimer > mainFadeStart)
        {
            _mainAlpha = MathF.Min(1f, (_transitionTimer - mainFadeStart) / (TransitionDuration - mainFadeStart));
            PlayMusic();
        }

        if (_transitionTimer >= TransitionDuration)
        {
            _introAlpha = 0f;
            _mainAlpha = 1f;
            _state = GameState.MainMenu;
            _menuAlpha = 0f;
            Array.Clear(_optionAlphas, 0, _optionAlphas.Length);
        }
    }

    private void UpdateMainMenu(float dt, KeyboardState kb)
    {
        PlayMusic();

        if (_showingOptions)
        {
            UpdateOptionsScreen(kb);
            return;
        }

        _menuAlpha = MathF.Min(2f, _menuAlpha + dt * 1.6f);
        for (int i = 0; i < _optionAlphas.Length; i++)
        {
            float delay = i * OptionStaggerDelay;
            if (_menuAlpha > delay)
            {
                _optionAlphas[i] = MathF.Min(1f, (_menuAlpha - delay) * 2.5f);
            }
        }

        if (WasKeyPressed(kb, Keys.Up) || WasKeyPressed(kb, Keys.W))
        {
            _selectedOption = (MenuOption)(((int)_selectedOption + 2) % 3);
            PlayClick();
        }

        if (WasKeyPressed(kb, Keys.Down) || WasKeyPressed(kb, Keys.S))
        {
            _selectedOption = (MenuOption)(((int)_selectedOption + 1) % 3);
            PlayClick();
        }

        if (WasKeyPressed(kb, Keys.Enter) || WasKeyPressed(kb, Keys.Space) || WasKeyPressed(kb, Keys.Z))
        {
            PlayClick();
            switch (_selectedOption)
            {
                case MenuOption.Play:
                    ShowToast("COMING SOON");
                    break;
                case MenuOption.Options:
                    _showingOptions = true;
                    _optionsSelected = 0;
                    break;
                case MenuOption.Exit:
                    Exit();
                    break;
            }
        }
    }

    private void ShowToast(string text)
    {
        _toastText = text;
        _toastTimer = 1.8f;
    }

    private void UpdateOptionsScreen(KeyboardState kb)
    {
        const int optionCount = 3; // volume, fullscreen, back

        if (WasKeyPressed(kb, Keys.Up) || WasKeyPressed(kb, Keys.W))
        {
            _optionsSelected = (_optionsSelected - 1 + optionCount) % optionCount;
            PlayClick();
        }

        if (WasKeyPressed(kb, Keys.Down) || WasKeyPressed(kb, Keys.S))
        {
            _optionsSelected = (_optionsSelected + 1) % optionCount;
            PlayClick();
        }

        bool left = WasKeyPressed(kb, Keys.Left) || WasKeyPressed(kb, Keys.A);
        bool right = WasKeyPressed(kb, Keys.Right) || WasKeyPressed(kb, Keys.D);
        bool confirm = WasKeyPressed(kb, Keys.Enter) || WasKeyPressed(kb, Keys.Space) || WasKeyPressed(kb, Keys.Z);

        switch (_optionsSelected)
        {
            case 0: // Master volume
                if (left || right)
                {
                    _masterVolume = MathHelper.Clamp(_masterVolume + (right ? 0.1f : -0.1f), 0f, 1f);
                    if (_musicInstance != null) _musicInstance.Volume = _masterVolume;
                    PlayClick();
                }
                break;

            case 1: // Fullscreen toggle
                if (left || right || confirm)
                {
                    PlayClick();
                    ToggleFullscreen();
                }
                break;

            case 2: // Back
                if (confirm)
                {
                    PlayClick();
                    _showingOptions = false;
                }
                break;
        }
    }

    private bool WasKeyPressed(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && _prevKeyboardState.IsKeyUp(key);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // NonPremultiplied: Texture2D.FromStream does not premultiply alpha, and the
        // translucent overlays below rely on straight alpha too.
        // PointClamp keeps the pixel font crisp instead of blurry.
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);

        switch (_state)
        {
            case GameState.IntroFadeIn:
            case GameState.IntroWaiting:
                DrawIntroScreen();
                break;
            case GameState.Transition:
                DrawTransitionScreen();
                break;
            case GameState.MainMenu:
                DrawMainMenuScreen();
                break;
        }

        DrawToast();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>Draws a texture stretched over the whole screen.</summary>
    private void DrawStretched(Texture2D texture, float alpha)
    {
        if (texture == null || alpha <= 0f) return;
        _spriteBatch.Draw(texture, new Rectangle(0, 0, _screenWidth, _screenHeight), Tint(alpha));
    }

    /// <summary>White tint with the given opacity (straight alpha, RGB untouched).</summary>
    private static Color Tint(float alpha) => WithAlpha(Color.White, alpha);

    /// <summary>Copies a color and scales only its alpha channel.</summary>
    private static Color WithAlpha(Color color, float alpha)
    {
        color.A = (byte)MathHelper.Clamp(alpha * 255f, 0f, 255f);
        return color;
    }

    private void DrawIntroScreen()
    {
        if (_introTexture == null) return;

        // Leave room at the bottom for the prompt.
        float reserved = _screenHeight * 0.12f;
        float scale = MathF.Min((float)_screenWidth / _introTexture.Width,
                                (_screenHeight - reserved) / _introTexture.Height);
        int width = (int)(_introTexture.Width * scale);
        int height = (int)(_introTexture.Height * scale);
        int x = (_screenWidth - width) / 2;
        int y = (int)((_screenHeight - reserved - height) / 2f);

        _spriteBatch.Draw(_introTexture, new Rectangle(x, y, width, height), Tint(_introAlpha));

        if (_state == GameState.IntroWaiting && _blinkVisible)
        {
            int promptY = Math.Min(y + height + _uiScale * 10, _screenHeight - _uiScale * 10);
            _font.DrawTextShadowed(_spriteBatch, "PRESS ANY KEY TO CONTINUE",
                _screenWidth / 2, promptY, _uiScale,
                WithAlpha(new Color(230, 230, 230), _introAlpha), true);
        }
    }

    private void DrawTransitionScreen()
    {
        if (_introTexture != null && _introAlpha > 0f)
        {
            float reserved = _screenHeight * 0.12f;
            float scale = MathF.Min((float)_screenWidth / _introTexture.Width,
                                    (_screenHeight - reserved) / _introTexture.Height);
            int width = (int)(_introTexture.Width * scale);
            int height = (int)(_introTexture.Height * scale);
            _spriteBatch.Draw(_introTexture,
                new Rectangle((_screenWidth - width) / 2, (int)((_screenHeight - reserved - height) / 2f), width, height),
                Tint(_introAlpha));
        }

        if (_mainTexture != null && _mainAlpha > 0f)
        {
            DrawStretched(_mainTexture, _mainAlpha);
        }
    }

    private void DrawMainMenuScreen()
    {
        if (_mainTexture != null)
        {
            DrawStretched(_mainTexture, _mainAlpha);
        }

        if (_showingOptions)
        {
            DrawOptionsScreen();
            return;
        }

        string[] labels = { "PLAY", "OPTIONS", "EXIT" };
        int menuScale = _uiScale + 1;
        int spacing = menuScale * 16;
        // The menu sits directly on the artwork; the drop shadow on each glyph
        // is what keeps it readable, so no backdrop is drawn.
        int menuStartY = _screenHeight - spacing * 3;

        for (int i = 0; i < labels.Length; i++)
        {
            float alpha = _optionAlphas[i];
            if (alpha <= 0f) continue;

            bool selected = i == (int)_selectedOption;
            Color color = selected
                ? WithAlpha(new Color(220, 40, 40), alpha)
                : WithAlpha(new Color(205, 205, 205), alpha * 0.85f);

            int y = menuStartY + i * spacing;
            _font.DrawTextShadowed(_spriteBatch, labels[i], _screenWidth / 2, y, menuScale, color, true);

            if (selected)
            {
                int halfWidth = _font.MeasureText(labels[i], menuScale) / 2;
                int arrowGap = menuScale * 7;
                _font.DrawTextShadowed(_spriteBatch, ">", _screenWidth / 2 - halfWidth - arrowGap, y, menuScale, color, true);
                _font.DrawTextShadowed(_spriteBatch, "<", _screenWidth / 2 + halfWidth + arrowGap, y, menuScale, color, true);
            }
        }
    }

    private void DrawOptionsScreen()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _screenWidth, _screenHeight), new Color(0, 0, 0, 215));

        int titleScale = _uiScale + 2;
        int itemScale = _uiScale + 1;
        int hintScale = Math.Max(2, _uiScale - 1);
        _font.DrawTextShadowed(_spriteBatch, "OPTIONS", _screenWidth / 2, (int)(_screenHeight * 0.22f), titleScale, Color.White, true);

        string[] labels =
        {
            $"VOLUME  {BuildVolumeBar()}",
            $"FULLSCREEN  {(_graphics.IsFullScreen ? "ON" : "OFF")}",
            "BACK"
        };

        int startY = (int)(_screenHeight * 0.42f);
        int spacing = itemScale * 16;

        for (int i = 0; i < labels.Length; i++)
        {
            bool selected = i == _optionsSelected;
            Color color = selected ? new Color(220, 40, 40) : new Color(200, 200, 200);
            int y = startY + i * spacing;

            _font.DrawTextShadowed(_spriteBatch, labels[i], _screenWidth / 2, y, itemScale, color, true);

            if (selected)
            {
                int halfWidth = _font.MeasureText(labels[i], itemScale) / 2;
                int arrowGap = itemScale * 7;
                _font.DrawTextShadowed(_spriteBatch, ">", _screenWidth / 2 - halfWidth - arrowGap, y, itemScale, color, true);
                _font.DrawTextShadowed(_spriteBatch, "<", _screenWidth / 2 + halfWidth + arrowGap, y, itemScale, color, true);
            }
        }

        _font.DrawTextShadowed(_spriteBatch, "LEFT / RIGHT - CHANGE    ESC - BACK",
            _screenWidth / 2, _screenHeight - hintScale * 12, hintScale,
            new Color(150, 150, 150), true);
    }

    private string BuildVolumeBar()
    {
        int filled = (int)MathF.Round(_masterVolume * 10f);
        return "[" + new string('#', filled) + new string('-', 10 - filled) + "]";
    }

    private void DrawToast()
    {
        if (_toastTimer <= 0f) return;

        float alpha = MathF.Min(1f, _toastTimer / 0.5f);
        int y = (int)(_screenHeight * 0.62f);
        _font.DrawTextShadowed(_spriteBatch, _toastText, _screenWidth / 2, y, _uiScale + 1,
            WithAlpha(new Color(255, 230, 120), alpha), true);
    }

    protected override void UnloadContent()
    {
        _musicInstance?.Stop();
        _musicInstance?.Dispose();
        _musicSound?.Dispose();
        _clickSound?.Dispose();
        _introTexture?.Dispose();
        if (!ReferenceEquals(_mainTexture, _introTexture)) _mainTexture?.Dispose();
        _pixel?.Dispose();
        _font?.Dispose();

        base.UnloadContent();
    }
}
