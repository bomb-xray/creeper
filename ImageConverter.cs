using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace CreeperGame;

/// <summary>
/// Re-encodes images to plain 32-bit PNG. Progressive/CMYK JPEGs and some exotic
/// PNG variants fail to load in the graphics backend, so they are converted once
/// and the result is cached next to the original file.
/// </summary>
public static class ImageConverter
{
    /// <summary>Formats the graphics backend loads reliably as-is.</summary>
    private static readonly string[] SafeFormats = { ".png", ".bmp" };

    /// <summary>Formats that are re-encoded before use.</summary>
    private static readonly string[] RiskyFormats = { ".jpg", ".jpeg", ".gif", ".tga", ".webp", ".tif", ".tiff" };

    /// <summary>
    /// Returns a path to an image named <paramref name="baseName"/> that is safe to load.
    /// </summary>
    public static string? EnsureLoadableImage(string dir, string baseName)
    {
        if (!Directory.Exists(dir)) return null;

        foreach (string ext in SafeFormats)
        {
            string path = Path.Combine(dir, baseName + ext);
            if (File.Exists(path)) return path;
        }

        foreach (string ext in RiskyFormats)
        {
            string inputPath = Path.Combine(dir, baseName + ext);
            if (!File.Exists(inputPath)) continue;

            string outputPath = Path.Combine(dir, baseName + "_converted.png");

            if (File.Exists(outputPath) &&
                File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(inputPath))
            {
                Console.WriteLine($"Using cached image conversion: {outputPath}");
                return outputPath;
            }

            Console.WriteLine($"Image format needs conversion: {inputPath}");
            if (ConvertToPng(inputPath, outputPath)) return outputPath;

            // Conversion failed; hand back the original and let the caller try.
            return inputPath;
        }

        return null;
    }

    /// <summary>Re-encodes any ImageSharp-readable file as a 32-bit RGBA PNG.</summary>
    public static bool ConvertToPng(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine($"Converting image: {inputPath} -> {outputPath}");

            using var image = Image.Load<Rgba32>(inputPath);

            var encoder = new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                BitDepth = PngBitDepth.Bit8,
                CompressionLevel = PngCompressionLevel.BestSpeed
            };

            image.Save(outputPath, encoder);

            Console.WriteLine($"  -> image conversion OK ({image.Width}x{image.Height})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> image conversion failed: {ex.Message}");
            try
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
            catch
            {
                // Ignored.
            }
            return false;
        }
    }
}
