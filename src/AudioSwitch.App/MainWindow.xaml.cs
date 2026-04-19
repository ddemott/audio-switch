using System.Windows;
using AudioSwitch.App.Composition;
using AudioSwitch.Core.Models;

namespace AudioSwitch.App;

public partial class MainWindow : Window
{
    private AppHost? _host;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Bind(AppHost host)
    {
        _host = host;
        RefreshDevices();
        RefreshProfiles();
        _host.ProfileManager.ProfilesChanged += (_, _) => Dispatcher.Invoke(RefreshProfiles);
        _host.ProfileManager.ProfileApplied += (_, result) => Dispatcher.Invoke(() => ShowApplyResult(result));
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    private void ClearInputButton_Click(object sender, RoutedEventArgs e)
    {
        InputDevicesList.SelectedItem = null;
        StatusText.Text = "Input cleared — next saved profile will have no microphone.";
    }

    private void SaveAsProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null || OutputDevicesList.SelectedItem is not AudioDevice output)
        {
            StatusText.Text = "Select an output device first.";
            return;
        }

        var input = InputDevicesList.SelectedItem as AudioDevice;
        var slot = NextAvailableSlot();
        var name = input is null ? output.Name : $"{output.Name} + {input.Name}";
        var profile = new AudioProfile
        {
            Name = name,
            Hotkey = $"Ctrl+Shift+{slot}",
            OutputDevice = new DeviceRef { Id = output.Id, Name = output.Name },
            InputDevice = input is null ? null : new DeviceRef { Id = input.Id, Name = input.Name },
            OutputVolume = 80,
            InputVolume = 80,
        };

        try
        {
            _host.ProfileManager.AddProfile(profile);
            var pairing = input is null ? "output only" : "output + input";
            StatusText.Text = $"Saved profile '{profile.Name}' ({pairing}) with hotkey {profile.Hotkey}.";
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplySelected();

    private void ProfilesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.DependencyObject src
            && FindAncestor<System.Windows.Controls.ListBoxItem>(src) is null)
        {
            return;
        }
        ApplySelected();
    }

    private void ApplySelected()
    {
        if (_host is null || ProfilesList.SelectedItem is not AudioProfile profile)
        {
            StatusText.Text = "Select a profile first.";
            return;
        }
        try
        {
            _host.ProfileManager.ApplyProfile(profile.Name);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Apply failed: {ex.Message}";
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null || ProfilesList.SelectedItem is not AudioProfile profile)
        {
            StatusText.Text = "Select a profile first.";
            return;
        }
        _host.ProfileManager.RemoveProfile(profile.Name);
        StatusText.Text = $"Removed profile '{profile.Name}'.";
    }

    private void RefreshDevices()
    {
        if (_host is null) return;
        var outputs = _host.DeviceService.GetDevices(AudioDeviceDirection.Render);
        var inputs = _host.DeviceService.GetDevices(AudioDeviceDirection.Capture);
        OutputDevicesList.ItemsSource = outputs;
        InputDevicesList.ItemsSource = inputs;
        StatusText.Text = $"{outputs.Count} output / {inputs.Count} input devices.";
    }

    private void RefreshProfiles()
    {
        if (_host is null) return;
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = _host.ProfileManager.Profiles;
    }

    private void ShowApplyResult(ProfileApplyResult result)
    {
        if (result.IsFullSuccess)
        {
            StatusText.Text = $"Applied '{result.Profile.Name}'.";
            return;
        }
        var first = result.Errors[0];
        StatusText.Text = $"Applied '{result.Profile.Name}' with {result.Errors.Count} error(s): [{first.Step}] {first.Message}";
    }

    private int NextAvailableSlot()
    {
        if (_host is null) return 1;
        var used = new HashSet<int>();
        foreach (var p in _host.ProfileManager.Profiles)
        {
            if (p.Hotkey is { } hk && hk.StartsWith("Ctrl+Shift+", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(hk.AsSpan("Ctrl+Shift+".Length), out var n))
            {
                used.Add(n);
            }
        }
        for (var i = 1; i <= 9; i++)
        {
            if (!used.Contains(i)) return i;
        }
        return 1;
    }

    private static T? FindAncestor<T>(System.Windows.DependencyObject start) where T : System.Windows.DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
