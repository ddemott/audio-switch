using System.Windows;
using System.Windows.Interop;
using AudioSwitch.App.Composition;
using AudioSwitch.App.Services;

namespace AudioSwitch.App;

public partial class App : Application
{
    private AppHost? _host;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        var source = (HwndSource)PresentationSource.FromVisual(_mainWindow)!;
        var hotkeyService = new HotkeyService(source);
        _host = new AppHost(hotkeyService);

        _mainWindow.Bind(_host);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
