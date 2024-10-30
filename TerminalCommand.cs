namespace JumpServer;

// Enum to represent common terminal commands
public enum TerminalCommand
{
    ClearScreen,
    SaveScreen,     // smcup
    RestoreScreen,  // rmcup
    EnableBold,     // bold
    DisableBold,    // sgr0
    HideCursor,     // civis
    ShowCursor,     // cnorm
    SaveCursorPos,  // sc - Save cursor position
    RestoreCursorPos // rc - Restore cursor position
}
public static class TerminalCommands
{
    // Array to map TerminalCommand enum to the corresponding escape code.
    private static readonly string[] CommandEscapeCodes = {
        "\u001b[H\u001b[2J",     // ClearScreen - Equivalent to tput clear
        "\u001b[?1049h",        // SaveScreen - Equivalent to tput smcup (alternate buffer on)
        "\u001b[?1049l",        // RestoreScreen - Equivalent to tput rmcup (alternate buffer off)
        "\u001b[1m",            // EnableBold - Equivalent to tput bold
        "\u001b[0m",            // DisableBold - Equivalent to tput sgr0 (reset all attributes)
        "\u001b[?25l",          // HideCursor - Equivalent to tput civis
        "\u001b[?25h",          // ShowCursor - Equivalent to tput cnorm
        "\u001b[s",             // SaveCursorPos - Equivalent to tput sc (save cursor position)
        "\u001b[u"              // RestoreCursorPos - Equivalent to tput rc (restore cursor position)
    };

    // Function to execute the terminal command by writing the escape code to the console
    public static void Execute(TerminalCommand command)
    {
        try
        {
            string escapeCode = CommandEscapeCodes[(int)command];
            Console.Write(escapeCode);
        }
        catch (IndexOutOfRangeException)
        {
            Console.WriteLine("Invalid terminal command.");
        }
    }
}