using System;

namespace CreeperGame;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Any unhandled crash is printed instead of closing the window silently,
        // which matters a lot when the game runs fullscreen.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine("FATAL: " + e.ExceptionObject);
        };

        try
        {
            using var game = new Game1();
            game.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("The game crashed:");
            Console.Error.WriteLine(ex);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Press ENTER to close...");
            try { Console.ReadLine(); } catch { /* no console attached */ }
        }
    }
}
