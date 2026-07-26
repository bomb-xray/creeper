using Microsoft.Xna.Framework.Audio;
using System;
using System.Diagnostics;
using System.IO;

namespace CreeperGame;

/// <summary>
/// Locates the ffmpeg executable and performs one-off media chores with it.
/// Everything here degrades gracefully: if ffmpeg is missing the callers simply
/// skip the feature instead of failing.
/// </summary>
public static class FFmpeg
{
    private static string? _cachedPath;
    private static bool _searched;

    /// <summary>Directory that holds the game assets; set once at startup.</summary>
    public static string AssetDirectory { get; set; } = "assets";

    /// <summary>
    /// Returns a usable ffmpeg path, or null. The result is cached, including the
    /// negative result, so we only pay for the search once.
    /// </summary>
    public static string? FindExecutable()
    {
        if (_searched) return _cachedPath;
        _searched = true;

        string exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        // Bundled next to the game or in the assets folder takes priority, so a
        // player can drop ffmpeg in without touching their system PATH.
        string[] localCandidates =
        {
            Path.Combine(AssetDirectory, exeName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", exeName),
            exeName
        };

        foreach (string candidate in localCandidates)
        {
            if (File.Exists(candidate))
            {
                _cachedPath = Path.GetFullPath(candidate);
                Console.WriteLine($"Found ffmpeg: {_cachedPath}");
                return _cachedPath;
            }
        }

        // Fall back to PATH.
        if (CanRun(exeName))
        {
            _cachedPath = exeName;
            Console.WriteLine("Found ffmpeg on PATH.");
            return _cachedPath;
        }

        return null;
    }

    private static bool CanRun(string exeName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exeName,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null) return false;

            process.WaitForExit(3000);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a video's audio track to a cached WAV and loads it as a SoundEffect.
    /// Returns null when the file has no audio or the extraction fails.
    /// </summary>
    public static SoundEffect? ExtractAudio(string ffmpegPath, string videoPath)
    {
        try
        {
            string cacheDir = Path.Combine(Path.GetDirectoryName(videoPath) ?? ".", "cache");
            Directory.CreateDirectory(cacheDir);

            string wavPath = Path.Combine(cacheDir,
                Path.GetFileNameWithoutExtension(videoPath) + "_audio.wav");

            bool cached = File.Exists(wavPath) &&
                          File.GetLastWriteTimeUtc(wavPath) >= File.GetLastWriteTimeUtc(videoPath);

            if (!cached)
            {
                Console.WriteLine($"Extracting video audio -> {wavPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-hide_banner");
                startInfo.ArgumentList.Add("-loglevel");
                startInfo.ArgumentList.Add("error");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(videoPath);
                startInfo.ArgumentList.Add("-vn");
                startInfo.ArgumentList.Add("-acodec");
                startInfo.ArgumentList.Add("pcm_s16le");
                startInfo.ArgumentList.Add("-ar");
                startInfo.ArgumentList.Add("44100");
                startInfo.ArgumentList.Add("-ac");
                startInfo.ArgumentList.Add("2");
                startInfo.ArgumentList.Add(wavPath);

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                process.WaitForExit(30000);

                if (!File.Exists(wavPath)) return null;
            }

            using var stream = File.OpenRead(wavPath);
            return SoundEffect.FromStream(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not extract the video audio: {ex.Message}");
            return null;
        }
    }
}
