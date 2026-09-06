using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Phone.Shell;

public sealed partial class MainPage : Page
{
    private List<WpcutSchema> _shortcuts = new List<WpcutSchema>();

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadShortcutsAsync();
    }

    private async Task LoadShortcutsAsync()
    {
        _shortcuts.Clear();
        try
        {
            StorageFolder docsFolder = KnownFolders.DocumentsLibrary;
            StorageFolder shortcutsFolder = await docsFolder.CreateFolderAsync("Shortcuts", CreationCollisionOption.OpenIfExists);
            var files = await shortcutsFolder.GetFilesAsync();

            foreach (var file in files)
            {
                if (file.FileType.Equals(".wpcut", StringComparison.OrdinalIgnoreCase))
                {
                    string json = await FileIO.ReadTextAsync(file);
                    // Using ValidatorAndRunner logic or direct deserialization
                    var schema = DeserializeJson<WpcutSchema>(json);
                    if (schema != null)
                    {
                        _shortcuts.Add(schema);
                    }
                }
            }
            ShortcutsListBox.ItemsSource = null;
            ShortcutsListBox.ItemsSource = _shortcuts;
        }
        catch (Exception)
        {
            // Handle folder read exceptions if needed
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string uuid)
        {
            // Navigate to detail view passing the UUID or file reference
            // Frame.Navigate(typeof(DetailView), uuid);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string uuid)
        {
            try
            {
                StorageFolder docsFolder = KnownFolders.DocumentsLibrary;
                StorageFolder shortcutsFolder = await docsFolder.GetFolderAsync("Shortcuts");
                StorageFile file = await shortcutsFolder.GetFileAsync($"{uuid}.wpcut");
                await file.DeleteAsync();
                await LoadShortcutsAsync();
            }
            catch (Exception)
            {
                MessageDialog dialog = new MessageDialog("Could not delete shortcut.");
                await dialog.ShowAsync();
            }
        }
    }

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

    private static T DeserializeJson<T>(string json)
    {
        var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
        {
            return (T)serializer.ReadObject(stream);
        }
    }
}