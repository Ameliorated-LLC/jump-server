using System.Drawing;

namespace JumpServer;

public class Frame
{
    public Frame(string frameTitle, int frameHeight, int frameWidth, DynamicBar? titleBar = null, DynamicBar? statusBar = null) => (FrameTitle, FrameHeight, FrameWidth, TitleBar, StatusBar) = (frameTitle, frameHeight, frameWidth, titleBar, statusBar);
    public string FrameTitle { get; set; }
    public int FrameHeight { get; set; }
    public int FrameWidth { get; set; }
    public DynamicBar? TitleBar { get; set; }
    public DynamicBar? StatusBar { get; set; }
}

public class DynamicBar
{
    public Text? Left { get; set; }
    public Text? Center { get; set; }
    public Text? Right { get; set; }
}

public class Text
{
    public Text(string content, Color? foreground = null, Color? background = null) => (Content, Foreground, Background) = (content, foreground, background);
    public Text(string content, ConsoleColor? foreground = null, Color? background = null) => (Content, Foreground, Background) = (content, Utilites.ConsoleColorToRGB(foreground), background);
    public Text(string content, Color? foreground = null, ConsoleColor? background = null) => (Content, Foreground, Background) = (content, foreground, Utilites.ConsoleColorToRGB(background));
    public Text(string content, ConsoleColor? foreground = null, ConsoleColor? background = null) => (Content, Foreground, Background) = (content, Utilites.ConsoleColorToRGB(foreground), Utilites.ConsoleColorToRGB(background));

    public string Content { get; set; }
    public Color? Foreground { get; set; }
    public Color? Background { get; set; }

    public string Compile()
    {
        if (Foreground == null && Background == null) return Content;
        
        var foreground = Foreground == null ? "" : $"\x1b[38;2;{Foreground.Value.R};{Foreground.Value.G};{Foreground.Value.B}m";
        var background = Background == null ? "" : $"\x1b[48;2;{Background.Value.R};{Background.Value.G};{Background.Value.B}m";
        var resetForeground = Foreground != null ? "\x1b[39m" : "";
        var resetBackground = Background != null ? "\x1b[49m" : "";
        return $"{foreground}{background}{Content}{resetForeground}{resetBackground}";
    }
}

