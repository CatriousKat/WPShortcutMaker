using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;

public static class WpcutValidator
{
    public static async Task<bool> ValidateAndExecuteAsync(StorageFile wpcutFile)
    {
        string fileName = Path.GetFileNameWithoutExtension(wpcutFile.Name);
        string jsonContent = await FileIO.ReadTextAsync(wpcutFile);

        WpcutSchema data;
        try
        {
            data = DeserializeJson<WpcutSchema>(jsonContent);
        }
        catch
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

        // 1. Validate UUID Match
        if (data == null || !string.Equals(data.ShortcutUuid, fileName, StringComparison.OrdinalIgnoreCase))
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

        // 2. Validate Icon Loading
        try
        {
            if (!string.IsNullOrEmpty(data.ShortcutIcon))
            {
                StorageFile iconFile;
                if (Path.IsPathRooted(data.ShortcutIcon) || data.ShortcutIcon.StartsWith("\\"))
                {
                    iconFile = await StorageFile.GetFileFromPathAsync(data.ShortcutIcon);
                }
                else
                {
                    var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    iconFile = await installFolder.GetFileAsync(data.ShortcutIcon);
                }

                using (var stream = await iconFile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapImage bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                }
            }
        }
        catch
        {
            await ShowErrorAsync("Unsupported icon format.");
            return false;
        }

        // 3. Validate Actions & Write Paths
        if (data.ShortcutActions == null)
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

        foreach (var action in data.ShortcutActions)
        {
            if (!string.IsNullOrEmpty(action.WriteFilePath))
            {
                if (!IsPathWritable(action.WriteFilePath))
                {
                    await ShowErrorAsync("Can't write file.");
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPathWritable(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string normalized = path.Replace('/', '\\');
        
        return normalized.StartsWith("Documents\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("SD Card\\", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ShowErrorAsync(string message)
    {
        MessageDialog dialog = new MessageDialog(message);
        await dialog.ShowAsync();
    }

    private static T DeserializeJson<T>(string json) => 
        (T)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T))
            .ReadObject(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
}