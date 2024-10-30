using System.Drawing;

namespace JumpServer.Menus;

public class AdminMenu
{
    private static readonly List<string?> _options = [ "Add Entry", "Edit Entry", null, "Reset Settings" ];
    
    public static void Show()
    { 
        Start:
        Canvas.Frame.FrameTitle = "Admin";
        Canvas.Frame.FrameHeight = 8;
        Canvas.Frame.StatusBar = new DynamicBar() { Center = new Text("Press Ctrl + X to return to menu", Color.LightGoldenrodYellow, (Color?)null)};
        Canvas.RefreshFrame();
        
        for (var i = 0; i < _options.Count; i++)
        {
            Canvas.WriteFrameLine(i, 0, $"   {_options[i]}", Color.LightGoldenrodYellow);
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
            var location = EntryMenu.Show(false, null!);
            if (location != null)
            {
                LocationSetupMenu.Show(location);
            }
            goto Start;
        }
        if (_options[index] == "Edit Entry")
        {
            EditMenu.Show();
            goto Start;
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

        Canvas.WriteFrameLine(index, 0, $"   {_options[index]}", Color.LightGoldenrodYellow);
        index = newIndex;
        Canvas.WriteFrameLine(index, 0, $" > {_options[index]}", Color.Black, Color.GhostWhite);
    }
}