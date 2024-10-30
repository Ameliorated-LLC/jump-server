using System.Drawing;

namespace JumpServer;

public static class Extensions
{
    public static string ToColored(this string content, Color? foreground) => new Text(content, foreground, (Color?)null).Compile();
    public static string ToColored(this string content, Color? foreground, Color? background) => new Text(content, foreground, background).Compile();
    
    /*
   
    public static string ToColored(this string content, ConsoleColor? foreground) => new Text(content, foreground, (Color?)null).Compile();
    public static string ToColored(this string content, ConsoleColor? foreground, ConsoleColor? background) => new Text(content, foreground, background).Compile();

    public static string ToColored(this string content, ConsoleColor? foreground, Color? background) => new Text(content, foreground, background).Compile();
    public static string ToColored(this string content, Color? foreground, ConsoleColor? background) => new Text(content, foreground, background).Compile();
    
    */

}