using System.Drawing;

namespace JumpServer;

public static class Extensions
{
    public static string ToColored(this string content, AnsiColor? foreground) => new Text(content, foreground, (AnsiColor?)null).Compile();
    public static string ToColored(this string content, AnsiColor? foreground, AnsiColor? background) => new Text(content, foreground, background).Compile();
    public static int RealLength(this string content) => Canvas.GetCharacters(content)?.Count ?? 0;

    
    /*
   
    public static string ToColored(this string content, ConsoleColor? foreground) => new Text(content, foreground, (AnsiColor?)null).Compile();
    public static string ToColored(this string content, ConsoleColor? foreground, ConsoleColor? background) => new Text(content, foreground, background).Compile();

    public static string ToColored(this string content, ConsoleColor? foreground, Color? background) => new Text(content, foreground, background).Compile();
    public static string ToColored(this string content, Color? foreground, ConsoleColor? background) => new Text(content, foreground, background).Compile();
    
    */

}