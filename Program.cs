using Raylib_cs;

namespace CreeperGame;

class Program
{
    static void Main(string[] args)
    {
        using var game = new Game();
        game.Run();
    }
}
