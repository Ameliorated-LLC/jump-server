using System.Drawing;
using System.Text.RegularExpressions;

namespace JumpServer.Menus;

public class AddEntryMenu
{
    private static List<Option> Options =
    [
       // new Option() { Type = OptionType.TextInputField, Name = "Name", MinLength = 1 },
        new Option() { Type = OptionType.TextInputField, Name = "Username", Regex = @"^[a-z][a-z0-9_-]{0,31}$", MinLength = 1 },
        new Option() { Type = OptionType.TextInputField, Name = "IP Address", Regex = @"^(?!.*\.\.)[A-Za-z0-9.-]+$", MinLength = 1 },
        new Option() { Type = OptionType.NumberInputField, Name = "SSH Port", Value = "22", MaxLength = 5, MinLength = 1},
        new Option() { Type = OptionType.ToggleButton, Name = "Import SSH Public Key", Toggled = true },
        new Option() { Type = OptionType.ToggleButton, Name = "Disable Password Auth", Toggled = true },
        new Option() { Type = OptionType.ToggleButton, Name = "Randomize Remote SSH Port", Toggled = false},
        //new Option() { Type = OptionType.ToggleButton, Name = "Require TOTP 2FA Auth", Toggled = false},
        
        new Option() { Type = OptionType.Selection, Name = " Connect ", Validater = true },
        new Option() { Type = OptionType.Selection, Name = " Cancel " },
    ];

    public static (Location? Location, bool ImportKey, bool DisablePasswordAuth, bool RandomizeSSHPort, bool RequireTOTP) Show()
    {
        Canvas.Set(new Frame("Add Entry", 16, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Use the space bar to toggle options", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() }
            ));


        Options.ForEach(x => x.Value = "");
        Options.First(x => x.Name == "Port").Value = "22";

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
            else if (keyInfo.Key == ConsoleKey.Spacebar && Options[index].Type == OptionType.ToggleButton)
            {
                Options[index].Toggled = !Options[index].Toggled;
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX, Options[index].Toggled ? "X".ToColored(AnsiColor.Grey89) : " ");
                Canvas.WriteFrame(Options[index].InputY, Options[index].InputX, "");
                
                /*
                if (!Options[index].Toggled)
                    continue;
                if (Options[index].Name == "Randomize Remote SSH Port")
                {
                    var portOption = Options.First(x => x.Name == "SSH Port");
                    if (Options[index].Toggled)
                    {
                        Canvas.WriteFrame(Options[index].InputY, Options[index].StartX, "Random", AnsiColor.Grey70, AnsiColor.Grey19);
                        portOption.Disabled = true;
                    }
                    else
                    {
                        Canvas.WriteFrame(Options[index].InputY, Options[index].StartX, portOption.Value + "     ", AnsiColor.Grey85, AnsiColor.Grey19);
                        portOption.Disabled = false;
                    }
                }
                */
            }
            else if ((Options[index].Type == OptionType.NumberInputField || Options[index].Type == OptionType.TextInputField) && keyInfo.Key == ConsoleKey.Backspace && Options[index].Value.Length > 0)
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
        
        TerminalCommands.Execute(TerminalCommand.HideCursor);

        if (Options[index].Name == " Connect ")
            return (new Location()
                {
                    Name = Options.First(x => x.Name == "IP Address").Value.Trim(),
                    Username = Options.First(x => x.Name == "Username").Value.Trim(),
                    IP = Options.First(x => x.Name == "IP Address").Value.Trim(),
                    Port = int.Parse(Options.First(x => x.Name == "SSH Port").Value.Trim()),
                }, Options.First(x => x.Name == "Import SSH Public Key").Toggled, Options.First(x => x.Name == "Disable Password Auth").Toggled, Options.First(x => x.Name == "Randomize Remote SSH Port").Toggled,
                false); //Options.First(x => x.Name == "Require TOTP 2FA Auth").Toggled);
        else
            return (null, false, false, false, false);
    }
    
    private static void Select(ref int index, int newIndex)
    {
        if (Options.Count > newIndex && newIndex >= 0 && Options[newIndex].Disabled)
            newIndex = index < newIndex ? newIndex + 1 : newIndex - 1;
        if (Options.Count > newIndex && newIndex >= 0 && Options[newIndex].Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length))
            newIndex++;
        if (newIndex > Options.Count - 1 || newIndex < 0)
            return;
        
        var option = Options[index];

        if (option.Type == OptionType.ToggleButton)
        {
            TerminalCommands.Execute(TerminalCommand.ResetCursorShapeAndBlinking);
        }
        
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
            TerminalCommands.Execute(TerminalCommand.CaretCursorShape);
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
        }
        else if (option.Type == OptionType.Selection)
        {
            TerminalCommands.Execute(TerminalCommand.HideCursor);
            Canvas.WriteFrame(option.InputY, option.StartX, option.Name, AnsiColor.Black,  AnsiColor.Grey85);
        }
        else if (option.Type == OptionType.ToggleButton)
        {
            Canvas.WriteFrame(option.InputY, option.StartX, null);
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
            TerminalCommands.Execute(TerminalCommand.UnderscoreCursorShape);
            TerminalCommands.Execute(TerminalCommand.StartCursorBlinking);
        }

        index = newIndex;
    }

    private static void WriteOptions()
    {
        var y = 0;
        for (var i = 0; i < Options.Count; i++)
        {
            var option = Options[i];
            if (option.Type == OptionType.TextInputField || option.Type == OptionType.NumberInputField)
            {
                option.StartX = 1 + $"{option.Name}: ".Length;
                option.StartY = y;
                option.InputX = 1 + $"{option.Name}: {option.Value}".Length;
                option.InputY = y;
                Canvas.WriteFrame(y, 1, $"{option.Name}: ");
                Canvas.WriteFrame(y, 1 + $"{option.Name}: ".Length, option.Value + new string(' ', (Canvas.Frame.FrameWidth - 4) - option.InputX - 1), i == 0 ? AnsiColor.Black :  AnsiColor.Grey85, i == 0 ? AnsiColor.Grey93 : AnsiColor.Grey19);
                y += 2;
            }
            if (option.Type == OptionType.Selection)
            {
                var option2 = Options[i + 1];
                
                var optionSpaces = new string(' ', 22 - (option.Name.Length + option2.Name.Length));
                var option1Offset = (Canvas.Frame.FrameWidth - 4 - 22) / 2;
                var option2Offset = option1Offset + option.Name.Length + optionSpaces.Length;
                
                option.InputX = option.StartX = option1Offset;
                option2.InputX = option2.StartX = option2Offset;
                option.InputY = option.StartY = y + 1;
                option2.InputY = option2.StartY = y + 1;

                Canvas.WriteFrame(y + 1, option1Offset, $"{option.Name}", option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? AnsiColor.Grey70 : AnsiColor.White, option.Validater && !Options.All(x => x.MinLength == null || x.MinLength <= x.Value.Length) ? AnsiColor.Grey23 : AnsiColor.Grey35);
                Canvas.WriteFrame(y + 1, option2Offset, $"{option2.Name}", AnsiColor.White, AnsiColor.Grey35);
                y += 2;
                i++;
            }
            if (option.Type == OptionType.ToggleButton)
            {
                option.StartX = 1 + $"[".Length;
                option.StartY = y;
                option.InputX = 1 + $"[".Length;
                option.InputY = y;
                Canvas.WriteFrame(y, 1, $"[" + (option.Toggled ? "X".ToColored(AnsiColor.Grey89) : " ") + $"]   {option.Name}");
                y += Options.Count != i + 1 && Options[i + 1].Type == OptionType.ToggleButton ? 1 : 2;
            }
        }
    }

    private enum OptionType
    {
        TextInputField,
        NumberInputField,
        ToggleButton,
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
        public bool Toggled { get; set; }
        public bool Disabled { get; set; }
    }
}