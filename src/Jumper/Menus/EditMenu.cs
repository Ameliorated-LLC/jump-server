using System.Drawing;

namespace JumpServer.Menus;

public class EditMenu
{
    public static void Show()
    {
        Canvas.Set(new Frame("Edit Entry", Configuration.Current.Locations.Count + 4, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Press Escape to return to menu", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() }));

        if (Configuration.Current.Locations.Count < 1)
            throw new ArgumentException();

        for (var i = 0; i < Configuration.Current.Locations.Count; i++)
        {
            var host = Configuration.Current.Locations[i].IP + (Configuration.Current.Locations[i].Port == 22 ? null : ":" + Configuration.Current.Locations[i].Port);
            
            Canvas.WriteFrameLine(i, 0, $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})", AnsiColor.Cornsilk1);
            Canvas.WriteFrame(i, -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})".Length - 10).Length - 1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})".Length - 10), Configuration.Current.Locations[i].Connected == null ? AnsiColor.Grey23 : Configuration.Current.Locations[i].Connected!.Value ? AnsiColor.Green : AnsiColor.Red);
        }

        var index = 0;
        Select(ref index, 0);

        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (keyInfo.Key == ConsoleKey.Escape)
                return;

            if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S || keyInfo.Key == ConsoleKey.Tab)
                Select(ref index, index + 1);
            if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                Select(ref index, index - 1);
        }

        if (index != -1)
        {
            EntryMenu.Edit(Configuration.Current.Locations[index]);
            File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
        }
    }
    
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
    
    private static void Select(ref int index, int newIndex)
    {
        if (newIndex > Configuration.Current.Locations.Count - 1 || newIndex < 0)
            return;
        
        var host = Configuration.Current.Locations[index].IP + (Configuration.Current.Locations[index].Port == 22 ? null : ":" + Configuration.Current.Locations[index].Port);

        Canvas.WriteFrameLine(index, 0, $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})", AnsiColor.Cornsilk1);
        Canvas.WriteFrame(index,
            -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10).Length -
            1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10),
            Configuration.Current.Locations[index].Connected == null ? AnsiColor.Grey23 : Configuration.Current.Locations[index].Connected!.Value ? AnsiColor.Green : AnsiColor.Red);
        index = newIndex;
        
        host = Configuration.Current.Locations[index].IP + (Configuration.Current.Locations[index].Port == 22 ? null : ":" + Configuration.Current.Locations[index].Port);
        
        Canvas.WriteFrameLine(index, 0, $" > {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})", AnsiColor.Black, AnsiColor.Grey93);
        Canvas.WriteFrame(index,
            -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10).Length -
            1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10),
            Configuration.Current.Locations[index].Connected == null ? AnsiColor.Grey23 : Configuration.Current.Locations[index].Connected!.Value ? AnsiColor.Green : AnsiColor.Red, AnsiColor.Grey93);

    }
}