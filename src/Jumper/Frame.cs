using System.Drawing;

namespace JumpServer;

public class Frame
{
    public Frame(string frameTitle, int frameHeight, int frameWidth, DynamicBar? titleBar = null, DynamicBar? statusBar = null, BottomBar? bottomBar = null) => (FrameTitle, FrameHeight, FrameWidth, TitleBar, StatusBar, BottomBar) = (frameTitle, frameHeight, frameWidth, titleBar, statusBar, bottomBar);
    public string FrameTitle { get; set; }
    public int FrameHeight { get; set; }
    public int FrameWidth { get; set; }
    public DynamicBar? TitleBar { get; set; }
    public DynamicBar? StatusBar { get; set; }
    public BottomBar? BottomBar { get; set; }
}

public class DynamicBar
{
    public string? Left { get; set; }
    public string? Center { get; set; }
    public string? Right { get; set; }
}
public class BottomBar
{
    public string[] Items { get; set; } = null!;
    public int MaxPadding = 5;
    public int MinPadding = 1;
}

public class Text
{
    public Text(string content, AnsiColor? foreground = null, AnsiColor? background = null) => (Content, Foreground, Background) = (content, foreground, background);
    public string Content { get; set; }
    public AnsiColor? Foreground { get; set; }
    public AnsiColor? Background { get; set; }

    public string Compile()
    {
        if (Foreground == null && Background == null) return Content;

        var foreground = Foreground == null ? "" : $"\x1b[38;5;{(int)Foreground.Value}m";
        var background = Background == null ? "" : $"\x1b[48;5;{(int)Background.Value}m";
        var resetForeground = Foreground != null ? "\x1b[39m" : "";
        var resetBackground = Background != null ? "\x1b[49m" : "";
        return $"{foreground}{background}{Content}{resetForeground}{resetBackground}";
    }
}

