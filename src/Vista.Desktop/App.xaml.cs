using System.IO;
using System.Text.Json;
using System.Windows;
using Vista.Core;

namespace Vista.Desktop;
public partial class App : Application
{
    private SupabaseClient? api;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var config = JsonSerializer.Deserialize<VistaConfiguration>(File.ReadAllText(path), SupabaseClient.JsonOptions)
                ?? throw new InvalidDataException("VISTA configuration is missing.");
            MapTiles.UrlTemplate = config.MapTileUrlTemplate;
            api = new SupabaseClient(config, store: new WindowsLoginStore());
            var model = new MainViewModel(api) { SimBriefTyphoonAirframeId = config.SimbriefTyphoonAirframeId, SimBriefAtlasAirframeId = config.SimbriefAtlasAirframeId };
            MainWindow = new MainWindow(model);
            MainWindow.Show();
            await model.RestoreLoginAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "VISTA could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { api?.Dispose(); base.OnExit(e); }
}
