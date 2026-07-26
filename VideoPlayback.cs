using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CreeperGame;

/// <summary>
/// Plays a video by piping raw RGBA frames out of an ffmpeg process and uploading
/// them to a texture each frame.
///
/// MonoGame's DesktopGL backend has no VideoPlayer at all, and the FFmpeg .NET
/// bindings need exact native library versions to be shipped alongside the game.
/// Talking to the ffmpeg executable over stdout avoids both problems: there is no
/// native interop, and any format ffmpeg understands just works.
///
/// If ffmpeg cannot be found the playback reports itself as finished immediately,
/// so callers can simply skip ahead.
/// </summary>
public class VideoPlayback : IDisposable
{
    /// <summary>Frames decoded ahead of the playhead. Keeps memory bounded.</summary>
    private const int MaxQueuedFrames = 4;

    /// <summary>Largest width we ask ffmpeg for; the GPU upscales to the screen.</summary>
    private const int MaxDecodeWidth = 960;

    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly int _frameBytes;
    private readonly double _frameRate;

    private Process? _ffmpeg;
    private Thread? _readerThread;
    private volatile bool _stopRequested;
    private volatile bool _readerDone;

    /// <summary>Decoded frames waiting to be shown.</summary>
    private readonly BlockingCollection<byte[]> _readyFrames =
        new BlockingCollection<byte[]>(MaxQueuedFrames);

    /// <summary>Recycled buffers, so decoding does not allocate every frame.</summary>
    private readonly ConcurrentQueue<byte[]> _freeBuffers = new ConcurrentQueue<byte[]>();

    private readonly Texture2D _texture;
    private readonly Stopwatch _clock = new Stopwatch();

    private SoundEffect? _audio;
    private SoundEffectInstance? _audioInstance;

    private int _framesPresented;
    private bool _disposed;

    /// <summary>True once the video has played to the end (or could not start at all).</summary>
    public bool Finished { get; private set; }

    /// <summary>The texture holding the most recently presented frame.</summary>
    public Texture2D Texture => _texture;

    /// <summary>True if at least one frame has been shown.</summary>
    public bool HasFrame { get; private set; }

    private VideoPlayback(GraphicsDevice device, int width, int height, double frameRate)
    {
        _frameWidth = width;
        _frameHeight = height;
        _frameRate = frameRate;
        _frameBytes = width * height * 4;
        _texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
    }

    /// <summary>
    /// Starts decoding <paramref name="videoPath"/>. Returns null when ffmpeg is
    /// unavailable or the process refuses to start.
    /// </summary>
    /// <param name="targetAspectWidth">Screen width, used to pick the output aspect.</param>
    /// <param name="targetAspectHeight">Screen height, used to pick the output aspect.</param>
    public static VideoPlayback? TryStart(GraphicsDevice device, string videoPath,
        int targetAspectWidth, int targetAspectHeight, float volume)
    {
        string? ffmpeg = FFmpeg.FindExecutable();
        if (ffmpeg == null)
        {
            Console.WriteLine("ffmpeg was not found, so the video cannot be played.");
            Console.WriteLine("Put ffmpeg.exe in the assets folder or on PATH to enable it.");
            return null;
        }

        // Output keeps the screen aspect; the source is letterboxed into it, so the
        // result can simply be stretched over the whole screen when drawn.
        int width = Math.Min(MaxDecodeWidth, targetAspectWidth);
        int height = (int)Math.Round(width * (double)targetAspectHeight / targetAspectWidth);
        width &= ~1;
        height &= ~1;

        const double frameRate = 30.0;

        var playback = new VideoPlayback(device, width, height, frameRate);

        try
        {
            playback.StartProcess(ffmpeg, videoPath, volume);
            return playback;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not start video playback: {ex.Message}");
            playback.Dispose();
            return null;
        }
    }

    private void StartProcess(string ffmpegPath, string videoPath, float volume)
    {
        // Audio is extracted once to a cached WAV and played through the normal
        // sound pipeline, which keeps this class purely about pictures.
        _audio = FFmpeg.ExtractAudio(ffmpegPath, videoPath);
        if (_audio != null)
        {
            _audioInstance = _audio.CreateInstance();
            _audioInstance.Volume = MathHelper.Clamp(volume, 0f, 1f);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList quotes each entry, which matters because asset paths often
        // contain spaces.
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-an");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add(
            $"scale={_frameWidth}:{_frameHeight}:force_original_aspect_ratio=decrease," +
            $"pad={_frameWidth}:{_frameHeight}:(ow-iw)/2:(oh-ih)/2:black," +
            $"fps={_frameRate}");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("rawvideo");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("rgba");
        startInfo.ArgumentList.Add("pipe:1");

        _ffmpeg = Process.Start(startInfo);
        if (_ffmpeg == null) throw new InvalidOperationException("Process.Start returned null.");

        // Draining stderr prevents ffmpeg from blocking on a full error pipe.
        _ffmpeg.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine($"[ffmpeg] {e.Data}");
        };
        _ffmpeg.BeginErrorReadLine();

        _readerThread = new Thread(ReadFrames)
        {
            IsBackground = true,
            Name = "video-decoder"
        };
        _readerThread.Start();

        Console.WriteLine($"Playing video: {videoPath} ({_frameWidth}x{_frameHeight} @ {_frameRate}fps)");
    }

    /// <summary>Background loop: pulls raw frames off the pipe until it closes.</summary>
    private void ReadFrames()
    {
        try
        {
            Stream output = _ffmpeg!.StandardOutput.BaseStream;

            while (!_stopRequested)
            {
                if (!_freeBuffers.TryDequeue(out byte[]? buffer))
                {
                    buffer = new byte[_frameBytes];
                }

                try
                {
                    // Throws at end of stream, which is the normal way out.
                    output.ReadExactly(buffer, 0, _frameBytes);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                if (_stopRequested) break;

                try
                {
                    _readyFrames.Add(buffer);
                }
                catch (InvalidOperationException)
                {
                    break; // Collection completed while we were adding.
                }
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested) Console.WriteLine($"Video decoding stopped: {ex.Message}");
        }
        finally
        {
            _readerDone = true;
            try { _readyFrames.CompleteAdding(); } catch { /* already completed */ }
        }
    }

    /// <summary>
    /// Advances playback. Call once per game frame; uploads a new frame to the
    /// texture when the wall clock says it is due.
    /// </summary>
    public void Update()
    {
        if (_disposed || Finished) return;

        if (!_clock.IsRunning)
        {
            _clock.Start();
            try { _audioInstance?.Play(); }
            catch (Exception ex) { Console.WriteLine($"Video audio failed: {ex.Message}"); }
        }

        // How many frames should have been shown by now.
        int targetFrame = (int)(_clock.Elapsed.TotalSeconds * _frameRate);

        // Catch up if we fell behind, but never block waiting for the decoder.
        while (_framesPresented <= targetFrame && _readyFrames.TryTake(out byte[]? frame))
        {
            _texture.SetData(frame);
            _freeBuffers.Enqueue(frame);
            _framesPresented++;
            HasFrame = true;
        }

        if (_readerDone && _readyFrames.Count == 0)
        {
            Finished = true;
        }
    }

    /// <summary>Stops playback immediately (used when the player skips).</summary>
    public void Stop()
    {
        Finished = true;
        _stopRequested = true;

        try { _audioInstance?.Stop(); } catch { /* nothing to do */ }

        try
        {
            if (_ffmpeg is { HasExited: false }) _ffmpeg.Kill(true);
        }
        catch
        {
            // The process may already be gone.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        try { _readyFrames.CompleteAdding(); } catch { /* already completed */ }
        _readerThread?.Join(500);

        while (_readyFrames.TryTake(out _)) { }
        _readyFrames.Dispose();

        _ffmpeg?.Dispose();
        _audioInstance?.Dispose();
        _audio?.Dispose();
        _texture?.Dispose();
    }
}
