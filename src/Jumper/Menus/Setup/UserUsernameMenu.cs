using System.Drawing;
using System.Text.RegularExpressions;

namespace JumpServer.Menus.Setup;

public class UserUsernameMenu
{
    private static List<Option> Options =
    [
        new Option() { Type = OptionType.TextInputField, Name = "Set jump user username", Regex = @"^[a-z][a-z0-9_-]{0,31}$", MinLength = 1 },
        new Option() { Type = OptionType.Selection, Name = " Continue ", Validater = true },
        new Option() { Type = OptionType.Selection, Name = " Back " },
    ];
    
    public static string? Show(string? value)
    {
        Canvas.Set(new Frame("Setup", 8, 52,
            new DynamicBar() { Center = new Text("Jump Server Setup", AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Use the arrows keys to navigate", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() }
            ));
        
        Options[0].Value = value ?? "";

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
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, AnsiColor.Grey70, AnsiColor.Grey23);
                
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX - 1, " ", null, AnsiColor.Grey93);
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX - 2, Options[index].Value.Length > 0 ? Options[index].Value.Last().ToString() : " ", AnsiColor.Black, Options[index].Value.Length > 0 ? AnsiColor.Grey93 : null);
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
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, AnsiColor.Grey70, AnsiColor.Grey23);
                else if (validater != null)
                    Canvas.WriteFrame(validater.InputY, validater.StartX, validater.Name, AnsiColor.White, AnsiColor.Grey35);
                
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX, Options[index].Value.Last().ToString(), AnsiColor.Black, AnsiColor.Grey93);
                Options[index].InputX += 1;
            }
        }

        if (Options[index].Name == " Continue ")
            return Options[0].Value;
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
            Canvas.WriteFrame(option.InputY, option.StartX, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1),  AnsiColor.Grey85, AnsiColor.Grey19);
        else if (option.Type == OptionType.Selection)
            Canvas.WriteFrame(option.InputY, option.StartX, option.Name, AnsiColor.White, AnsiColor.Grey35);
        option = Options[newIndex];
        if (option.Type == OptionType.TextInputField || option.Type == OptionType.NumberInputField)
        {
            Canvas.WriteFrame(option.InputY, option.StartX, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), AnsiColor.Black,
                AnsiColor.Grey93);
            Canvas.WriteFrame(option.InputY, option.InputX, "");
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
        }
        else if (option.Type == OptionType.Selection)
        {
            TerminalCommands.Execute(TerminalCommand.HideCursor);
            Canvas.WriteFrame(option.InputY, option.StartX, option.Name, AnsiColor.Black,  AnsiColor.Grey85);
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
                Canvas.WriteFrame(i * 2, 1 + $"{option.Name}: ".Length, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), i == 0 ? AnsiColor.Black :  AnsiColor.Grey85, i == 0 ? AnsiColor.Grey93 : AnsiColor.Grey19);
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
                
                Canvas.WriteFrame(i * 2 + 1, option1Offset, $"{option.Name}", option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? AnsiColor.Grey70 : AnsiColor.White, option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? AnsiColor.Grey23 : AnsiColor.Grey35);
                Canvas.WriteFrame(i * 2 + 1, option2Offset, $"{option2.Name}", AnsiColor.White, AnsiColor.Grey35);
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