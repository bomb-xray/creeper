using Raylib_cs;
using System;
using System.Numerics;

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

public class Game : IDisposable
{
    // Screen
    private int _screenWidth;
    private int _screenHeight;

    // Textures
    private Texture2D _introTexture;
    private Texture2D _mainTexture;

    // Audio
    private Sound _clickSound;
    private Music _bgMusic;
    private bool _musicLoaded = false;

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

    private bool _disposed = false;

    // Helper: create Color with explicit byte cast to avoid ambiguity
    private static Color MakeColor(int r, int g, int b, int a)
    {
        return new Color((byte)r, (byte)g, (byte)b, (byte)a);
    }

    public void Run()
    {
        Initialize();
        GameLoop();
        Cleanup();
    }

    private void Initialize()
    {
        // Fullscreen mode
        Raylib.SetConfigFlags(ConfigFlags.FullscreenMode);
        Raylib.InitWindow(0, 0, "Creeper");

        _screenWidth = Raylib.GetScreenWidth();
        _screenHeight = Raylib.GetScreenHeight();

        // Hide cursor
        Raylib.HideCursor();
        Raylib.DisableCursor();

        // Audio
        Raylib.InitAudioDevice();

        // Load assets
        // Try different paths for assets
        string[] assetPaths = {
            "assets",
            "../../../assets",
            "../../assets",
            "../assets",
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
        };

        string assetDir = "";
        foreach (string path in assetPaths)
        {
            if (System.IO.Directory.Exists(path))
            {
                assetDir = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(assetDir))
        {
            assetDir = "assets"; // fallback
        }

        // Load textures with fallback
        string imagePath = System.IO.Path.Combine(assetDir, "image.png");
        string negroPath = System.IO.Path.Combine(assetDir, "negro.png");
        string clickPath = System.IO.Path.Combine(assetDir, "click.wav");

        try
        {
            _introTexture = Raylib.LoadTexture(imagePath);
            // Check if texture loaded properly (default texture is 1x1)
            if (_introTexture.Width <= 1)
            {
                Console.WriteLine($"Warning: image.png failed to load properly, using negro.png as fallback");
                _introTexture = Raylib.LoadTexture(negroPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load image.png: {ex.Message}");
            _introTexture = Raylib.LoadTexture(negroPath);
        }

        try
        {
            _mainTexture = Raylib.LoadTexture(negroPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to load negro.png: {ex.Message}");
            // Create a dummy 1x1 texture
            Image dummyImage = Raylib.GenImageColor(1, 1, Color.DarkGray);
            _mainTexture = Raylib.LoadTextureFromImage(dummyImage);
            Raylib.UnloadImage(dummyImage);
        }

        try
        {
            _clickSound = Raylib.LoadSound(clickPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load click.wav: {ex.Message}");
        }

        // Try to load music with proper error handling
        string mp3Path = System.IO.Path.Combine(assetDir, "untrust.mp3");
        string wavPath = System.IO.Path.Combine(assetDir, "untrust.wav");
        
        _musicLoaded = false;
        try
        {
            if (System.IO.File.Exists(mp3Path))
            {
                Console.WriteLine($"Loading music: {mp3Path}");
                _bgMusic = Raylib.LoadMusicStream(mp3Path);
                _musicLoaded = true;
            }
            else if (System.IO.File.Exists(wavPath))
            {
                Console.WriteLine($"Loading music: {wavPath}");
                _bgMusic = Raylib.LoadMusicStream(wavPath);
                _musicLoaded = true;
            }
            else
            {
                Console.WriteLine("Warning: No music file found (untrust.mp3 or untrust.wav)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load music: {ex.Message}");
            _musicLoaded = false;
        }

        if (_musicLoaded)
        {
            Raylib.SetMusicVolume(_bgMusic, 0.7f);
        }
        Raylib.SetTargetFPS(60);
    }

    private void GameLoop()
    {
        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            Update(dt);
            Draw();
        }
    }

    private void Update(float dt)
    {
        // Allow ESC to quit / go back
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_showingOptions)
            {
                _showingOptions = false;
                return;
            }
            Environment.Exit(0);
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
        // Blink text
        _blinkTimer += dt;
        if (_blinkTimer >= 0.6f)
        {
            _blinkTimer = 0f;
            _blinkVisible = !_blinkVisible;
        }

        // Check for any key press
        int key = Raylib.GetKeyPressed();
        if (key > 0 || Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _state = GameState.Transition;
            Raylib.PlaySound(_clickSound);
            _transitionTimer = 0f;
        }
    }

    private void UpdateTransition(float dt)
    {
        _transitionTimer += dt;

        // Fade out intro
        _introAlpha = MathF.Max(0f, 1f - (_transitionTimer / _transitionDuration));

        // Fade in main image (starts a bit after intro starts fading)
        float mainFadeStart = _transitionDuration * 0.3f;
        if (_transitionTimer > mainFadeStart)
        {
            float mainProgress = (_transitionTimer - mainFadeStart) / (_transitionDuration - mainFadeStart);
            _mainAlpha = MathF.Min(1f, mainProgress);
        }

        // Start music when main image appears
        if (_transitionTimer > mainFadeStart && _musicLoaded && !Raylib.IsMusicStreamPlaying(_bgMusic))
        {
            Raylib.PlayMusicStream(_bgMusic);
        }

        if (_transitionTimer >= _transitionDuration)
        {
            _introAlpha = 0f;
            _mainAlpha = 1f;
            _state = GameState.MainMenu;
            _menuAlpha = 0f;
            _optionAlphas = new float[3] { 0f, 0f, 0f };
        }

        if (_musicLoaded)
        {
            Raylib.UpdateMusicStream(_bgMusic);
        }
    }

    private void UpdateMainMenu(float dt)
    {
        if (_musicLoaded)
        {
            Raylib.UpdateMusicStream(_bgMusic);

            // Loop music
            if (!Raylib.IsMusicStreamPlaying(_bgMusic))
            {
                Raylib.PlayMusicStream(_bgMusic);
            }
        }

        if (_showingOptions)
        {
            UpdateOptionsScreen(dt);
            return;
        }

        // Fade in menu items
        _menuAlpha = MathF.Min(1f, _menuAlpha + dt * 2f);
        for (int i = 0; i < 3; i++)
        {
            float delay = i * _optionStaggerDelay;
            if (_menuAlpha > delay)
            {
                _optionAlphas[i] = MathF.Min(1f, (_menuAlpha - delay) * 2f);
            }
        }

        // Navigation
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            int prev = (int)_selectedOption;
            _selectedOption = (MenuOption)((prev - 1 + 3) % 3);
            Raylib.PlaySound(_clickSound);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            int next = (int)_selectedOption;
            _selectedOption = (MenuOption)((next + 1) % 3);
            Raylib.PlaySound(_clickSound);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Raylib.PlaySound(_clickSound);
            switch (_selectedOption)
            {
                case MenuOption.Play:
                    // Not implemented yet
                    break;
                case MenuOption.Options:
                    _showingOptions = true;
                    _optionsSelected = 0;
                    break;
                case MenuOption.Exit:
                    Environment.Exit(0);
                    break;
            }
        }
    }

    private void UpdateOptionsScreen(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _optionsSelected = (_optionsSelected - 1 + 2) % 2;
            Raylib.PlaySound(_clickSound);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _optionsSelected = (_optionsSelected + 1) % 2;
            Raylib.PlaySound(_clickSound);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Raylib.PlaySound(_clickSound);
            switch (_optionsSelected)
            {
                case 0: // Back
                    _showingOptions = false;
                    break;
                case 1: // Credits
                    _showingOptions = false;
                    break;
            }
        }
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

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

        Raylib.EndDrawing();
    }

    private void DrawIntroScreen()
    {
        // Calculate scale to fit screen
        float scaleX = (float)_screenWidth / _introTexture.Width;
        float scaleY = (float)_screenHeight / _introTexture.Height;
        float scale = MathF.Min(scaleX, scaleY); // Maintain aspect ratio

        // Calculate centered position
        float scaledWidth = _introTexture.Width * scale;
        float scaledHeight = _introTexture.Height * scale;
        float posX = (_screenWidth - scaledWidth) / 2f;
        float posY = (_screenHeight - scaledHeight) / 2f - 30;

        Color tintColor = MakeColor(255, 255, 255, (int)(_introAlpha * 255));
        
        // Draw with scaling
        Rectangle sourceRect = new Rectangle(0, 0, _introTexture.Width, _introTexture.Height);
        Rectangle destRect = new Rectangle(posX, posY, scaledWidth, scaledHeight);
        Vector2 origin = new Vector2(0, 0);
        Raylib.DrawTexturePro(_introTexture, sourceRect, destRect, origin, 0f, tintColor);

        // "PRESS ANY KEY" text below image
        if (_state == GameState.IntroWaiting && _blinkVisible)
        {
            string text = "PRESS ANY KEY TO CONTINUE";
            int textWidth = Raylib.MeasureText(text, SubFontSize);
            float textX = (_screenWidth - textWidth) / 2f;
            float textY = posY + scaledHeight + 20;

            Color textColor = MakeColor(255, 255, 255, (int)(_introAlpha * 255));
            DrawPixelText(text, (int)textX, (int)textY, SubFontSize, textColor);
        }
    }

    private void DrawTransitionScreen()
    {
        // Draw fading intro image
        if (_introAlpha > 0f)
        {
            float scaleX = (float)_screenWidth / _introTexture.Width;
            float scaleY = (float)_screenHeight / _introTexture.Height;
            float scale = MathF.Min(scaleX, scaleY);

            float scaledWidth = _introTexture.Width * scale;
            float scaledHeight = _introTexture.Height * scale;
            float posX = (_screenWidth - scaledWidth) / 2f;
            float posY = (_screenHeight - scaledHeight) / 2f - 30;

            Color tintColor = MakeColor(255, 255, 255, (int)(_introAlpha * 255));
            Rectangle sourceRect = new Rectangle(0, 0, _introTexture.Width, _introTexture.Height);
            Rectangle destRect = new Rectangle(posX, posY, scaledWidth, scaledHeight);
            Vector2 origin = new Vector2(0, 0);
            Raylib.DrawTexturePro(_introTexture, sourceRect, destRect, origin, 0f, tintColor);
        }

        // Draw fading in main image
        if (_mainAlpha > 0f)
        {
            float scaleX = (float)_screenWidth / _mainTexture.Width;
            float scaleY = (float)_screenHeight / _mainTexture.Height;
            float scale = MathF.Min(scaleX, scaleY);

            float scaledWidth = _mainTexture.Width * scale;
            float scaledHeight = _mainTexture.Height * scale;
            float posX = (_screenWidth - scaledWidth) / 2f;
            float posY = (_screenHeight - scaledHeight) / 2f - 40;

            Color tintColor = MakeColor(255, 255, 255, (int)(_mainAlpha * 255));
            Rectangle sourceRect = new Rectangle(0, 0, _mainTexture.Width, _mainTexture.Height);
            Rectangle destRect = new Rectangle(posX, posY, scaledWidth, scaledHeight);
            Vector2 origin = new Vector2(0, 0);
            Raylib.DrawTexturePro(_mainTexture, sourceRect, destRect, origin, 0f, tintColor);
        }
    }

    private void DrawMainMenuScreen()
    {
        // Draw main image (fitted to screen)
        float scaleX = (float)_screenWidth / _mainTexture.Width;
        float scaleY = (float)_screenHeight / _mainTexture.Height;
        float scale = MathF.Min(scaleX, scaleY);

        float scaledWidth = _mainTexture.Width * scale;
        float scaledHeight = _mainTexture.Height * scale;
        float imgX = (_screenWidth - scaledWidth) / 2f;
        float imgY = (_screenHeight - scaledHeight) / 2f - 50;

        Color tintColor = MakeColor(255, 255, 255, (int)(_mainAlpha * 255));
        Rectangle sourceRect = new Rectangle(0, 0, _mainTexture.Width, _mainTexture.Height);
        Rectangle destRect = new Rectangle(imgX, imgY, scaledWidth, scaledHeight);
        Vector2 origin = new Vector2(0, 0);
        Raylib.DrawTexturePro(_mainTexture, sourceRect, destRect, origin, 0f, tintColor);

        if (_showingOptions)
        {
            DrawOptionsScreen();
            return;
        }

        // Draw menu options
        string[] options = { "PLAY", "OPTIONS", "EXIT" };
        float menuStartY = imgY + scaledHeight + 20; // Position below the image
        float spacing = 60;

        for (int i = 0; i < options.Length; i++)
        {
            float alpha = _optionAlphas[i];
            if (alpha <= 0f) continue;

            bool selected = (i == (int)_selectedOption);
            string displayText = options[i];

            int textWidth = Raylib.MeasureText(displayText, MenuFontSize);
            float textX = (_screenWidth - textWidth) / 2f;
            float textY = menuStartY + (i * spacing);

            Color textColor;
            if (selected)
            {
                textColor = MakeColor(220, 40, 40, (int)(alpha * 255));
                // Draw selector arrows
                int arrowOffset = 40;
                Color arrowColor = MakeColor(220, 40, 40, (int)(alpha * 255));
                DrawPixelText(">", (int)(textX - arrowOffset), (int)textY, MenuFontSize, arrowColor);
                DrawPixelText("<", (int)(textX + textWidth + arrowOffset - 20), (int)textY, MenuFontSize, arrowColor);
            }
            else
            {
                textColor = MakeColor(200, 200, 200, (int)(alpha * 200));
            }

            DrawPixelText(displayText, (int)textX, (int)textY, MenuFontSize, textColor);

            // "Coming soon" for Play
            if (i == 0 && selected)
            {
                string hint = "(coming soon)";
                int hintWidth = Raylib.MeasureText(hint, 16);
                Color hintColor = MakeColor(150, 150, 150, (int)(alpha * 180));
                DrawPixelText(hint, (int)((_screenWidth - hintWidth) / 2f), (int)(textY + spacing - 15), 16, hintColor);
            }
        }
    }

    private void DrawOptionsScreen()
    {
        // Semi-transparent overlay
        Raylib.DrawRectangle(0, 0, _screenWidth, _screenHeight, MakeColor(0, 0, 0, 200));

        // Title
        string title = "OPTIONS";
        int titleWidth = Raylib.MeasureText(title, TitleFontSize);
        DrawPixelText(title, (_screenWidth - titleWidth) / 2, _screenHeight / 2 - 120, TitleFontSize, Color.White);

        // Options
        string[] opts = { "BACK", "CREDITS" };
        float startY = _screenHeight / 2f - 20;
        float spacing = 60;

        for (int i = 0; i < opts.Length; i++)
        {
            bool selected = (i == _optionsSelected);
            int tw = Raylib.MeasureText(opts[i], MenuFontSize);
            float tx = (_screenWidth - tw) / 2f;
            float ty = startY + (i * spacing);

            Color col = selected ? MakeColor(220, 40, 40, 255) : MakeColor(200, 200, 200, 200);
            DrawPixelText(opts[i], (int)tx, (int)ty, MenuFontSize, col);

            if (selected)
            {
                DrawPixelText(">", (int)(tx - 40), (int)ty, MenuFontSize, MakeColor(220, 40, 40, 255));
                DrawPixelText("<", (int)(tx + tw + 20), (int)ty, MenuFontSize, MakeColor(220, 40, 40, 255));
            }
        }

        // Hint
        string hint = "PRESS ESC TO GO BACK";
        int hw = Raylib.MeasureText(hint, 18);
        DrawPixelText(hint, (_screenWidth - hw) / 2, _screenHeight - 60, 18, MakeColor(120, 120, 120, 255));
    }

    /// <summary>
    /// Draws text with pixel-snapped positioning for crisp pixel look.
    /// </summary>
    private void DrawPixelText(string text, int x, int y, int fontSize, Color color)
    {
        // Snap to pixel grid
        x = x - (x % 2);
        y = y - (y % 2);
        fontSize = fontSize - (fontSize % 2);
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private void Cleanup()
    {
        if (_musicLoaded)
        {
            Raylib.StopMusicStream(_bgMusic);
            Raylib.UnloadMusicStream(_bgMusic);
        }
        Raylib.UnloadSound(_clickSound);
        Raylib.UnloadTexture(_introTexture);
        Raylib.UnloadTexture(_mainTexture);
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}
