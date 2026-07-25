using Raylib_cs;
using System;
using System.Numerics;
using static Raylib_cs.Raylib;
using static Raylib_cs.Color;
using static Raylib_cs.KeyboardKey;
using static Raylib_cs.MouseButton;

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

    public void Run()
    {
        Initialize();
        GameLoop();
        Cleanup();
    }

    private void Initialize()
    {
        // Fullscreen mode
        SetConfigFlags(ConfigFlags.FLAG_FULLSCREEN_MODE);
        SetConfigFlags(ConfigFlags.FLAG_VSYNC_HINT);

        InitWindow(0, 0, "Creeper");

        _screenWidth = GetScreenWidth();
        _screenHeight = GetScreenHeight();

        // Hide cursor
        HideCursor();
        DisableCursor();

        // Audio
        InitAudioDevice();

        // Load assets
        _introTexture = LoadTexture("assets/image.png");
        _mainTexture = LoadTexture("assets/negro.png");
        _clickSound = LoadSound("assets/click.wav");
        
        // Try to load MP3 first, fall back to WAV
        if (System.IO.File.Exists("assets/untrust.mp3"))
        {
            _bgMusic = LoadMusicStream("assets/untrust.mp3");
        }
        else
        {
            _bgMusic = LoadMusicStream("assets/untrust.wav");
        }

        SetMusicVolume(_bgMusic, 0.7f);

        SetTargetFPS(60);
    }

    private void GameLoop()
    {
        while (!WindowShouldClose())
        {
            float dt = GetFrameTime();
            Update(dt);
            Draw();
        }
    }

    private void Update(float dt)
    {
        // Allow ESC to quit at any time
        if (IsKeyPressed(KEY_ESCAPE))
        {
            if (_showingOptions)
            {
                _showingOptions = false;
                return;
            }

            if (_state == GameState.MainMenu)
            {
                // Go back to intro? Or just close
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(0);
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
        int key = GetKeyPressed();
        if (key > 0 || IsMouseButtonPressed(MOUSE_BUTTON_LEFT))
        {
            // Start transition
            _state = GameState.Transition;
            PlaySound(_clickSound);
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
        if (_transitionTimer > mainFadeStart && !IsMusicStreamPlaying(_bgMusic))
        {
            PlayMusicStream(_bgMusic);
        }

        if (_transitionTimer >= _transitionDuration)
        {
            _introAlpha = 0f;
            _mainAlpha = 1f;
            _state = GameState.MainMenu;
            _menuAlpha = 0f;
            _optionAlphas = new float[3] { 0f, 0f, 0f };
        }

        UpdateMusicStream(_bgMusic);
    }

    private void UpdateMainMenu(float dt)
    {
        UpdateMusicStream(_bgMusic);

        // Loop music
        if (!IsMusicStreamPlaying(_bgMusic))
        {
            PlayMusicStream(_bgMusic);
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
        if (IsKeyPressed(KEY_UP) || IsKeyPressed(KEY_W))
        {
            int prev = (int)_selectedOption;
            _selectedOption = (MenuOption)((prev - 1 + 3) % 3);
            PlaySound(_clickSound);
        }

        if (IsKeyPressed(KEY_DOWN) || IsKeyPressed(KEY_S))
        {
            int next = (int)_selectedOption;
            _selectedOption = (MenuOption)((next + 1) % 3);
            PlaySound(_clickSound);
        }

        if (IsKeyPressed(KEY_ENTER) || IsKeyPressed(KEY_SPACE))
        {
            PlaySound(_clickSound);
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
        if (IsKeyPressed(KEY_UP) || IsKeyPressed(KEY_W))
        {
            _optionsSelected = (_optionsSelected - 1 + 2) % 2;
            PlaySound(_clickSound);
        }

        if (IsKeyPressed(KEY_DOWN) || IsKeyPressed(KEY_S))
        {
            _optionsSelected = (_optionsSelected + 1) % 2;
            PlaySound(_clickSound);
        }

        if (IsKeyPressed(KEY_ENTER) || IsKeyPressed(KEY_SPACE))
        {
            PlaySound(_clickSound);
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
        BeginDrawing();
        ClearBackground(BLACK);

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

        EndDrawing();
    }

    private void DrawIntroScreen()
    {
        // Draw centered intro image with alpha
        float imgX = (_screenWidth - _introTexture.Width) / 2f;
        float imgY = (_screenHeight - _introTexture.Height) / 2f - 60;

        Color tintColor = new Color(255, 255, 255, (byte)(_introAlpha * 255));
        DrawTexture(_introTexture, (int)imgX, (int)imgY, tintColor);

        // "PRESS ANY KEY" text below image
        if (_state == GameState.IntroWaiting && _blinkVisible)
        {
            string text = "PRESS ANY KEY TO CONTINUE";
            int textWidth = MeasureText(text, SubFontSize);
            float textX = (_screenWidth - textWidth) / 2f;
            float textY = imgY + _introTexture.Height + 40;

            Color textColor = new Color(255, 255, 255, (byte)(_introAlpha * 255));
            DrawPixelText(text, (int)textX, (int)textY, SubFontSize, textColor);
        }
    }

    private void DrawTransitionScreen()
    {
        // Draw fading intro image
        if (_introAlpha > 0f)
        {
            float imgX = (_screenWidth - _introTexture.Width) / 2f;
            float imgY = (_screenHeight - _introTexture.Height) / 2f - 60;
            Color tintColor = new Color(255, 255, 255, (byte)(_introAlpha * 255));
            DrawTexture(_introTexture, (int)imgX, (int)imgY, tintColor);
        }

        // Draw fading in main image
        if (_mainAlpha > 0f)
        {
            float imgX = (_screenWidth - _mainTexture.Width) / 2f;
            float imgY = (_screenHeight - _mainTexture.Height) / 2f - 80;
            Color tintColor = new Color(255, 255, 255, (byte)(_mainAlpha * 255));
            DrawTexture(_mainTexture, (int)imgX, (int)imgY, tintColor);
        }
    }

    private void DrawMainMenuScreen()
    {
        // Draw main image (background-ish)
        float imgX = (_screenWidth - _mainTexture.Width) / 2f;
        float imgY = (_screenHeight - _mainTexture.Height) / 2f - 100;
        Color tintColor = new Color(255, 255, 255, (byte)(_mainAlpha * 255));
        DrawTexture(_mainTexture, (int)imgX, (int)imgY, tintColor);

        if (_showingOptions)
        {
            DrawOptionsScreen();
            return;
        }

        // Draw menu options
        string[] options = { "PLAY", "OPTIONS", "EXIT" };
        float menuStartY = _screenHeight / 2f + 80;
        float spacing = 60;

        for (int i = 0; i < options.Length; i++)
        {
            float alpha = _optionAlphas[i];
            if (alpha <= 0f) continue;

            bool selected = (i == (int)_selectedOption);
            string displayText = options[i];

            // Pixel-style text
            int textWidth = MeasureText(displayText, MenuFontSize);
            float textX = (_screenWidth - textWidth) / 2f;
            float textY = menuStartY + (i * spacing);

            // Color
            Color textColor;
            if (selected)
            {
                // Highlight with red / crimson
                textColor = new Color(220, 40, 40, (byte)(alpha * 255));
                // Draw selector arrows
                string leftArrow = ">";
                string rightArrow = "<";
                int arrowOffset = 40;
                Color arrowColor = new Color(220, 40, 40, (byte)(alpha * 255));
                DrawPixelText(leftArrow, (int)(textX - arrowOffset), (int)textY, MenuFontSize, arrowColor);
                DrawPixelText(rightArrow, (int)(textX + textWidth + arrowOffset - 20), (int)textY, MenuFontSize, arrowColor);
            }
            else
            {
                textColor = new Color(200, 200, 200, (byte)(alpha * 200));
            }

            DrawPixelText(displayText, (int)textX, (int)textY, MenuFontSize, textColor);

            // "Coming soon" for Play
            if (i == 0 && selected)
            {
                string hint = "(coming soon)";
                int hintWidth = MeasureText(hint, 16);
                Color hintColor = new Color(150, 150, 150, (byte)(alpha * 180));
                DrawPixelText(hint, (int)((_screenWidth - hintWidth) / 2f), (int)(textY + spacing - 15), 16, hintColor);
            }
        }
    }

    private void DrawOptionsScreen()
    {
        // Semi-transparent overlay
        DrawRectangle(0, 0, _screenWidth, _screenHeight, new Color(0, 0, 0, 200));

        // Title
        string title = "OPTIONS";
        int titleWidth = MeasureText(title, TitleFontSize);
        DrawPixelText(title, (_screenWidth - titleWidth) / 2, _screenHeight / 2 - 120, TitleFontSize, WHITE);

        // Options
        string[] opts = { "BACK", "CREDITS" };
        float startY = _screenHeight / 2f - 20;
        float spacing = 60;

        for (int i = 0; i < opts.Length; i++)
        {
            bool selected = (i == _optionsSelected);
            int tw = MeasureText(opts[i], MenuFontSize);
            float tx = (_screenWidth - tw) / 2f;
            float ty = startY + (i * spacing);

            Color col = selected ? new Color(220, 40, 40, 255) : new Color(200, 200, 200, 200);
            DrawPixelText(opts[i], (int)tx, (int)ty, MenuFontSize, col);

            if (selected)
            {
                DrawPixelText(">", (int)(tx - 40), (int)ty, MenuFontSize, new Color(220, 40, 40, 255));
                DrawPixelText("<", (int)(tx + tw + 20), (int)ty, MenuFontSize, new Color(220, 40, 40, 255));
            }
        }

        // Hint
        string hint = "PRESS ESC TO GO BACK";
        int hw = MeasureText(hint, 18);
        DrawPixelText(hint, (_screenWidth - hw) / 2, _screenHeight - 60, 18, new Color(120, 120, 120, 255));
    }

    /// <summary>
    /// Draws text in a pixelated/blocky style using the default Raylib font
    /// with integer-scaled positioning for crisp pixel look.
    /// </summary>
    private void DrawPixelText(string text, int x, int y, int fontSize, Color color)
    {
        // Snap to pixel grid for crisp rendering
        x = x - (x % 2);
        y = y - (y % 2);
        fontSize = fontSize - (fontSize % 2);

        // Use default font with integer spacing
        DrawText(text, x, y, fontSize, color);
    }

    private void Cleanup()
    {
        StopMusicStream(_bgMusic);
        UnloadMusicStream(_bgMusic);
        UnloadSound(_clickSound);
        UnloadTexture(_introTexture);
        UnloadTexture(_mainTexture);
        CloseAudioDevice();
        CloseWindow();
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
