using System;
using System.Linq;
using Microsoft.Phone.Shell;

public static class ShortcutHelper
{
    public static void CreateHomeScreenShortcut(string shortcutName, string iconPath, string wpcutFilePath)
    {
        StandardTileData tileData = new StandardTileData
        {
            Title = shortcutName,
            BackgroundImage = new Uri(iconPath, UriKind.Absolute)
        };

        Uri targetUri = new Uri($"/MainPage.xaml?shortcut={Uri.EscapeDataString(wpcutFilePath)}", UriKind.Relative);
        
        if (!ShellTile.ActiveTiles.Any(x => x.NavigationUri == targetUri))
        {
            ShellTile.Create(targetUri, tileData);
        }
    }
}