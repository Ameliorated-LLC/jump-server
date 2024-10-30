using System.Drawing;

namespace JumpServer.Menus;

public class EditMenu
{
    public static void Show()
    {
        Canvas.Initialize(new Frame("Edit Entry", Configuration.Current.Locations.Count + 4, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, Color.GhostWhite, (Color?)null) },
            new DynamicBar() { Center = new Text("Press Ctrl + X to return to menu", Color.LightGoldenrodYellow, (Color?)null) }));

        if (Configuration.Current.Locations.Count < 1)
            throw new ArgumentException();

        for (var i = 0; i < Configuration.Current.Locations.Count; i++)
        {
            var host = Configuration.Current.Locations[i].IP + (Configuration.Current.Locations[i].Port == 22 ? null : ":" + Configuration.Current.Locations[i].Port);
            
            Canvas.WriteFrameLine(i, 0, $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})", Color.LightGoldenrodYellow);
            Canvas.WriteFrame(i, -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})".Length - 10).Length - 1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[i].Name} ({Configuration.Current.Locations[i].Username})".Length - 10), Configuration.Current.Locations[i].Connected == null ? Color.DimGray : Configuration.Current.Locations[i].Connected!.Value ? Color.Green : Color.Red);
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

        if (index != -1)
        {
            EntryMenu.Show(true, Configuration.Current.Locations[index]);
            Program.Exit(null, null);
            Console.WriteLine();
            Environment.Exit(0);
            Thread.Sleep(Timeout.Infinite);
        }

        if (index == -1)
        {

            AdminMenu.Show();

            return;
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

        Canvas.WriteFrameLine(index, 0, $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})", Color.LightGoldenrodYellow);
        Canvas.WriteFrame(index,
            -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10).Length -
            1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10),
            Configuration.Current.Locations[index].Connected == null ? Color.DimGray : Configuration.Current.Locations[index].Connected!.Value ? Color.Green : Color.Red);
        index = newIndex;
        
        host = Configuration.Current.Locations[index].IP + (Configuration.Current.Locations[index].Port == 22 ? null : ":" + Configuration.Current.Locations[index].Port);
        
        Canvas.WriteFrameLine(index, 0, $" > {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})", Color.Black, Color.GhostWhite);
        Canvas.WriteFrame(index,
            -Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10).Length -
            1, Truncate(host, Canvas.Frame.FrameWidth - 4 - $"   {Configuration.Current.Locations[index].Name} ({Configuration.Current.Locations[index].Username})".Length - 10),
            Configuration.Current.Locations[index].Connected == null ? Color.DimGray : Configuration.Current.Locations[index].Connected!.Value ? Color.Green : Color.Red, Color.GhostWhite);

    }
}