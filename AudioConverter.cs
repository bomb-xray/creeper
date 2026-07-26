using System;
using System.IO;
using NAudio.Wave;

namespace CreeperGame;

/// <summary>
/// MonoGame's DesktopGL backend can only decode WAV through SoundEffect.FromStream,
/// so every other audio format is transcoded to 16-bit 44.1 kHz stereo WAV on first
/// run and cached next to the original file.
/// </summary>
public static class AudioConverter
{
    /// <summary>Extensions that are loaded directly without any conversion.</summary>
    private static readonly string[] NativeFormats = { ".wav" };

    /// <summary>Extensions we try to transcode, in order of preference.</summary>
    private static readonly string[] ConvertibleFormats =
    {
        ".mp3", ".m4a", ".aac", ".wma", ".flac", ".aiff", ".aif", ".ogg"
    };

    /// <summary>
    /// Finds an audio file named <paramref name="baseName"/> in <paramref name="dir"/>
    /// and returns the path to a WAV file the engine can actually play.
    /// </summary>
    public static string? EnsurePlayableAudio(string dir, string baseName)
    {
        if (!Directory.Exists(dir)) return null;

        foreach (string ext in NativeFormats)
        {
            string path = Path.Combine(dir, baseName + ext);
            if (File.Exists(path)) return path;
        }

        foreach (string ext in ConvertibleFormats)
        {
            string inputPath = Path.Combine(dir, baseName + ext);
            if (!File.Exists(inputPath)) continue;

            string outputPath = Path.Combine(dir, baseName + "_converted.wav");

            // Reuse the cached WAV unless the source file is newer.
            if (File.Exists(outputPath) &&
                File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(inputPath))
            {
                Console.WriteLine($"Using cached audio conversion: {outputPath}");
                return outputPath;
            }

            Console.WriteLine($"Audio format needs conversion: {inputPath}");
            if (ConvertToWav(inputPath, outputPath)) return outputPath;

            // Conversion failed (e.g. no codec for OGG on this machine):
            // hand back the original and let the engine try its own decoder.
            return inputPath;
        }

        return null;
    }

    /// <summary>Transcodes any NAudio-readable file to 44.1 kHz 16-bit stereo WAV.</summary>
    public static bool ConvertToWav(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine($"Converting audio: {inputPath} -> {outputPath}");

            using WaveStream reader = CreateReader(inputPath);
            var targetFormat = new WaveFormat(44100, 16, 2);

            if (reader.WaveFormat.Equals(targetFormat))
            {
                WaveFileWriter.CreateWaveFile(outputPath, reader);
            }
            else if (OperatingSystem.IsWindows())
            {
                using var resampler = new MediaFoundationResampler(reader, targetFormat)
                {
                    ResamplerQuality = 60
                };
                WaveFileWriter.CreateWaveFile(outputPath, resampler);
            }
            else
            {
                // Portable fallback: NAudio's managed resampler.
                var resampled = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                    reader.ToSampleProvider(), targetFormat.SampleRate);
                WaveFileWriter.CreateWaveFile16(outputPath, resampled);
            }

            Console.WriteLine("  -> audio conversion OK");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> audio conversion failed: {ex.Message}");
            TryDelete(outputPath);
            return false;
        }
    }

    private static WaveStream CreateReader(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".wav" => new WaveFileReader(path),
            ".aiff" or ".aif" => new AiffFileReader(path),
            // MediaFoundation decodes MP3/M4A/AAC/WMA on Windows; elsewhere
            // AudioFileReader picks whatever decoder NAudio has available.
            _ => OperatingSystem.IsWindows()
                ? new MediaFoundationReader(path)
                : new AudioFileReader(path)
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignored: a leftover partial file is harmless.
        }
    }
}
