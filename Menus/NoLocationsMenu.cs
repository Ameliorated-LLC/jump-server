﻿using System.Drawing;

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
            
            var location = EntryMenu.Show(false, null!);
            if (location == null)
                continue;
            
            LocationSetupMenu.Show(location);
            
            break;
        }


        Canvas.Initialize(new Frame("Jump", 9, 52,
            new DynamicBar() { Center = new Text("Jump Server", (Color?)null, (Color?)null) }
        ));
    }
}