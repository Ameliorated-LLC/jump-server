using System.Drawing;

namespace JumpServer.Menus;

public static class NoLocationsMenu
{
    public static void Show()
    {
        while (true)
        {
            bool exit = !Canvas.OptionPrompt("Setup", "No entries have been added. Add entry?", "Yes", "Exit");
            if (exit)
            {
                Program.Exit(null, null);
                Environment.Exit(0);
                Thread.Sleep(Timeout.Infinite);
            }

            if (!Program.Authenticated && Configuration.Current.AdminPassword != null)
            {
                Program.Authenticated = AuthenticateMenu.Show();
                if (!Program.Authenticated)
                    continue;
            }
            
            var result = AddEntryMenu.Show();
            if (result.Location == null)
                continue;
            
            LocationSetupMenu.Show(result.Location, result.ImportKey, result.DisablePasswordAuth, result.RandomizeSSHPort, result.RequireTOTP);
            
            break;
        }


        Canvas.Set(new Frame("Jump", 9, 52,
            new DynamicBar() { Center = new Text("Jump Server", (AnsiColor?)null, (AnsiColor?)null).Compile() }
        ));
    }
}