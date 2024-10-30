using System.Drawing;
using System.Text.RegularExpressions;

namespace JumpServer.Menus;

public class EntryMenu
{
    private static List<Option> Options =
    [
        new Option() { Type = OptionType.TextInputField, Name = "Name", MinLength = 1 },
        new Option() { Type = OptionType.TextInputField, Name = "Username", Regex = @"^[a-z][a-z0-9_-]{0,31}$", MinLength = 1 },
        new Option() { Type = OptionType.TextInputField, Name = "IP Address", Regex = @"^(?!.*\.\.)[A-Za-z0-9.-]+$", MinLength = 1 },
        new Option() { Type = OptionType.NumberInputField, Name = "Port", Value = "22", MaxLength = 5, MinLength = 1},
        new Option() { Type = OptionType.Selection, Name = " Connect ", Validater = true },
        new Option() { Type = OptionType.Selection, Name = " Cancel " },
    ];
    
    public static Location? Show(bool edit, Location location)
    {
        Canvas.Initialize(new Frame(edit ? "Edit Entry" : "Add Entry", 14, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, Color.GhostWhite, (Color?)null) },
            new DynamicBar() { Center = new Text("Use the arrows keys to navigate", Color.LightGoldenrodYellow, (Color?)null) }
            ));

        if (edit)
        {
            Options.First(x => x.Name == "Name").Value = location.Name;
            Options.First(x => x.Name == "Username").Value = location.Username;
            Options.First(x => x.Name == "IP Address").Value = location.IP;
            Options.First(x => x.Name == "Port").Value = location.Port.ToString();
        }
        else
        {
            Options.ForEach(x => x.Value = "");
            Options.First(x => x.Name == "Port").Value = "22";
        }
        
        WriteOptions();
        
        var validater = Options.FirstOrDefault(x => x.Validater);
        
        var index = 0;
        Select(ref index, 0);
        
        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter || Options[index].Type != OptionType.Selection)
        {
            if ((keyInfo.Key == ConsoleKey.DownArrow &&
                 !(Options.Count > index + 1 && Options[index].Type == OptionType.Selection && Options[index + 1].Type == OptionType.Selection)) ||
                keyInfo.Key == ConsoleKey.Tab || keyInfo.Key == ConsoleKey.Enter)
            {
                var oldIndex = index;
                Select(ref index, index + 1);
                if (keyInfo.Key == ConsoleKey.Tab && oldIndex == index)
                    Select(ref index, 0);
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (Options[index].Type == OptionType.Selection)
                    Select(ref index, Options.FindLastIndex(x => x.Type != OptionType.Selection));
                else
                    Select(ref index, index - 1);
            }
            else if ((keyInfo.Key == ConsoleKey.RightArrow || keyInfo.Key == ConsoleKey.Tab) && Options.Count > index + 1 &&
                     Options[index].Type == OptionType.Selection && Options[index + 1].Type == OptionType.Selection)
            {
                var oldIndex = index;
                Select(ref index, index + 1);
                if (keyInfo.Key == ConsoleKey.Tab && oldIndex == index)
                    Select(ref index, 0);
            }
            else if ((keyInfo.Key == ConsoleKey.LeftArrow) && index > 0 && Options[index].Type == OptionType.Selection && Options[index - 1].Type == OptionType.Selection)
                Select(ref index, index - 1);

            else if (Options[index].Type != OptionType.Selection && keyInfo.Key == ConsoleKey.Backspace && Options[index].Value.Length > 0)
            {
                Options[index].Value = Options[index].Value.Substring(0, Options[index].Value.Length - 1);
                
                if (validater != null && !(Options[index].MinLength == null || Options[index].MinLength <= Options[index].Value.Length))
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, Color.FromArgb(180, 180, 180), Color.FromArgb(60, 60, 60));
                
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX - 1, " ", null, Color.GhostWhite);
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX - 2, Options[index].Value.Length > 0 ? Options[index].Value.Last().ToString() : " ", Color.Black, Options[index].Value.Length > 0 ? Color.GhostWhite : null);
                Options[index].InputX -= 1;
            }
            else if (Options[index].Type != OptionType.Selection && !char.IsControl(keyInfo.KeyChar) && Options[index].Value.Length <
                Math.Min(Options[index].MaxLength ?? (Canvas.Frame.FrameWidth - 4) - Options[index].StartX - 1,
                    (Canvas.Frame.FrameWidth - 4) - Options[index].StartX - 1))
            {
                if (Options[index].Type == OptionType.NumberInputField && !char.IsNumber(keyInfo.KeyChar))
                    continue;
                Options[index].Value += keyInfo.KeyChar;
                if (Options[index].Regex != null && !Regex.IsMatch(Options[index].Value, Options[index].Regex!))
                {
                    Options[index].Value = Options[index].Value.Substring(0, Options[index].Value.Length - 1);
                    continue;
                }
                
                if (validater != null && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length))
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, Color.FromArgb(180, 180, 180), Color.FromArgb(60, 60, 60));
                else if (validater != null)
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, Color.White, Color.FromArgb(85, 85, 85));
                
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX, Options[index].Value.Last().ToString(), Color.Black, Color.GhostWhite);
                Options[index].InputX += 1;
            }
        }

        if (Options[index].Name == " Connect ")
            return new Location()
            {
                Name = Options.First(x => x.Name == "Name").Value,
                Username = Options.First(x => x.Name == "Username").Value,
                IP = Options.First(x => x.Name == "IP Address").Value,
                Port = int.Parse(Options.First(x => x.Name == "Port").Value),
            };
        else
            return null;
    }
    
    private static void Select(ref int index, int newIndex)
    {
        if (Options.Count > newIndex && newIndex >= 0 && Options[newIndex].Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length))
            newIndex++;
        if (newIndex > Options.Count - 1 || newIndex < 0)
            return;
        
        var option = Options[index];
        if (option.Type == OptionType.TextInputField || option.Type == OptionType.NumberInputField)
            Canvas.WriteFrame(option.InputY, option.StartX, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), Color.FromArgb(215, 215, 215), Color.FromArgb(50, 50, 50));
        else if (option.Type == OptionType.Selection)
            Canvas.WriteFrame(option.InputY, option.StartX, option.Name, Color.White, Color.FromArgb(85, 85, 85));
        option = Options[newIndex];
        if (option.Type == OptionType.TextInputField || option.Type == OptionType.NumberInputField)
        {
            Canvas.WriteFrame(option.InputY, option.StartX, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), Color.Black,
                Color.GhostWhite);
            Canvas.WriteFrame(option.InputY, option.InputX, "");
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
        }
        else if (option.Type == OptionType.Selection)
        {
            TerminalCommands.Execute(TerminalCommand.HideCursor);
            Canvas.WriteFrame(option.InputY, option.StartX, option.Name, Color.Black, Color.FromArgb(215, 215, 215));
        }
        index = newIndex;
    }

    private static void WriteOptions()
    {
        for (var i = 0; i < Options.Count; i++)
        {
            var option = Options[i];
            if (option.Type == OptionType.TextInputField || option.Type == OptionType.NumberInputField)
            {
                option.StartX = 1 + $"{option.Name}: ".Length;
                option.StartY = i * 2;
                option.InputX = 1 + $"{option.Name}: {option.Value}".Length;
                option.InputY = i * 2;
                Canvas.WriteFrame(i * 2, 1, $"{option.Name}: ");
                Canvas.WriteFrame(i * 2, 1 + $"{option.Name}: ".Length, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), i == 0 ? Color.Black : Color.FromArgb(215, 215, 215), i == 0 ? Color.GhostWhite : Color.FromArgb(50, 50, 50));
            }
            if (option.Type == OptionType.Selection)
            {
                var option2 = Options[i + 1];
                
                var optionSpaces = new string(' ', 22 - (option.Name.Length + option2.Name.Length));
                var option1Offset = (Canvas.Frame.FrameWidth - 4 - 22) / 2;
                var option2Offset = option1Offset + option.Name.Length + optionSpaces.Length;
                
                option.InputX = option.StartX = option1Offset;
                option2.InputX = option2.StartX = option2Offset;
                option.InputY = option.StartY = i * 2 + 1;
                option2.InputY = option2.StartY = i * 2 + 1;
                
                Canvas.WriteFrame(i * 2 + 1, option1Offset, $"{option.Name}", option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? Color.FromArgb(180, 180, 180) : Color.White, option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? Color.FromArgb(60, 60, 60) : Color.FromArgb(85, 85, 85));
                Canvas.WriteFrame(i * 2 + 1, option2Offset, $"{option2.Name}", Color.White, Color.FromArgb(85, 85, 85));
                i++;
            }
        }
    }

    private enum OptionType
    {
        TextInputField,
        NumberInputField,
        Selection
    }
    private class Option
    {
        public OptionType Type { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = "";
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public int InputX { get; set; }
        public int InputY { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public string? Regex { get; set; }
        public bool Validater { get; set; }
    }
}