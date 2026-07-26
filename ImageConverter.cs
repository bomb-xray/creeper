using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace CreeperGame;

/// <summary>
/// Converts unsupported image formats to PNG for Raylib compatibility.
/// </summary>
public static class ImageConverter
{
    /// <summary>
    /// Converts any supported image format to PNG.
    /// </summary>
    /// <param name="inputPath">Path to the source image</param>
    /// <param name="outputPath">Path where PNG file will be saved</param>
    /// <returns>True if conversion successful</returns>
    public static bool ConvertToPng(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine($"Converting image: {inputPath} -> {outputPath}");
            
            using (var image = Image.Load(inputPath))
            {
                // Save as PNG with optimal compression
                var encoder = new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestSpeed,
                    FilterMethod = PngFilterMethod.Adaptive
                };
                
                image.Save(outputPath, encoder);
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
    /// Ensures an image file is in a format Raylib can load.
    /// If the original format fails, converts to PNG.
    /// </summary>
    /// <param name="dir">Directory to search</param>
    /// <param name="baseName">Base filename without extension</param>
    /// <returns>Path to a loadable image file</returns>
    public static string EnsureLoadableImage(string dir, string baseName)
    {
        // Formats Raylib definitely supports
        string[] raylibNative = { ".png", ".bmp", ".tga" };
        
        // Formats that might have issues
        string[] problematicFormats = { ".jpg", ".jpeg", ".gif" };
        
        // First, check native formats
        foreach (string ext in raylibNative)
        {
            string path = Path.Combine(dir, baseName + ext);
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        // Check problematic formats and convert if needed
        foreach (string ext in problematicFormats)
        {
            string inputPath = Path.Combine(dir, baseName + ext);
            if (File.Exists(inputPath))
            {
                string convertedPath = Path.Combine(dir, baseName + "_converted.png");
                
                // Check if already converted
                if (File.Exists(convertedPath))
                {
                    Console.WriteLine($"  -> Using cached conversion: {convertedPath}");
                    return convertedPath;
                }
                
                // Convert
                Console.WriteLine($"Found potentially problematic format: {inputPath}");
                if (ConvertToPng(inputPath, convertedPath))
                {
                    return convertedPath;
                }
            }
        }
        
        // No image found
        return null;
    }
}
