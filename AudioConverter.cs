using System;
using System.IO;
using NAudio.Wave;

namespace CreeperGame;

/// <summary>
/// Converts unsupported audio formats (M4A, AAC, etc.) to WAV for Raylib compatibility.
/// </summary>
public static class AudioConverter
{
    /// <summary>
    /// Converts an M4A/AAC file to WAV format.
    /// </summary>
    /// <param name="inputPath">Path to the M4A/AAC file</param>
    /// <param name="outputPath">Path where WAV file will be saved</param>
    /// <returns>True if conversion successful</returns>
    public static bool ConvertM4AToWav(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine($"Converting: {inputPath} -> {outputPath}");
            
            using (var reader = new MediaFoundationReader(inputPath))
            {
                // Resample to 44100 Hz, 16-bit, stereo for best Raylib compatibility
                var targetFormat = new WaveFormat(44100, 16, 2);
                
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                {
                    resampler.ResamplerQuality = 60; // High quality
                    
                    WaveFileWriter.CreateWaveFile(outputPath, resampler);
                }
            }
            
            Console.WriteLine($"  -> Conversion successful!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Conversion failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds and converts any unsupported audio file to WAV.
    /// Returns the path to a playable WAV file.
    /// </summary>
    /// <param name="dir">Directory to search</param>
    /// <param name="baseName">Base filename without extension</param>
    /// <returns>Path to WAV file (original or converted)</returns>
    public static string EnsurePlayableAudio(string dir, string baseName)
    {
        // Check if WAV already exists
        string wavPath = Path.Combine(dir, baseName + ".wav");
        if (File.Exists(wavPath))
        {
            return wavPath;
        }

        // Check for OGG or MP3 (natively supported)
        string[] supportedFormats = { ".ogg", ".mp3", ".flac" };
        foreach (string ext in supportedFormats)
        {
            string path = Path.Combine(dir, baseName + ext);
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Check for M4A/AAC (needs conversion)
        string[] unsupportedFormats = { ".m4a", ".aac", ".wma" };
        foreach (string ext in unsupportedFormats)
        {
            string inputPath = Path.Combine(dir, baseName + ext);
            if (File.Exists(inputPath))
            {
                Console.WriteLine($"Found unsupported format: {inputPath}");
                string convertedPath = Path.Combine(dir, baseName + "_converted.wav");
                
                // Check if already converted
                if (File.Exists(convertedPath))
                {
                    Console.WriteLine($"  -> Using cached conversion: {convertedPath}");
                    return convertedPath;
                }
                
                // Convert
                if (ConvertM4AToWav(inputPath, convertedPath))
                {
                    return convertedPath;
                }
            }
        }

        // No audio file found
        return null;
    }
}
