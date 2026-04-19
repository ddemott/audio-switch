using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AudioSwitch.App.Composition;
using AudioSwitch.App.Services;

namespace AudioSwitch.App;

public partial class App : Application
{
    public const string StartupArg = "--startup";

    private AppHost? _host;
    private MainWindow? _mainWindow;
    private TrayIconHost? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var launchedByStartup = e.Args.Any(a =>
            string.Equals(a, StartupArg, StringComparison.OrdinalIgnoreCase));

        var themeService = new ThemeService(this);

        _mainWindow = new MainWindow();
        _mainWindow.Show();  // need to Show once so HwndSource exists for hotkeys

        var source = (HwndSource)PresentationSource.FromVisual(_mainWindow)!;
        var hotkeyService = new HotkeyService(source);
        _host = new AppHost(hotkeyService, themeService, this);

        _mainWindow.Bind(_host);

        _tray = new TrayIconHost(_host, _mainWindow, this);
        _mainWindow.AttachTrayHost(_tray);

        if (launchedByStartup)
        {
            _mainWindow.Hide();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
