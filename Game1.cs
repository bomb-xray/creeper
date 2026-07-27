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
    MainMenu,
    FileSelect,
    OpeningVideo,
    Playing
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

    // File select (three save slots)
    private SaveSlots _saves = null!;
    private int _slotSelected;
    private float _fileSelectAlpha;

    // Opening video, played once when a brand new run is started
    private VideoPlayback? _video;
    private float _videoFadeAlpha;
    private string _assetDir = "assets";

    // The playable side-scrolling scene, entered from a save slot
    private GameScene? _scene;

    // Text rendering (TrueType from assets, pixel font fallback)
    private TextRenderer _text = null!;

    // Input
    private KeyboardState _prevKeyboardState;
    private MouseState _prevMouseState;

    // Screen metrics
    private int _screenWidth;
    private int _screenHeight;

    // Base text height in screen pixels, derived from the screen height so the UI
    // keeps the same proportions on 768p and 1080p displays.
    private float _baseTextSize = 24f;

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
        // ~3.1% of the screen height: 24px at 768p, 34px at 1080p.
        _baseTextSize = MathF.Max(14f, _screenHeight * 0.031f);
        Console.WriteLine($"Screen: {_screenWidth}x{_screenHeight} (base text {_baseTextSize:0}px)");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _assetDir = FindAssetDirectory();
        FFmpeg.AssetDirectory = _assetDir;

        // Picks up a .ttf/.otf from the assets folder, otherwise falls back to
        // the built-in pixel font.
        _text = new TextRenderer(GraphicsDevice, _assetDir);

        _saves = new SaveSlots(_assetDir);

        LoadAssets(_assetDir);
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
            HandleEscape();
        }

        if (WasKeyPressed(kb, Keys.F11))
        {
            ToggleFullscreen();
        }

        // F1 jumps straight into the playable scene, for checking the art.
        if (WasKeyPressed(kb, Keys.F1))
        {
            PlayClick();
            if (_state == GameState.Playing) LeavePlaying();
            else EnterPlaying();
        }

        if (_toastTimer > 0f) _toastTimer -= dt;

        // Shared blink used by the intro prompt and the coming-soon card.
        _blinkTimer += dt;
        if (_blinkTimer >= 0.55f)
        {
            _blinkTimer = 0f;
            _blinkVisible = !_blinkVisible;
        }

        switch (_state)
        {
            case GameState.IntroFadeIn:
                UpdateIntroFadeIn(dt);
                break;
            case GameState.IntroWaiting:
                UpdateIntroWaiting(kb, mouse);
                break;
            case GameState.Transition:
                UpdateTransition(dt);
                break;
            case GameState.MainMenu:
                UpdateMainMenu(dt, kb);
                break;
            case GameState.FileSelect:
                UpdateFileSelect(dt, kb);
                break;
            case GameState.OpeningVideo:
                UpdateOpeningVideo(dt, kb);
                break;
            case GameState.Playing:
                UpdatePlaying(dt, kb);
                break;
        }

        _prevKeyboardState = kb;
        _prevMouseState = mouse;
        base.Update(gameTime);
    }

    /// <summary>ESC steps back one screen, and quits only from the main menu.</summary>
    private void HandleEscape()
    {
        if (_showingOptions)
        {
            _showingOptions = false;
            return;
        }

        switch (_state)
        {
            case GameState.FileSelect:
                PlayClick();
                _state = GameState.MainMenu;
                break;

            case GameState.OpeningVideo:
                // Skipping the cutscene drops straight into the game.
                StopVideo();
                EnterPlaying();
                break;

            case GameState.Playing:
                LeavePlaying();
                break;

            default:
                Exit();
                break;
        }
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
        _scene?.Resize(_screenWidth, _screenHeight);
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

    private void UpdateIntroWaiting(KeyboardState kb, MouseState mouse)
    {
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
                    _state = GameState.FileSelect;
                    _slotSelected = 0;
                    _fileSelectAlpha = 0f;
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

    // ---------------------------------------------------------------- file select

    private void UpdateFileSelect(float dt, KeyboardState kb)
    {
        PlayMusic();
        _fileSelectAlpha = MathF.Min(1f, _fileSelectAlpha + dt * 4f);

        if (WasKeyPressed(kb, Keys.Up) || WasKeyPressed(kb, Keys.W))
        {
            _slotSelected = (_slotSelected - 1 + SaveSlots.SlotCount) % SaveSlots.SlotCount;
            PlayClick();
        }

        if (WasKeyPressed(kb, Keys.Down) || WasKeyPressed(kb, Keys.S))
        {
            _slotSelected = (_slotSelected + 1) % SaveSlots.SlotCount;
            PlayClick();
        }

        // Erasing a slot frees it up again for a fresh run.
        if (WasKeyPressed(kb, Keys.Delete) && _saves[_slotSelected].Exists)
        {
            PlayClick();
            _saves.Erase(_slotSelected);
            ShowToast($"SLOT {_slotSelected + 1} ERASED");
        }

        if (WasKeyPressed(kb, Keys.Enter) || WasKeyPressed(kb, Keys.Space) || WasKeyPressed(kb, Keys.Z))
        {
            PlayClick();
            SelectSlot(_slotSelected);
        }
    }

    /// <summary>
    /// A brand new run plays the opening video first; an existing one goes
    /// straight to the end card.
    /// </summary>
    private void SelectSlot(int index)
    {
        bool isNewGame = !_saves[index].Exists;

        if (isNewGame)
        {
            Console.WriteLine($"Starting a new run in slot {index + 1}");
            _saves.CreateNew(index);
            StartOpeningVideo();
        }
        else
        {
            Console.WriteLine($"Continuing save slot {index + 1}: {_saves[index].Name}");
            EnterPlaying();
        }
    }

    // -------------------------------------------------------------- opening video

    private void StartOpeningVideo()
    {
        string? videoPath = FindOpeningVideo();

        if (videoPath == null)
        {
            Console.WriteLine("No opening video found in assets; going straight into the game.");
            EnterPlaying();
            return;
        }

        // The video carries its own audio, so the menu music steps aside.
        try { _musicInstance?.Pause(); } catch { /* not fatal */ }

        _video = VideoPlayback.TryStart(GraphicsDevice, videoPath, _screenWidth, _screenHeight, _masterVolume);

        if (_video == null)
        {
            // ffmpeg missing or refused to start: do not punish the player.
            Console.WriteLine("Opening video skipped; going straight into the game.");
            EnterPlaying();
            return;
        }

        _videoFadeAlpha = 0f;
        _state = GameState.OpeningVideo;
    }

    /// <summary>Looks for an opening video under a few conventional names.</summary>
    private string? FindOpeningVideo()
    {
        string[] names = { "opening", "intro", "opening_video", "cutscene" };
        string[] extensions = { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v", ".wmv" };

        foreach (string name in names)
        {
            foreach (string ext in extensions)
            {
                string path = Path.Combine(_assetDir, name + ext);
                if (File.Exists(path)) return path;
            }
        }

        return null;
    }

    private void UpdateOpeningVideo(float dt, KeyboardState kb)
    {
        if (_video == null)
        {
            EnterPlaying();
            return;
        }

        _video.Update();

        // Fade the first frame in so playback does not start with a hard cut.
        _videoFadeAlpha = MathF.Min(1f, _videoFadeAlpha + dt * 2f);

        // Any key skips the cutscene.
        bool skip = kb.GetPressedKeys().Length > 0 && _prevKeyboardState.GetPressedKeys().Length == 0;

        if (skip || _video.Finished)
        {
            StopVideo();
            EnterPlaying();
        }
    }

    private void StopVideo()
    {
        if (_video == null) return;

        _video.Dispose();
        _video = null;

        try { _musicInstance?.Resume(); } catch { /* not fatal */ }
    }

    private void ReturnToMenu()
    {
        PlayClick();
        StopVideo();
        _state = GameState.MainMenu;
    }

    // ----------------------------------------------------------------- playing

    /// <summary>Builds the scene on first entry and switches to it.</summary>
    private void EnterPlaying()
    {
        if (_scene == null)
        {
            _scene = new GameScene(GraphicsDevice, _assetDir, _screenWidth, _screenHeight);

            if (!_scene.HasArt)
            {
                Console.WriteLine("No scene art found; the playable area will be mostly empty.");
            }
        }

        // The scene has its own mood, so the menu music steps aside.
        try { _musicInstance?.Pause(); } catch { /* not fatal */ }

        _state = GameState.Playing;
    }

    private void LeavePlaying()
    {
        PlayClick();
        try { _musicInstance?.Resume(); } catch { /* not fatal */ }
        _state = GameState.MainMenu;
    }

    private void UpdatePlaying(float dt, KeyboardState kb)
    {
        _scene?.Update(dt, kb);
    }

    private void DrawPlayingScreen()
    {
        if (_scene == null) return;

        _scene.Draw(_spriteBatch);

        float hintSize = _baseTextSize * 0.62f;

        // Report exactly what was searched and what was there, which is far more
        // useful than a generic "not found".
        if (_scene.MissingArt.Count > 0)
        {
            float y = _screenHeight * 0.10f;

            _text.DrawShadowed(_spriteBatch, "MISSING ART",
                _screenWidth / 2f, y, _baseTextSize * 1.1f,
                new Color(220, 40, 40), true);

            y += _baseTextSize * 1.6f;
            _text.DrawShadowed(_spriteBatch,
                "MISSING: " + string.Join("  ", _scene.MissingArt).ToUpperInvariant(),
                _screenWidth / 2f, y, hintSize, new Color(230, 170, 90), true);

            y += _baseTextSize * 1.5f;
            _text.DrawShadowed(_spriteBatch, "LOOKED IN:",
                _screenWidth / 2f, y, hintSize * 0.9f, new Color(140, 140, 140), true);

            y += _baseTextSize;
            _text.DrawShadowed(_spriteBatch, _scene.AssetPath,
                _screenWidth / 2f, y, hintSize * 0.9f, new Color(180, 180, 180), true);

            y += _baseTextSize * 1.5f;
            _text.DrawShadowed(_spriteBatch, "IMAGES FOUND THERE:",
                _screenWidth / 2f, y, hintSize * 0.9f, new Color(140, 140, 140), true);

            y += _baseTextSize;
            string found = _scene.FoundFiles.Count > 0
                ? string.Join("  ", _scene.FoundFiles)
                : "(NONE)";

            // Wrap the list so a long folder listing stays on screen.
            foreach (string line in WrapText(found, 58))
            {
                _text.DrawShadowed(_spriteBatch, line, _screenWidth / 2f, y,
                    hintSize * 0.9f, new Color(180, 180, 180), true);
                y += _baseTextSize;
            }
        }

        _text.DrawShadowed(_spriteBatch,
            "ARROWS - MOVE    SPACE - JUMP    S - CROUCH    RIGHT SHIFT - DASH    ESC - MENU",
            _screenWidth / 2f, _screenHeight - hintSize * 2f, hintSize,
            new Color(210, 210, 210), true);
    }

    /// <summary>Splits text into lines of at most maxChars, breaking on spaces.</summary>
    private static System.Collections.Generic.List<string> WrapText(string text, int maxChars)
    {
        var lines = new System.Collections.Generic.List<string>();
        string current = string.Empty;

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > maxChars)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = current.Length == 0 ? word : current + " " + word;
            }
        }

        if (current.Length > 0) lines.Add(current);
        if (lines.Count == 0) lines.Add(string.Empty);
        return lines;
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
            case GameState.FileSelect:
                DrawFileSelectScreen();
                break;
            case GameState.OpeningVideo:
                DrawOpeningVideoScreen();
                break;
            case GameState.Playing:
                DrawPlayingScreen();
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
            float promptSize = _baseTextSize;
            int promptY = (int)MathF.Min(y + height + promptSize, _screenHeight - promptSize);
            _text.DrawShadowed(_spriteBatch, "PRESS ANY KEY TO CONTINUE",
                _screenWidth / 2f, promptY, promptSize,
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
        float menuSize = _baseTextSize * 1.35f;
        float spacing = menuSize * 1.6f;
        // The menu sits directly on the artwork; the drop shadow on each glyph
        // is what keeps it readable, so no backdrop is drawn.
        float menuStartY = _screenHeight - spacing * 3f;

        for (int i = 0; i < labels.Length; i++)
        {
            float alpha = _optionAlphas[i];
            if (alpha <= 0f) continue;

            bool selected = i == (int)_selectedOption;
            Color color = selected
                ? WithAlpha(new Color(220, 40, 40), alpha)
                : WithAlpha(new Color(205, 205, 205), alpha * 0.85f);

            float y = menuStartY + i * spacing;
            _text.DrawShadowed(_spriteBatch, labels[i], _screenWidth / 2f, y, menuSize, color, true);

            if (selected)
            {
                float halfWidth = _text.Measure(labels[i], menuSize).X / 2f;
                float arrowGap = menuSize * 0.9f;
                _text.DrawShadowed(_spriteBatch, ">", _screenWidth / 2f - halfWidth - arrowGap, y, menuSize, color, true);
                _text.DrawShadowed(_spriteBatch, "<", _screenWidth / 2f + halfWidth + arrowGap, y, menuSize, color, true);
            }
        }
    }

    private void DrawOptionsScreen()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _screenWidth, _screenHeight), new Color(0, 0, 0, 215));

        float titleSize = _baseTextSize * 1.8f;
        float itemSize = _baseTextSize * 1.2f;
        float hintSize = _baseTextSize * 0.8f;
        _text.DrawShadowed(_spriteBatch, "OPTIONS", _screenWidth / 2f, _screenHeight * 0.22f, titleSize, Color.White, true);

        string[] labels =
        {
            $"VOLUME  {BuildVolumeBar()}",
            $"FULLSCREEN  {(_graphics.IsFullScreen ? "ON" : "OFF")}",
            "BACK"
        };

        float startY = _screenHeight * 0.42f;
        float spacing = itemSize * 1.9f;

        for (int i = 0; i < labels.Length; i++)
        {
            bool selected = i == _optionsSelected;
            Color color = selected ? new Color(220, 40, 40) : new Color(200, 200, 200);
            float y = startY + i * spacing;

            _text.DrawShadowed(_spriteBatch, labels[i], _screenWidth / 2f, y, itemSize, color, true);

            if (selected)
            {
                float halfWidth = _text.Measure(labels[i], itemSize).X / 2f;
                float arrowGap = itemSize * 0.9f;
                _text.DrawShadowed(_spriteBatch, ">", _screenWidth / 2f - halfWidth - arrowGap, y, itemSize, color, true);
                _text.DrawShadowed(_spriteBatch, "<", _screenWidth / 2f + halfWidth + arrowGap, y, itemSize, color, true);
            }
        }

        _text.DrawShadowed(_spriteBatch, "LEFT / RIGHT - CHANGE    ESC - BACK",
            _screenWidth / 2f, _screenHeight - hintSize * 2.5f, hintSize,
            new Color(150, 150, 150), true);
    }

    private void DrawFileSelectScreen()
    {
        // Keep the menu artwork visible but pushed back behind the panel.
        if (_mainTexture != null) DrawStretched(_mainTexture, 1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _screenWidth, _screenHeight),
            WithAlpha(Color.Black, 0.82f * _fileSelectAlpha));

        float titleSize = _baseTextSize * 1.5f;
        float nameSize = _baseTextSize * 1.15f;
        float infoSize = _baseTextSize * 0.75f;
        float hintSize = _baseTextSize * 0.7f;

        _text.DrawShadowed(_spriteBatch, "SELECT A FILE", _screenWidth / 2f,
            _screenHeight * 0.16f, titleSize, WithAlpha(Color.White, _fileSelectAlpha), true);

        float rowHeight = _screenHeight * 0.145f;
        float firstRowY = _screenHeight * 0.33f;
        float boxWidth = MathF.Min(_screenWidth * 0.62f, 680f * (_screenHeight / 768f));
        float boxLeft = (_screenWidth - boxWidth) / 2f;

        for (int i = 0; i < SaveSlots.SlotCount; i++)
        {
            SaveSlot slot = _saves[i];
            bool selected = i == _slotSelected;
            float rowY = firstRowY + i * rowHeight;
            float boxHeight = rowHeight * 0.78f;

            // Selection highlight behind the row.
            var box = new Rectangle((int)boxLeft, (int)rowY, (int)boxWidth, (int)boxHeight);
            _spriteBatch.Draw(_pixel, box,
                WithAlpha(selected ? new Color(70, 20, 20) : new Color(20, 20, 20),
                    (selected ? 0.85f : 0.55f) * _fileSelectAlpha));

            DrawBorder(box, selected ? new Color(220, 40, 40) : new Color(90, 90, 90),
                selected ? 2 : 1, _fileSelectAlpha);

            Color nameColor = slot.Exists
                ? (selected ? new Color(255, 230, 120) : new Color(215, 215, 215))
                : (selected ? new Color(220, 40, 40) : new Color(130, 130, 130));

            float textLeft = boxLeft + boxWidth * 0.06f;

            _text.DrawShadowed(_spriteBatch, $"{i + 1}.  {slot.Title}",
                textLeft, rowY + boxHeight * 0.22f, nameSize,
                WithAlpha(nameColor, _fileSelectAlpha), false);

            _text.DrawShadowed(_spriteBatch, slot.Summary,
                textLeft, rowY + boxHeight * 0.62f, infoSize,
                WithAlpha(new Color(160, 160, 160), _fileSelectAlpha), false);

            if (selected)
            {
                _text.DrawShadowed(_spriteBatch, ">",
                    boxLeft - _baseTextSize, rowY + boxHeight / 2f, nameSize,
                    WithAlpha(new Color(220, 40, 40), _fileSelectAlpha), true);
            }
        }

        string hint = _saves[_slotSelected].Exists
            ? "ENTER - CONTINUE    DEL - ERASE    ESC - BACK"
            : "ENTER - BEGIN A NEW STORY    ESC - BACK";

        _text.DrawShadowed(_spriteBatch, hint, _screenWidth / 2f,
            _screenHeight - hintSize * 3f, hintSize,
            WithAlpha(new Color(150, 150, 150), _fileSelectAlpha), true);

        // Warn up front on an empty slot, since that is the one that wants a video.
        if (!_saves[_slotSelected].Exists && !FFmpeg.IsAvailable)
        {
            _text.DrawShadowed(_spriteBatch, "FFMPEG NOT FOUND - OPENING VIDEO WILL BE SKIPPED",
                _screenWidth / 2f, _screenHeight - hintSize * 1.4f, hintSize * 0.9f,
                WithAlpha(new Color(120, 100, 60), _fileSelectAlpha), true);
        }
    }

    /// <summary>Draws a hollow rectangle out of four thin filled quads.</summary>
    private void DrawBorder(Rectangle rect, Color color, int thickness, float alpha)
    {
        Color c = WithAlpha(color, alpha);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), c);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), c);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), c);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), c);
    }

    private void DrawOpeningVideoScreen()
    {
        // Letterboxing is baked into the decoded frame, so a full stretch is correct.
        if (_video is { HasFrame: true })
        {
            _spriteBatch.Draw(_video.Texture, new Rectangle(0, 0, _screenWidth, _screenHeight),
                Tint(_videoFadeAlpha));
        }

        float hintSize = _baseTextSize * 0.7f;
        _text.DrawShadowed(_spriteBatch, "PRESS ANY KEY TO SKIP", _screenWidth / 2f,
            _screenHeight - hintSize * 2.5f, hintSize,
            WithAlpha(new Color(150, 150, 150), 0.75f * _videoFadeAlpha), true);
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
        _text.DrawShadowed(_spriteBatch, _toastText, _screenWidth / 2f, y, _baseTextSize * 1.1f,
            WithAlpha(new Color(255, 230, 120), alpha), true);
    }

    protected override void UnloadContent()
    {
        _scene?.Dispose();
        _video?.Dispose();
        _musicInstance?.Stop();
        _musicInstance?.Dispose();
        _musicSound?.Dispose();
        _clickSound?.Dispose();
        _introTexture?.Dispose();
        if (!ReferenceEquals(_mainTexture, _introTexture)) _mainTexture?.Dispose();
        _pixel?.Dispose();
        _text?.Dispose();

        base.UnloadContent();
    }
}
