using System.Drawing;

namespace JumpServer.Menus;

public static class DeleteConfirmMenu
{
    public static bool Show(string entryName)
    {
        return Canvas.OptionPrompt("Delete Entry", $"Delete entry {Truncate(entryName, 20)}?", "Yes", "No");
    }
    
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}