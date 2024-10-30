using System.Drawing;

namespace JumpServer;

public static class Utilites
{
    public static Color? ConsoleColorToRGB(ConsoleColor? color)
    {
        if (color == null) return null;
        switch (color)
        {
            case ConsoleColor.Black: return Color.FromArgb(0, 0, 0);
            case ConsoleColor.DarkBlue: return Color.FromArgb(0, 0, 139);
            case ConsoleColor.DarkGreen: return Color.FromArgb(0, 100, 0);
            case ConsoleColor.DarkCyan: return Color.FromArgb(0, 139, 139);
            case ConsoleColor.DarkRed: return Color.FromArgb(139, 0, 0);
            case ConsoleColor.DarkMagenta: return Color.FromArgb(139, 0, 139);
            case ConsoleColor.DarkYellow: return Color.FromArgb(255, 140, 0);
            case ConsoleColor.Gray: return Color.FromArgb(169, 169, 169);
            case ConsoleColor.DarkGray: return Color.FromArgb(105, 105, 105);
            case ConsoleColor.Blue: return Color.FromArgb(0, 0, 255);
            case ConsoleColor.Green: return Color.FromArgb(0, 255, 0);
            case ConsoleColor.Cyan: return Color.FromArgb(0, 255, 255);
            case ConsoleColor.Red: return Color.FromArgb(255, 0, 0);
            case ConsoleColor.Magenta: return Color.FromArgb(255, 0, 255);
            case ConsoleColor.Yellow: return Color.FromArgb(255, 255, 0);
            case ConsoleColor.White: return Color.FromArgb(255, 255, 255);
            default: throw new ArgumentOutOfRangeException(nameof(color), color, null);
        }
    }
}