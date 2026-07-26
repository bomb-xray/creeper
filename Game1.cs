using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
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
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // Textures
    private Texture2D _introTexture;
    private Texture2D _mainTexture;

    // Audio
    private SoundEffect _clickSound;
    private Song _bgMusic;

    // State
    private GameState _state = GameState.IntroFadeIn;
    private MenuOption _selectedOption = MenuOption.Play;

    // Fade
    private float _introAlpha = 0f;
    private float _mainAlpha = 0f;
    private float _fadeSpeed = 1.2f;
    private float _transitionTimer = 0f;
    private float _transitionDuration = 1.5f;

    // Text blink
    private float _blinkTimer = 0f;
    private bool _blinkVisible = true;

    // Menu animation
    private float _menuAlpha = 0f;
    private float[] _optionAlphas = new float[3] { 0f, 0f, 0f };
    private float _optionStaggerDelay = 0.2f;

    // Options screen
    private bool _showingOptions = false;
    private int _optionsSelected = 0;

    // Pixel font size
    private const int TitleFontSize = 50;
    private const int MenuFontSize = 40;
    private const int SubFontSize = 24;

    // Font (using default)
    private PixelFont _font;

    // Previous keyboard state for key press detection
    private KeyboardState _prevKeyboardState;

    // Screen
    private int _screenWidth;
    private int _screenHeight;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        // Fullscreen
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        _graphics.ApplyChanges();

        _screenWidth = _graphics.PreferredBackBufferWidth;
        _screenHeight = _graphics.PreferredBackBufferHeight;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = new PixelFont(GraphicsDevice);

        // Load assets from assets folder
        string assetDir = FindAssetDirectory();
        LoadAssets(assetDir);
    }

    private string FindAssetDirectory()
    {
        string[] paths = {
            "assets",
            "../../../assets",
            "../../assets",
            "../assets",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
        };

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                Console.WriteLine($"Found assets directory: {path}");
                return path;
            }
        }

        Console.WriteLine("Warning: assets directory not found!");
        return "assets";
    }

    private void LoadAssets(string assetDir)
    {
        // Load textures with auto-conversion
        _introTexture = LoadTextureWithConversion(assetDir, "image") ?? 
                       LoadTextureWithConversion(assetDir, "negro") ??
                       CreateDummyTexture();

        _mainTexture = LoadTextureWithConversion(assetDir, "negro") ?? CreateDummyTexture();

        // Load click sound
        _clickSound = LoadSoundWithConversion(assetDir, "click");

        // Load music
        string musicPath = AudioConverter.EnsurePlayableAudio(assetDir, "untrust");
        if (!string.IsNullOrEmpty(musicPath) && File.Exists(musicPath))
        {
            try
            {
                using (var stream = File.OpenRead(musicPath))
                {
                    _bgMusic = Song.FromStream("untrust", stream);
                    MediaPlayer.Volume = 0.7f;
                    MediaPlayer.IsRepeating = true;
                    Console.WriteLine($"Music loaded: {musicPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load music: {ex.Message}");
            }
        }
    }

    private Texture2D LoadTextureWithConversion(string dir, string baseName)
    {
        // Try to convert problematic formats first
        string imagePath = ImageConverter.EnsureLoadableImage(dir, baseName);
        
        if (string.IsNullOrEmpty(imagePath))
        {
            // Try native formats
            string[] extensions = { ".png", ".bmp", ".tga", ".jpg", ".jpeg" };
            foreach (string ext in extensions)
            {
                string path = Path.Combine(dir, baseName + ext);
                if (File.Exists(path))
                {
                    imagePath = path;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Console.WriteLine($"No texture found for: {baseName}");
            return null;
        }

        try
        {
            Console.WriteLine($"Loading texture: {imagePath}");
            using (var stream = File.OpenRead(imagePath))
            {
                var texture = Texture2D.FromStream(GraphicsDevice, stream);
                Console.WriteLine($"  -> SUCCESS: {texture.Width}x{texture.Height}");
                return texture;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> FAILED: {ex.Message}");
            return null;
        }
    }

    private SoundEffect LoadSoundWithConversion(string dir, string baseName)
    {
        string soundPath = AudioConverter.EnsurePlayableAudio(dir, baseName);
        
        if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
        {
            Console.WriteLine($"No sound found for: {baseName}");
            return null;
        }

        try
        {
            Console.WriteLine($"Loading sound: {soundPath}");
            using (var stream = File.OpenRead(soundPath))
            {
                var sound = SoundEffect.FromStream(stream);
                Console.WriteLine($"  -> SUCCESS");
                return sound;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> FAILED: {ex.Message}");
            return null;
        }
    }

    private Texture2D CreateDummyTexture()
    {
        var texture = new Texture2D(GraphicsDevice, 1, 1);
        texture.SetData(new[] { Color.DarkGray });
        return texture;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var kbState = Keyboard.GetState();

        // ESC to quit
        if (WasKeyPressed(kbState, Keys.Escape))
        {
            if (_showingOptions)
            {
                _showingOptions = false;
            }
            else
            {
                Exit();
            }
        }

        switch (_state)
        {
            case GameState.IntroFadeIn:
                UpdateIntroFadeIn(dt);
                break;
            case GameState.IntroWaiting:
                UpdateIntroWaiting(dt);
                break;
            case GameState.Transition:
                UpdateTransition(dt);
                break;
            case GameState.MainMenu:
                UpdateMainMenu(dt);
                break;
        }

        _prevKeyboardState = kbState;
        base.Update(gameTime);
    }

    private void UpdateIntroFadeIn(float dt)
    {
        _introAlpha += _fadeSpeed * dt;
        if (_introAlpha >= 1f)
        {
            _introAlpha = 1f;
            _state = GameState.IntroWaiting;
        }
    }

    private void UpdateIntroWaiting(float dt)
    {
        _blinkTimer += dt;
        if (_blinkTimer >= 0.6f)
        {
            _blinkTimer = 0f;
            _blinkVisible = !_blinkVisible;
        }

        var kbState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        if (kbState.GetPressedKeys().Length > 0 || mouseState.LeftButton == ButtonState.Pressed)
        {
            _state = GameState.Transition;
            _clickSound?.Play();
            _transitionTimer = 0f;
        }
    }

    private void UpdateTransition(float dt)
    {
        _transitionTimer += dt;

        _introAlpha = MathF.Max(0f, 1f - (_transitionTimer / _transitionDuration));

        float mainFadeStart = _transitionDuration * 0.3f;
        if (_transitionTimer > mainFadeStart)
        {
            float mainProgress = (_transitionTimer - mainFadeStart) / (_transitionDuration - mainFadeStart);
            _mainAlpha = MathF.Min(1f, mainProgress);
        }

        if (_transitionTimer > mainFadeStart && _bgMusic != null && MediaPlayer.State != MediaState.Playing)
        {
            MediaPlayer.Play(_bgMusic);
        }

        if (_transitionTimer >= _transitionDuration)
        {
            _introAlpha = 0f;
            _mainAlpha = 1f;
            _state = GameState.MainMenu;
            _menuAlpha = 0f;
            _optionAlphas = new float[3] { 0f, 0f, 0f };
        }
    }

    private void UpdateMainMenu(float dt)
    {
        if (_bgMusic != null && MediaPlayer.State != MediaState.Playing)
        {
            MediaPlayer.Play(_bgMusic);
        }

        if (_showingOptions)
        {
            UpdateOptionsScreen(dt);
            return;
        }

        _menuAlpha = MathF.Min(1f, _menuAlpha + dt * 2f);
        for (int i = 0; i < 3; i++)
        {
            float delay = i * _optionStaggerDelay;
            if (_menuAlpha > delay)
            {
                _optionAlphas[i] = MathF.Min(1f, (_menuAlpha - delay) * 2f);
            }
        }

        var kbState = Keyboard.GetState();

        if (WasKeyPressed(kbState, Keys.Up) || WasKeyPressed(kbState, Keys.W))
        {
            int prev = (int)_selectedOption;
            _selectedOption = (MenuOption)((prev - 1 + 3) % 3);
            _clickSound?.Play();
        }

        if (WasKeyPressed(kbState, Keys.Down) || WasKeyPressed(kbState, Keys.S))
        {
            int next = (int)_selectedOption;
            _selectedOption = (MenuOption)((next + 1) % 3);
            _clickSound?.Play();
        }

        if (WasKeyPressed(kbState, Keys.Enter) || WasKeyPressed(kbState, Keys.Space))
        {
            _clickSound?.Play();
            switch (_selectedOption)
            {
                case MenuOption.Play:
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

    private bool WasKeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && _prevKeyboardState.IsKeyUp(key);
    }

    private void UpdateOptionsScreen(float dt)
    {
        var kbState = Keyboard.GetState();

        if (WasKeyPressed(kbState, Keys.Up) || WasKeyPressed(kbState, Keys.W))
        {
            _optionsSelected = (_optionsSelected - 1 + 2) % 2;
            _clickSound?.Play();
        }

        if (WasKeyPressed(kbState, Keys.Down) || WasKeyPressed(kbState, Keys.S))
        {
            _optionsSelected = (_optionsSelected + 1) % 2;
            _clickSound?.Play();
        }

        if (WasKeyPressed(kbState, Keys.Enter) || WasKeyPressed(kbState, Keys.Space))
        {
            _clickSound?.Play();
            _showingOptions = false;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin();

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

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawIntroScreen()
    {
        if (_introTexture != null)
        {
            float scaleX = (float)_screenWidth / _introTexture.Width;
            float scaleY = (float)_screenHeight / _introTexture.Height;
            float scale = MathF.Min(scaleX, scaleY);

            float scaledWidth = _introTexture.Width * scale;
            float scaledHeight = _introTexture.Height * scale;
            float posX = (_screenWidth - scaledWidth) / 2f;
            float posY = (_screenHeight - scaledHeight) / 2f - 30;

            Color tintColor = new Color(255, 255, 255, (int)(_introAlpha * 255));
            _spriteBatch.Draw(_introTexture, new Rectangle((int)posX, (int)posY, (int)scaledWidth, (int)scaledHeight), tintColor);

            if (_state == GameState.IntroWaiting && _blinkVisible)
            {
                string text = "PRESS ANY KEY TO CONTINUE";
                DrawText(text, _screenWidth / 2, (int)(posY + scaledHeight + 20), SubFontSize, new Color(255, 255, 255, (int)(_introAlpha * 255)), true);
            }
        }
    }

    private void DrawTransitionScreen()
    {
        if (_introAlpha > 0f && _introTexture != null)
        {
            float scaleX = (float)_screenWidth / _introTexture.Width;
            float scaleY = (float)_screenHeight / _introTexture.Height;
            float scale = MathF.Min(scaleX, scaleY);

            float scaledWidth = _introTexture.Width * scale;
            float scaledHeight = _introTexture.Height * scale;
            float posX = (_screenWidth - scaledWidth) / 2f;
            float posY = (_screenHeight - scaledHeight) / 2f - 30;

            Color tintColor = new Color(255, 255, 255, (int)(_introAlpha * 255));
            _spriteBatch.Draw(_introTexture, new Rectangle((int)posX, (int)posY, (int)scaledWidth, (int)scaledHeight), tintColor);
        }

        if (_mainAlpha > 0f && _mainTexture != null)
        {
            float scaleX = (float)_screenWidth / _mainTexture.Width;
            float scaleY = (float)_screenHeight / _mainTexture.Height;
            float scale = MathF.Min(scaleX, scaleY);

            float scaledWidth = _mainTexture.Width * scale;
            float scaledHeight = _mainTexture.Height * scale;
            float posX = (_screenWidth - scaledWidth) / 2f;
            float posY = (_screenHeight - scaledHeight) / 2f - 40;

            Color tintColor = new Color(255, 255, 255, (int)(_mainAlpha * 255));
            _spriteBatch.Draw(_mainTexture, new Rectangle((int)posX, (int)posY, (int)scaledWidth, (int)scaledHeight), tintColor);
        }
    }

    private void DrawMainMenuScreen()
    {
        if (_mainTexture != null)
        {
            _spriteBatch.Draw(_mainTexture, new Rectangle(0, 0, _screenWidth, _screenHeight), new Color(255, 255, 255, (int)(_mainAlpha * 255)));
        }

        if (_showingOptions)
        {
            DrawOptionsScreen();
            return;
        }

        string[] options = { "PLAY", "OPTIONS", "EXIT" };
        float menuStartY = _screenHeight - 220;
        float spacing = 55;

        for (int i = 0; i < options.Length; i++)
        {
            float alpha = _optionAlphas[i];
            if (alpha <= 0f) continue;

            bool selected = (i == (int)_selectedOption);
            Color textColor = selected ? new Color(220, 40, 40, (int)(alpha * 255)) : new Color(200, 200, 200, (int)(alpha * 200));

            DrawText(options[i], _screenWidth / 2, (int)(menuStartY + (i * spacing)), MenuFontSize, textColor, true);

            if (selected)
            {
                DrawText(">", _screenWidth / 2 - 150, (int)(menuStartY + (i * spacing)), MenuFontSize, textColor, false);
                DrawText("<", _screenWidth / 2 + 150, (int)(menuStartY + (i * spacing)), MenuFontSize, textColor, false);
            }
        }
    }

    private void DrawOptionsScreen()
    {
        _spriteBatch.Draw(CreateDummyTexture(), new Rectangle(0, 0, _screenWidth, _screenHeight), new Color(0, 0, 0, 200));

        DrawText("OPTIONS", _screenWidth / 2, _screenHeight / 2 - 120, TitleFontSize, Color.White, true);

        string[] opts = { "BACK", "CREDITS" };
        float startY = _screenHeight / 2f - 20;
        float spacing = 60;

        for (int i = 0; i < opts.Length; i++)
        {
            bool selected = (i == _optionsSelected);
            Color col = selected ? new Color(220, 40, 40, 255) : new Color(200, 200, 200, 200);

            DrawText(opts[i], _screenWidth / 2, (int)(startY + (i * spacing)), MenuFontSize, col, true);

            if (selected)
            {
                DrawText(">", _screenWidth / 2 - 100, (int)(startY + (i * spacing)), MenuFontSize, col, false);
                DrawText("<", _screenWidth / 2 + 100, (int)(startY + (i * spacing)), MenuFontSize, col, false);
            }
        }

        DrawText("PRESS ESC TO GO BACK", _screenWidth / 2, _screenHeight - 60, 18, new Color(120, 120, 120, 255), true);
    }

    private void DrawText(string text, int x, int y, int fontSize, Color color, bool center)
    {
        if (_font == null) return;
        // fontSize is the pixel scale factor (e.g., 4 means each font pixel = 4 screen pixels)
        int pixelSize = Math.Max(1, fontSize / 8);
        _font.DrawText(_spriteBatch, text, x, y, pixelSize, color, center);
    }

    protected override void UnloadContent()
    {
        _introTexture?.Dispose();
        _mainTexture?.Dispose();
        _clickSound?.Dispose();
        _font?.Dispose();
        
        if (_bgMusic != null)
        {
            MediaPlayer.Stop();
        }

        base.UnloadContent();
    }
}
