using System.Drawing;

namespace JumpServer.Menus;

public class AdminMenu
{
    private static readonly List<string?> _options = [ "Add Entry", "Edit Entry", null, "Reset Settings" ];
    
    public static void Show()
    { 
        Start:
        
        Canvas.Set(new Frame("Admin", 8, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Press Ctrl + X to return to menu", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile()
            }));
        
        for (var i = 0; i < _options.Count; i++)
        {
            Canvas.WriteFrameLine(i, 0, $"   {_options[i]}", AnsiColor.Cornsilk1);
        }

        var index = 0;
        Select(ref index, 0);

        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                return;

            if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S || keyInfo.Key == ConsoleKey.Tab)
                Select(ref index, index + 1);
            if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                Select(ref index, index - 1);
        }
        
        if (_options[index] == "Add Entry")
        {
            var result = AddEntryMenu.Show();
            if (result.Location != null)
            {
                LocationSetupMenu.Show(result.Location, result.ImportKey, result.DisablePasswordAuth, result.RandomizeSSHPort, result.RequireTOTP);
            }
            goto Start;
        }
        if (_options[index] == "Edit Entry")
        {
            EditMenu.Show();
            return;
        }
    }
    private static void Select(ref int index, int newIndex)
    {
        if (newIndex > _options.Count - 1 || newIndex < 0)
            return;
        if (_options[newIndex] == null && index < newIndex)
            newIndex++;
        if (_options[newIndex] == null && index > newIndex)
            newIndex--;

        Canvas.WriteFrameLine(index, 0, $"   {_options[index]}", AnsiColor.Cornsilk1);
        index = newIndex;
        Canvas.WriteFrameLine(index, 0, $" > {_options[index]}", AnsiColor.Black, AnsiColor.Grey93);
    }
}