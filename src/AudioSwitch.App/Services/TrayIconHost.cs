using System.Windows;
using System.Windows.Controls;
using AudioSwitch.App.Composition;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace AudioSwitch.App.Services;

public sealed class TrayIconHost : IDisposable
{
    private const int MaxTooltipLength = 63;  // Windows NOTIFYICONDATA limit

    private readonly AppHost _host;
    private readonly Window _mainWindow;
    private readonly Application _application;
    private readonly TaskbarIcon _tray;
    private readonly MenuItem _startWithWindowsItem;
    private bool _exiting;

    public bool IsExiting => _exiting;

    public TrayIconHost(AppHost host, Window mainWindow, Application application)
    {
        _host = host;
        _mainWindow = mainWindow;
        _application = application;

        _tray = new TaskbarIcon
        {
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/audio-switch.ico", UriKind.Absolute)),
            ToolTipText = BuildTooltip(),
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        _tray.TrayLeftMouseUp += (_, _) => ShowMainWindow();

        _startWithWindowsItem = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = _host.IsStartWithWindowsEnabled,
            StaysOpenOnClick = false,
        };
        _startWithWindowsItem.Click += OnToggleStartWithWindows;

        _tray.ContextMenu = BuildContextMenu();

        _host.ProfileManager.ProfileApplied += OnProfileApplied;
        _host.ProfileManager.ProfilesChanged += OnProfilesChanged;
    }

    public void RequestClose()
    {
        switch (_host.ProfileManager.CloseBehavior)
        {
            case WindowCloseBehavior.Exit:
                ExitApplication();
                break;
            case WindowCloseBehavior.MinimizeToTray:
                HideMainWindow();
                break;
            default:
                PromptAndAct();
                break;
        }
    }

    public void Dispose()
    {
        _host.ProfileManager.ProfileApplied -= OnProfileApplied;
        _host.ProfileManager.ProfilesChanged -= OnProfilesChanged;
        _tray.Dispose();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var show = new MenuItem { Header = "Show AudioSwitch" };
        show.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(show);

        menu.Items.Add(new Separator());

        // Profiles submenu — quick-apply from tray
        var profilesItem = new MenuItem { Header = "Apply profile" };
        RebuildProfilesSubmenu(profilesItem);
        profilesItem.SubmenuOpened += (_, _) => RebuildProfilesSubmenu(profilesItem);
        menu.Items.Add(profilesItem);

        menu.Items.Add(new Separator());
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);

        return menu;
    }

    private void RebuildProfilesSubmenu(MenuItem parent)
    {
        parent.Items.Clear();
        var profiles = _host.ProfileManager.Profiles;
        if (profiles.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "(no profiles yet)", IsEnabled = false });
            return;
        }
        foreach (var profile in profiles)
        {
            var captured = profile;
            var item = new MenuItem
            {
                Header = captured.Name,
                IsChecked = _host.ProfileManager.ActiveProfile?.Name == captured.Name,
                IsCheckable = false,
            };
            item.Click += (_, _) =>
            {
                try { _host.ProfileManager.ApplyProfile(captured.Name); }
                catch { /* missing-profile race is benign */ }
            };
            parent.Items.Add(item);
        }
    }

    private void OnToggleStartWithWindows(object sender, RoutedEventArgs e)
    {
        _host.SetStartWithWindows(_startWithWindowsItem.IsChecked);
        // Reconcile UI against actual registry state in case registration failed.
        _startWithWindowsItem.IsChecked = _host.IsStartWithWindowsEnabled;
    }

    private void OnProfileApplied(object? sender, ProfileApplyResult e) =>
        _application.Dispatcher.Invoke(() => _tray.ToolTipText = BuildTooltip());

    private void OnProfilesChanged(object? sender, EventArgs e) =>
        _application.Dispatcher.Invoke(() => _tray.ToolTipText = BuildTooltip());

    private string BuildTooltip()
    {
        var profile = _host.ProfileManager.ActiveProfile?.Name;
        var text = string.IsNullOrWhiteSpace(profile)
            ? "AudioSwitch — no profile active"
            : $"AudioSwitch — {profile}";
        return text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void HideMainWindow() => _mainWindow.Hide();

    private void ExitApplication()
    {
        _exiting = true;
        _application.Shutdown();
    }

    private void PromptAndAct()
    {
        var dialog = new Views.CloseChoiceWindow { Owner = _mainWindow };
        dialog.ShowDialog();

        var newPref = CloseBehaviorResolver.UpdatePreference(
            _host.ProfileManager.CloseBehavior, dialog.Result, dialog.RememberChoice);
        if (newPref != _host.ProfileManager.CloseBehavior)
        {
            _host.SetCloseBehavior(newPref);
        }

        switch (dialog.Result)
        {
            case CloseAction.MinimizeToTray: HideMainWindow(); break;
            case CloseAction.Exit: ExitApplication(); break;
            default: /* Cancel → leave window open */ break;
        }
    }
}
