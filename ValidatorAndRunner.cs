using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;

public static class WpcutValidator
{
    private static string _lastInput = string.Empty;

    public static async Task<bool> ValidateAndExecuteAsync(StorageFile wpcutFile)
    {
        string fileName = Path.GetFileNameWithoutExtension(wpcutFile.Name);
        string rawContent = await FileIO.ReadTextAsync(wpcutFile);

        string jsonContent;
        try
        {
            byte[] decodedBytes = Base32Utility.FromBase32String(rawContent);
            jsonContent = Encoding.UTF8.GetString(decodedBytes, 0, decodedBytes.Length);
        }
        catch
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

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

        if (data == null || !string.Equals(data.ShortcutUuid, fileName, StringComparison.OrdinalIgnoreCase))
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

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

        if (data.ShortcutActions == null)
        {
            await ShowErrorAsync("This shortcut is invalid.");
            return false;
        }

        foreach (var action in data.ShortcutActions)
        {
            if (!string.IsNullOrEmpty(action.WriteFilePath))
            {
                string evaluatedPath = ReplaceVariables(action.WriteFilePath);
                if (!IsPathWritable(evaluatedPath))
                {
                    await ShowErrorAsync("Can't write file.");
                    return false;
                }
            }
        }

        _lastInput = string.Empty;
        foreach (var action in data.ShortcutActions)
        {
            await ExecuteActionAsync(action);
        }

        return true;
    }

    private static async Task ExecuteActionAsync(WpcutAction action)
    {
        string type = action.Type?.ToLowerInvariant();
        string evaluatedDialog = ReplaceVariables(action.Dialog);

        if (type == "dialog" || string.IsNullOrEmpty(type))
        {
            MessageDialog msg = new MessageDialog(evaluatedDialog ?? string.Empty);
            await msg.ShowAsync();
        }
        else if (type == "inputdialog")
        {
            _lastInput = await ShowInputPromptAsync(evaluatedDialog ?? "Enter input:");
        }
        else if (type == "base64dialog")
        {
            string targetText = !string.IsNullOrEmpty(_lastInput) ? _lastInput : (evaluatedDialog ?? string.Empty);
            string base64Result = Convert.ToBase64String(Encoding.UTF8.GetBytes(targetText));
            
            MessageDialog msg = new MessageDialog(base64Result, "Base64 Encoded");
            await msg.ShowAsync();
            _lastInput = base64Result;
        }
        else if (type == "writefile" || !string.IsNullOrEmpty(action.WriteFilePath))
        {
            string path = ReplaceVariables(action.WriteFilePath);
            string content = ReplaceVariables(action.WriteFileContent);

            StorageFile file = await ResolveFileForWritingAsync(path);
            await FileIO.WriteTextAsync(file, content);
        }
        else if (type == "if")
        {
            if (EvaluateCondition(action.Condition))
            {
                if (action.Actions != null)
                {
                    foreach (var subAction in action.Actions)
                    {
                        await ExecuteActionAsync(subAction);
                    }
                }
            }
        }
    }

    private static bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrEmpty(condition)) return false;
        string evaluated = ReplaceVariables(condition);

        if (evaluated.Contains("=="))
        {
            var parts = evaluated.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string left = CleanLiteral(parts[0]);
                string right = CleanLiteral(parts[1]);
                return string.Equals(left, right, StringComparison.Ordinal);
            }
        }
        else if (evaluated.Contains("!="))
        {
            var parts = evaluated.Split(new[] { "!=" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string left = CleanLiteral(parts[0]);
                string right = CleanLiteral(parts[1]);
                return !string.Equals(left, right, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static string CleanLiteral(string val)
    {
        if (val == null) return string.Empty;
        val = val.Trim();
        if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
        {
            if (val.Length >= 2)
            {
                val = val.Substring(1, val.Length - 2);
            }
        }
        return val;
    }

    private static string ReplaceVariables(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Replace("%input%", _lastInput ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ShowInputPromptAsync(string promptMessage)
    {
        TextBox inputTextBox = new TextBox { AcceptsReturn = false, Header = promptMessage };
        ContentDialog dialog = new ContentDialog
        {
            Title = "Input Required",
            Content = inputTextBox,
            PrimaryButtonText = "OK",
            SecondaryButtonText = "Cancel"
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return inputTextBox.Text;
        }
        return string.Empty;
    }

    private static async Task<StorageFile> ResolveFileForWritingAsync(string relativePath)
    {
        string cleanPath = relativePath.Replace('/', '\\').TrimStart('\\');
        if (cleanPath.StartsWith("Documents\\", StringComparison.OrdinalIgnoreCase))
        {
            string subPath = cleanPath.Substring("Documents\\".Length);
            StorageFolder docsFolder = KnownFolders.DocumentsLibrary;
            
            string directory = Path.GetDirectoryName(subPath);
            string fileName = Path.GetFileName(subPath);
            
            StorageFolder targetFolder = docsFolder;
            if (!string.IsNullOrEmpty(directory))
            {
                foreach (var dir in directory.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    targetFolder = await targetFolder.CreateFolderAsync(dir, CreationCollisionOption.OpenIfExists);
                }
            }
            
            return await targetFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }
        throw new UnauthorizedAccessException("Can't write file.");
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
            .ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(json)));
}