using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AudioSwitch.App.Composition;
using AudioSwitch.App.Views;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App;

public partial class MainWindow : Window
{
    private AppHost? _host;
    private bool _suppressSelectionSync;
    private bool _redrawQueued;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => QueueRedraw();
        SizeChanged += (_, _) => QueueRedraw();
    }

    public void Bind(AppHost host)
    {
        _host = host;
        RefreshAll();
        UpdateThemeLabel();
        _host.ProfileManager.LibraryChanged += (_, _) => Dispatcher.Invoke(() => { RefreshLibrary(); QueueRedraw(); });
        _host.ProfileManager.ProfilesChanged += (_, _) => Dispatcher.Invoke(RefreshProfiles);
        _host.ProfileManager.ProfileApplied += (_, r) => Dispatcher.Invoke(() => ShowApplyResult(r));
        _host.ThemeService.AppliedThemeChanged += (_, _) => Dispatcher.Invoke(() => { UpdateThemeLabel(); QueueRedraw(); });
    }

    // === Add menus ===

    private void AddOutput_Click(object sender, RoutedEventArgs e) =>
        ShowDevicePickerMenu(
            (Button)sender,
            AudioDeviceDirection.Render,
            device => new OutputDeviceComponent { Name = device.Name, DeviceId = device.Id, Volume = 80 });

    private void AddInput_Click(object sender, RoutedEventArgs e) =>
        ShowDevicePickerMenu(
            (Button)sender,
            AudioDeviceDirection.Capture,
            device => new InputDeviceComponent { Name = device.Name, DeviceId = device.Id, Volume = 80 });

    private void AddEqualizer_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        var menu = new ContextMenu();

        var blank = new MenuItem { Header = "New blank equalizer (flat)" };
        blank.Click += (_, _) => AddEqualizerComponent(
            name: $"Equalizer {_host.ProfileManager.Library.Equalizers.Count + 1}",
            bands: EqualizerComponent.DefaultBands());
        menu.Items.Add(blank);
        menu.Items.Add(new Separator());

        foreach (var group in EqualizerPresets.All.GroupBy(p => p.Category))
        {
            var groupItem = new MenuItem { Header = group.Key };
            foreach (var preset in group)
            {
                var captured = preset;
                var item = new MenuItem { Header = captured.Name };
                item.Click += (_, _) => AddEqualizerComponent(
                    name: UniqueName(captured.Name),
                    bands: EqualizerPresets.BuildBands(captured));
                groupItem.Items.Add(item);
            }
            menu.Items.Add(groupItem);
        }

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void AddEqualizerComponent(string name, List<EqualizerBand> bands)
    {
        if (_host is null) return;
        var eq = new EqualizerComponent { Name = name, Bands = bands };
        _host.ProfileManager.AddComponent(eq);
        StatusText.Text = $"Added '{eq.Name}'.";
    }

    private string UniqueName(string baseName)
    {
        if (_host is null) return baseName;
        var existing = _host.ProfileManager.Library.Equalizers.Select(e => e.Name).ToHashSet();
        if (!existing.Contains(baseName)) return baseName;
        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{baseName} ({n})";
            if (!existing.Contains(candidate)) return candidate;
        }
        return baseName;
    }

    private void ShowDevicePickerMenu(Button anchor, AudioDeviceDirection direction, Func<AudioDevice, Component> factory)
    {
        if (_host is null) return;
        var menu = new ContextMenu();
        var devices = _host.DeviceService.GetDevices(direction);
        foreach (var device in devices)
        {
            var captured = device;
            var item = new MenuItem { Header = captured.Name };
            item.Click += (_, _) =>
            {
                var component = factory(captured);
                if (_host.ProfileManager.AddComponent(component))
                {
                    StatusText.Text = $"Added '{component.Name}'.";
                }
                else
                {
                    StatusText.Text = $"'{component.Name}' already exists in the library.";
                }
            };
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(no devices found)", IsEnabled = false });
        }
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    // === Component row right-click: delete ===

    private void Component_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_host is null || sender is not ListBox list) return;
        if (list.SelectedItem is not Component component) return;

        var menu = new ContextMenu();

        if (component is EqualizerComponent eq)
        {
            var edit = new MenuItem { Header = $"Edit '{eq.Name}'..." };
            edit.Click += (_, _) => OpenEqualizerEditor(eq);
            menu.Items.Add(edit);
            menu.Items.Add(new Separator());
        }

        var delete = new MenuItem { Header = $"Delete '{component.Name}'" };
        delete.Click += (_, _) =>
        {
            if (_host.ProfileManager.RemoveComponent(component.Id))
            {
                StatusText.Text = $"Deleted component '{component.Name}'.";
            }
        };
        menu.Items.Add(delete);
        menu.PlacementTarget = list;
        menu.IsOpen = true;
    }

    private void OpenEqualizerEditor(EqualizerComponent eq)
    {
        if (_host is null) return;
        var editor = new EqualizerEditorWindow(eq) { Owner = this };
        if (editor.ShowDialog() == true && editor.Saved)
        {
            _host.ProfileManager.UpdateComponent(eq);
            StatusText.Text = $"Updated '{eq.Name}'.";
        }
    }

    private void Profile_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_host is null || ProfilesListBox.SelectedItem is not AudioProfile profile) return;

        var menu = new ContextMenu();

        var rename = new MenuItem { Header = $"Rename '{profile.Name}'..." };
        rename.Click += (_, _) => PromptRenameProfile(profile);
        menu.Items.Add(rename);

        menu.Items.Add(new Separator());

        var delete = new MenuItem { Header = $"Delete '{profile.Name}'" };
        delete.Click += (_, _) =>
        {
            _host.ProfileManager.RemoveProfile(profile.Name);
            StatusText.Text = $"Deleted profile '{profile.Name}'.";
        };
        menu.Items.Add(delete);
        menu.PlacementTarget = ProfilesListBox;
        menu.IsOpen = true;
    }

    // === Theme menu ===

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        var menu = new ContextMenu();
        foreach (ThemePreference pref in Enum.GetValues<ThemePreference>())
        {
            var captured = pref;
            var item = new MenuItem
            {
                Header = PrefDisplayName(captured),
                IsChecked = _host.ThemeService.Preference == captured,
                IsCheckable = true,
                StaysOpenOnClick = false,
            };
            item.Click += (_, _) => _host.SetThemePreference(captured);
            menu.Items.Add(item);
        }
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void UpdateThemeLabel()
    {
        if (_host is null) return;
        ThemeButtonLabel.Text = PrefDisplayName(_host.ThemeService.Preference);
    }

    private static string PrefDisplayName(ThemePreference pref) => pref switch
    {
        ThemePreference.Light => "Light",
        ThemePreference.Dark => "Dark",
        _ => "System",
    };

    private void PromptRenameProfile(AudioProfile profile)
    {
        if (_host is null) return;
        var dialog = new NameEditorWindow("Rename profile", "New profile name", profile.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _host.ProfileManager.RenameProfile(profile.Name, dialog.EnteredName);
            StatusText.Text = $"Renamed to '{dialog.EnteredName}'.";
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    // === Profile apply ===

    private void EqualizersList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EqualizersListBox.SelectedItem is not EqualizerComponent eq) return;
        if (e.OriginalSource is DependencyObject src && FindAncestor<ListBoxItem>(src) is null) return;
        OpenEqualizerEditor(eq);
    }

    private void Profile_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src
            && FindAncestor<ListBoxItem>(src) is null)
        {
            return;
        }
        if (_host is null || ProfilesListBox.SelectedItem is not AudioProfile profile) return;
        try
        {
            _host.ProfileManager.ApplyProfile(profile.Name);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Apply failed: {ex.Message}";
        }
    }

    // === Save current selections as a new profile ===

    private void SaveLinks_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        var ids = new List<string>();
        if (OutputsListBox.SelectedItem is OutputDeviceComponent o) ids.Add(o.Id);
        if (InputsListBox.SelectedItem is InputDeviceComponent i) ids.Add(i.Id);
        if (EqualizersListBox.SelectedItem is EqualizerComponent q) ids.Add(q.Id);
        if (ids.Count == 0)
        {
            StatusText.Text = "Select at least one item across the columns first.";
            return;
        }

        var suggested = BuildProfileName();
        var dialog = new NameEditorWindow("Save link config", "Name this link config", suggested) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var name = string.IsNullOrWhiteSpace(dialog.EnteredName) ? suggested : dialog.EnteredName;

        var slot = NextAvailableSlot();
        var profile = new AudioProfile
        {
            Name = name,
            Hotkey = $"Ctrl+Shift+{slot}",
            ComponentIds = ids,
        };
        try
        {
            _host.ProfileManager.AddProfile(profile);
            StatusText.Text = $"Saved link config '{profile.Name}' with hotkey {profile.Hotkey}.";
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private string BuildProfileName()
    {
        var parts = new List<string>();
        if (OutputsListBox.SelectedItem is Component o) parts.Add(o.Name);
        if (InputsListBox.SelectedItem is Component i) parts.Add(i.Name);
        if (EqualizersListBox.SelectedItem is Component q) parts.Add(q.Name);
        var baseName = string.Join(" + ", parts);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Link config";

        if (_host is null) return baseName;
        var existing = _host.ProfileManager.Profiles.Select(p => p.Name).ToHashSet();
        if (!existing.Contains(baseName)) return baseName;
        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{baseName} ({n})";
            if (!existing.Contains(candidate)) return candidate;
        }
        return baseName;
    }

    // === Selection → bezier redraw + profile-to-columns sync ===

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync) return;
        QueueRedraw();
    }

    private void ProfileSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_host is null) return;
        if (ProfilesListBox.SelectedItem is not AudioProfile profile)
        {
            return;
        }
        _suppressSelectionSync = true;
        try
        {
            OutputsListBox.SelectedItem = profile.ComponentIds
                .Select(id => _host.ProfileManager.Library.FindById(id))
                .OfType<OutputDeviceComponent>()
                .FirstOrDefault();
            InputsListBox.SelectedItem = profile.ComponentIds
                .Select(id => _host.ProfileManager.Library.FindById(id))
                .OfType<InputDeviceComponent>()
                .FirstOrDefault();
            EqualizersListBox.SelectedItem = profile.ComponentIds
                .Select(id => _host.ProfileManager.Library.FindById(id))
                .OfType<EqualizerComponent>()
                .FirstOrDefault();
        }
        finally
        {
            _suppressSelectionSync = false;
        }
        QueueRedraw();
    }

    private void QueueRedraw()
    {
        if (_redrawQueued) return;
        _redrawQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            _redrawQueued = false;
            RedrawLinks();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // === Bezier drawing ===

    private void RedrawLinks()
    {
        if (_host is null) return;
        LinkCanvas.Children.Clear();

        var outBrush = FindBrush("AccentOutput");
        var inBrush = FindBrush("AccentInput");
        var thickness = FindDouble("BezierThickness", fallback: 2.0);

        DrawBetween(
            AnchorPoint(OutputsListBox, OutputsListBox.SelectedItem, rightEdge: true),
            AnchorPoint(InputsListBox, InputsListBox.SelectedItem, rightEdge: false),
            outBrush, thickness);
        DrawBetween(
            AnchorPoint(InputsListBox, InputsListBox.SelectedItem, rightEdge: true),
            AnchorPoint(EqualizersListBox, EqualizersListBox.SelectedItem, rightEdge: false),
            inBrush, thickness);
    }

    private Brush FindBrush(string key)
    {
        return TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    private double FindDouble(string key, double fallback)
    {
        return TryFindResource(key) is double d ? d : fallback;
    }

    private void DrawBetween(Point? start, Point? end, Brush stroke, double thickness)
    {
        if (start is null || end is null) return;

        var path = new Path
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            SnapsToDevicePixels = true,
            Data = BuildBezier(start.Value, end.Value),
            IsHitTestVisible = false,
        };
        LinkCanvas.Children.Add(path);
    }

    private static Geometry BuildBezier(Point a, Point b)
    {
        var dx = Math.Max(40, (b.X - a.X) * 0.5);
        var c1 = new Point(a.X + dx, a.Y);
        var c2 = new Point(b.X - dx, b.Y);
        var figure = new PathFigure { StartPoint = a };
        figure.Segments.Add(new BezierSegment(c1, c2, b, isStroked: true));
        var geom = new PathGeometry();
        geom.Figures.Add(figure);
        return geom;
    }

    private Point? AnchorPoint(ListBox list, object? item, bool rightEdge)
    {
        if (item is null) return null;
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container) return null;
        if (container.ActualWidth <= 0 || container.ActualHeight <= 0) return null;
        try
        {
            var x = rightEdge ? container.ActualWidth : 0;
            var y = container.ActualHeight / 2;
            return container.TransformToVisual(LinkCanvas).Transform(new Point(x, y));
        }
        catch
        {
            return null;
        }
    }

    // === Refresh helpers ===

    private void RefreshAll()
    {
        RefreshLibrary();
        RefreshProfiles();
    }

    private void RefreshLibrary()
    {
        if (_host is null) return;
        var lib = _host.ProfileManager.Library;

        _suppressSelectionSync = true;
        try
        {
            RefreshList(OutputsListBox, lib.Outputs, OutputCountText);
            RefreshList(InputsListBox, lib.Inputs, InputCountText);
            RefreshList(EqualizersListBox, lib.Equalizers, EqualizerCountText);
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private void RefreshProfiles()
    {
        if (_host is null) return;
        var selected = ProfilesListBox.SelectedItem as AudioProfile;
        ProfilesListBox.ItemsSource = null;
        ProfilesListBox.ItemsSource = _host.ProfileManager.Profiles;
        ProfileCountText.Text = _host.ProfileManager.Profiles.Count.ToString();
        if (selected is not null)
        {
            ProfilesListBox.SelectedItem = _host.ProfileManager.Profiles.FirstOrDefault(p => p.Name == selected.Name);
        }
    }

    private static void RefreshList<T>(ListBox list, IReadOnlyList<T> source, TextBlock countLabel)
    {
        var selected = list.SelectedItem;
        list.ItemsSource = null;
        list.ItemsSource = source;
        countLabel.Text = source.Count.ToString();
        if (selected is T t && source.Contains(t))
        {
            list.SelectedItem = selected;
        }
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

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
