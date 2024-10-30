﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace JumpServer;

public static class Canvas
{
    private static object _interfaceLock = new ();

    public static Frame Frame { get; private set; } = null!;
    
    private static int _screenHeight;
    private static int _screenWidth;

    private static int _frameOffsetY => CalculateCenterY(Frame.FrameHeight);
    private static int _frameOffsetX => CalculateCenterX(Frame.FrameWidth);
    
    private static Timer? _sizeCheckTimer;

    private static bool _initialized = false;
    public static void Initialize(Frame frame)
    {
        Frame = frame;
        if (_initialized)
        {
            RefreshFrame();
            return;
        }
        lock (_interfaceLock)
        {
            _initialized = true;
            UpdateWindowSize();
            
            if (_sizeCheckTimer == null) _sizeCheckTimer = new Timer(_ =>
            {
                var screenWidth = _screenWidth;
                var screenHeight = _screenHeight;
                UpdateWindowSize();
                if (_screenWidth != screenWidth || _screenHeight != screenHeight)
                {
                    Task.Run(Redraw);
                }
            }, null, 0, 100);

            _canvas = [];
            for (int i = 0; i < _screenHeight; i++)
            {
                _canvas.Add([]);
                for (int j = 0; j < _screenWidth; j++)
                {
                    _canvas.Last().Add(" ");
                }
            }
            
            Draw();
        }
    }

    private static void Redraw()
    {
        lock (_interfaceLock)
        {
            Console.Clear();
            Draw();
        }
    }

    public static void WriteCanvasToFile(string filename)
    {
        using var writer = new StreamWriter(filename);
        foreach (var row in _canvas)
        {
            writer.WriteLine(string.Concat(row));
        }
    }
    public static void WriteCanvasToScreen()
    {
        Console.Clear();
        for (var i = 0; i < _canvas.Count; i++)
        {
            Console.SetCursorPosition(0, i);
            Console.Write(string.Concat(_canvas[i]));
        }
    }

    private static List<List<string>> _canvas = null!;

    private static void Draw()
    {
        if (_screenWidth < Frame.FrameWidth || _screenHeight < Frame.FrameHeight) {
            Program.Exit(null, null);
            Console.WriteLine("Terminal screen size is too small.");
            Environment.Exit(5);
            Thread.Sleep(Timeout.Infinite);
        }
        
        var oldWidth = _canvas[0].Count;
        var oldHeight = _canvas.Count;
        var oldFrameOffsetX =  (oldWidth - Frame.FrameWidth) / 2;
        var oldFrameOffsetY =  (oldHeight - Frame.FrameHeight) / 2;
        var oldLastCursorPositionX = _lastCursorPositionX;
        var oldLastCursorPositionY = _lastCursorPositionY;

        var oldCanvas = _canvas;
        
        _canvas = new List<List<string>>();
        for (int i = 0; i < _screenHeight; i++)
        {
            _canvas.Add([]);
            for (int j = 0; j < _screenWidth; j++)
            {
                _canvas.Last().Add(" ");
            }
        }
        
        if (Frame.TitleBar?.Left != null)
            WriteCanvas(0, 0, Frame.TitleBar.Left.Compile());
        if (Frame.TitleBar?.Center != null)
            WriteCanvas(CalculateCenterX(Frame.TitleBar.Center.Content.Length), 0, Frame.TitleBar.Center.Compile());
        if (Frame.TitleBar?.Right != null)
            WriteCanvas(_screenWidth - Frame.TitleBar.Right.Content.Length, 0, Frame.TitleBar.Right.Compile());

        /*
        if (Frame.StatusBar?.Left != null)
            WriteCanvas(0, _screenHeight - 1, Frame.StatusBar.Left.Compile());
        if (Frame.StatusBar?.Center != null)
            WriteCanvas(CalculateCenterX(Frame.StatusBar.Center.Content.Length), _screenHeight - 1, Frame.StatusBar.Center.Compile());
        if (Frame.StatusBar?.Right != null)
            WriteCanvas(_screenWidth - Frame.StatusBar.Right.Content.Length, _screenHeight - 1, Frame.StatusBar.Right.Compile());
        */

        if (Frame.StatusBar?.Left != null)
            WriteCanvas(_frameOffsetX + 1, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Left.Compile());
        if (Frame.StatusBar?.Center != null)
            WriteCanvas(((Frame.FrameWidth - Frame.StatusBar.Center.Content.Length) / 2) + _frameOffsetX, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Center.Compile());
        if (Frame.StatusBar?.Right != null)
            WriteCanvas((Frame.FrameWidth - Frame.StatusBar.Right.Content.Length) + _frameOffsetX, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Right.Compile());

        
        int frameContentWidth = Frame.FrameWidth - 2; // Exclude borders

        int dashLength = Frame.FrameWidth - 6 - Frame.FrameTitle.Length;

        var frameColor = Color.LightGoldenrodYellow;
        var titleForeground = Color.Black;
        var titleBackground = frameColor;

        for (int i = 0; i < Frame.FrameHeight; i++)
        {
            if (i == 0)
            {
                WriteCanvas(_frameOffsetX, _frameOffsetY + i,
                    "┌──".ToColored(frameColor) + 
                    (" " + Frame.FrameTitle + " ").ToColored(titleForeground, titleBackground) +
                    (new string('─', dashLength) + "┐").ToColored(frameColor, (Color?)null)
                    );
            }
            else if (i == Frame.FrameHeight - 1)
            {
                WriteCanvas(_frameOffsetX, _frameOffsetY + i, ("└" + new string('─', frameContentWidth) + "┘").ToColored(frameColor, (Color?)null));
            }
            else
            {
                WriteCanvas(_frameOffsetX, _frameOffsetY + i, ("│" + new string(' ', frameContentWidth) + "│").ToColored(frameColor, (Color?)null));
            }
        }

        for (int i = 0; i < Frame.FrameHeight - 2; i++)
        {
            WriteCanvas(_frameOffsetX + 1, _frameOffsetY + 1 + i, string.Concat(oldCanvas[oldFrameOffsetY + 1 + i].GetRange(oldFrameOffsetX + 1, Frame.FrameWidth - 2)));
        }

        var newCursorLeft = Math.Min(Math.Max(_frameOffsetX + (oldLastCursorPositionX - oldFrameOffsetX), 0), _screenWidth);
        var newCursorTop = Math.Min(Math.Max(_frameOffsetY + (oldLastCursorPositionY - oldFrameOffsetY), 0), _screenHeight);
        Console.SetCursorPosition(newCursorLeft, newCursorTop);
        _lastCursorPositionX = newCursorLeft;
        _lastCursorPositionY = newCursorTop;
    }

    public static void RefreshFrame()
    {
        lock (_interfaceLock)
        {
            if (_screenWidth < Frame.FrameWidth || _screenHeight < Frame.FrameHeight) {
                Program.Exit(null, null);
                Console.WriteLine("Terminal screen size is too small.");
                Environment.Exit(5);
            }
            
            for (int i = 0; i < _screenHeight; i++)
            {
                Console.SetCursorPosition(0, i);
                Console.Write("\u001b[2K");
            }

            

            if (Frame.TitleBar?.Left != null)
                WriteCanvas(0, 0, Frame.TitleBar.Left.Compile());
            if (Frame.TitleBar?.Center != null)
                WriteCanvas(CalculateCenterX(Frame.TitleBar.Center.Content.Length), 0, Frame.TitleBar.Center.Compile());
            if (Frame.TitleBar?.Right != null)
                WriteCanvas(_screenWidth - Frame.TitleBar.Right.Content.Length, 0, Frame.TitleBar.Right.Compile());
            
            /*
            if (Frame.StatusBar?.Left != null)
                WriteCanvas(0, _screenHeight - 1, Frame.StatusBar.Left.Compile());
            if (Frame.StatusBar?.Center != null)
                WriteCanvas(CalculateCenterX(Frame.StatusBar.Center.Content.Length), _screenHeight - 1, Frame.StatusBar.Center.Compile());
            if (Frame.StatusBar?.Right != null)
                WriteCanvas(_screenWidth - Frame.StatusBar.Right.Content.Length, _screenHeight - 1, Frame.StatusBar.Right.Compile());
            */

            if (Frame.StatusBar?.Left != null)
                WriteCanvas(_frameOffsetX + 1, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Left.Compile());
            if (Frame.StatusBar?.Center != null)
                WriteCanvas(((Frame.FrameWidth - Frame.StatusBar.Center.Content.Length) / 2) + _frameOffsetX, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Center.Compile());
            if (Frame.StatusBar?.Right != null)
                WriteCanvas((Frame.FrameWidth - Frame.StatusBar.Right.Content.Length) + _frameOffsetX, _frameOffsetY + Frame.FrameHeight, Frame.StatusBar.Right.Compile());
            
            int frameContentWidth = Frame.FrameWidth - 2; // Exclude borders

            int dashLength = Frame.FrameWidth - 6 - Frame.FrameTitle.Length;

            var frameColor = Color.LightGoldenrodYellow;
            var titleForeground = Color.Black;
            var titleBackground = frameColor;
            
            for (int i = 0; i < Frame.FrameHeight; i++)
            {
                if (i == 0)
                {
                    WriteCanvas(_frameOffsetX, _frameOffsetY + i,
                        "┌──".ToColored(frameColor) +
                        (" " + Frame.FrameTitle + " ").ToColored(titleForeground, titleBackground) +
                        (new string('─', dashLength) + "┐").ToColored(frameColor)
                    );
                }
                else if (i == Frame.FrameHeight - 1)
                {
                    WriteCanvas(_frameOffsetX, _frameOffsetY + i, ("└" + new string('─', frameContentWidth) + "┘").ToColored(frameColor));
                }
                else
                {
                    WriteCanvas(_frameOffsetX, _frameOffsetY + i, ("│" + new string(' ', frameContentWidth) + "│").ToColored(frameColor));
                }
            }
        }
    }

    private static int _lastCursorPositionX = 0;
    private static int _lastCursorPositionY = 0;
    
    public static void WriteFrame(int y, int x, string text, Color? foreground = null, Color? background = null)
    {
        if (text.Contains('\n'))
            throw new Exception("New lines are not supported.");
        //if (text.Length > (Frame.FrameWidth - 2) - x)
        //    throw new Exception("Text length is longer than available frame width.");
        if (y > Frame.FrameHeight - 2)
            throw new Exception("Y index is out of range.");
        
        lock (_interfaceLock)
        {
            var xOffset = x >= 0 ? _frameOffsetX + 2 + x : _frameOffsetX + Frame.FrameWidth - 2 + x;
            var yOffset = y >= 0 ? _frameOffsetY + 2 + y : _frameOffsetY + Frame.FrameHeight - 2 + y;
            
            WriteCanvas(xOffset, yOffset, text.ToColored(foreground, background));
        }
    }
    public static void WriteFrameLine(int y, int x, string text, Color? foreground = null, Color? background = null) => WriteFrame(y, x, text + new string(' ', (Frame.FrameWidth - 4) - x - text.Length), foreground, background);
    public static void WriteFrameCentered(int y, string text, Color? foreground = null, Color? background = null) => WriteFrame(y, 0, new string(' ', ((Frame.FrameWidth - 4) - text.Length) / 2) + text + new string(' ', (Frame.FrameWidth - 4) - (((Frame.FrameWidth - 4) - text.Length) / 2) - text.Length), foreground, background);
    private static void WriteCanvas(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        WriteInternalCanvas(x, y, text);
        Console.Write(text);
    }
    private static void WriteCanvas(string text)
    {
        WriteInternalCanvas(text);
        Console.Write(text);
    }

    private static void WriteInternalCanvas(int x, int y, string text)
    {
        var characters = GetCharacters(text);
        if (characters == null)
        {
            _canvas[y][x] = text + _canvas[y][x];
            return;
        }

        for (var i = 0; i < characters.Count; i++)
        {
            _canvas[y][x + i] = characters[i];
        }
        
        _lastCursorPositionX = x + characters.Count;
        _lastCursorPositionY = y;
    }
    private static void WriteInternalCanvas(string text) => WriteInternalCanvas(_lastCursorPositionX, _lastCursorPositionY, text);
    
    public static List<string>? GetCharacters(string text)
    {
        List<string> result = new List<string>();
        Regex regex = new Regex(@"\u001b\[[0-9;?]*[A-Za-z]");
        int currentIndex = 0;
        StringBuilder currentEscapeCodes = new StringBuilder();
        int textLength = text.Length;

        // Find all matches of ANSI escape sequences
        var matches = regex.Matches(text);

        // Use an index to keep track of the next match
        int matchIndex = 0;

        while (currentIndex < textLength)
        {
            // Check if currentIndex is at the start of a match
            if (matchIndex < matches.Count && matches[matchIndex].Index == currentIndex)
            {
                string escapeCode = matches[matchIndex].Value;

                // Append the escape code to currentEscapeCodes
                currentEscapeCodes.Append(escapeCode);

                // Advance currentIndex by the length of the escape code
                currentIndex += matches[matchIndex].Length;
                // Advance matchIndex to the next match
                matchIndex++;
            }
            else
            {
                // Process character
                result.Add(currentEscapeCodes.ToString() + text[currentIndex]);
                currentEscapeCodes.Clear();
                // Advance currentIndex
                currentIndex++;
            }
        }

        // After processing all characters, check if any remaining escape codes
        while (matchIndex < matches.Count)
        {
            string escapeCode = matches[matchIndex].Value;
            currentEscapeCodes.Append(escapeCode);
            matchIndex++;
        }

        // If currentEscapeCodes is not empty, append it to the last element
        if (currentEscapeCodes.Length > 0 && result.Count > 0)
        {
            result[^1] += currentEscapeCodes.ToString();
        }
        else if (currentEscapeCodes.Length > 0)
        {
            return null;
        }

        return result;
    }
    private static int CalculateCenterX(int itemWidth) => (_screenWidth - itemWidth) / 2;
    
    private static int CalculateCenterY(int itemHeight) =>  (_screenHeight - itemHeight) / 2;

    private static void SignalHandler()
    {
        /*
        var signal = new UnixSignal(Signum.SIGWINCH);
        _signalHandlerReady.Set();
        
        while (true)
        {
            UnixSignal.WaitAny([ signal ]);
            _pendingRedraw = true;
        }
        */
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel; // Unused in this context
        public ushort ws_ypixel; // Unused in this context
    }

    // The ioctl system call to get window size
    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, ref Winsize size);

    private const ulong TIOCGWINSZ = 0x5413;

    // File descriptor for the standard input (terminal)
    private const int STDIN_FILENO = 0;

    private static void UpdateWindowSize()
    {
        Winsize ws = new Winsize();
        if (ioctl(STDIN_FILENO, TIOCGWINSZ, ref ws) == -1)
            throw new Exception("Failed to get window size.");
        _screenWidth = ws.ws_col;
        _screenHeight = ws.ws_row;
    }
    
    public static bool OptionPrompt(string title, string message, string option1, string option2, bool option2Disabled = false)
    {
        Canvas.Initialize(new Frame(title, 10, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, Color.GhostWhite, (Color?)null) }
            //new DynamicBar() { Center = new Text("Press Ctrl + X to cancel setup", Color.LightGoldenrodYellow, (Color?)null) }
        ));
        
        var messageOffset = (Canvas.Frame.FrameWidth - 4 - message.Length) / 2;
        
        Canvas.WriteFrameLine(1, messageOffset, message, Color.LightGoldenrodYellow);
                
        option1 = $" {option1} ";
        option2 = $" {option2} ";
        
        var optionSpaces = new string(' ', 22 - (option1.Length + option2.Length));
        var option1Offset = (Canvas.Frame.FrameWidth - 4 - 22) / 2;
        var option2Offset = option1Offset + option1.Length + optionSpaces.Length;
                
        Canvas.WriteFrame(4, option1Offset, option1, Color.Black, Color.FromArgb(215, 215, 215));
        Canvas.WriteFrame(4, option2Offset, option2, option2Disabled ? Color.FromArgb(180, 180, 180) : Color.White, option2Disabled ? Color.FromArgb(60, 60, 60) : Color.FromArgb(85, 85, 85));
        string currentSelection = option1;
        ConsoleKeyInfo keyInfo;
        while (Console.KeyAvailable) Console.ReadKey(true);
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if ((keyInfo.Key == ConsoleKey.RightArrow || keyInfo.Key == ConsoleKey.D || keyInfo.Key == ConsoleKey.Tab) && currentSelection == option1 && !option2Disabled)
            {
                currentSelection = option2;
                Canvas.WriteFrame(4, option1Offset, option1, Color.White, Color.FromArgb(85, 85, 85));
                Canvas.WriteFrame(4, option2Offset, option2, Color.Black, Color.FromArgb(215, 215, 215));
            } else if ((keyInfo.Key == ConsoleKey.LeftArrow || keyInfo.Key == ConsoleKey.A || keyInfo.Key == ConsoleKey.Tab) && currentSelection == option2)
            {
                currentSelection = option1;
                Canvas.WriteFrame(4, option1Offset, option1, Color.Black, Color.FromArgb(215, 215, 215));
                Canvas.WriteFrame(4, option2Offset, option2, Color.White, Color.FromArgb(85, 85, 85));
            }
        }
        return currentSelection == option1;
    }
}