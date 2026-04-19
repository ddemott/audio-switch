using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using Component = AudioSwitch.Core.Models.Component;

namespace AudioSwitch.App.Views;

public partial class ProfileVolumesWindow : Window
{
    private readonly List<VolumeRow> _rows;
    private readonly AudioProfile _profile;
    private readonly IVolumeController _volumeController;
    private readonly Dictionary<string, int> _originalDeviceVolumes = new();
    private bool _committed;

    public ProfileVolumesWindow(
        AudioProfile profile,
        IReadOnlyList<Component> components,
        IVolumeController volumeController)
    {
        InitializeComponent();
        _profile = profile;
        _volumeController = volumeController;
        HeaderText.Text = $"Edit volumes — {profile.Name}";
        _rows = BuildRows(profile, components);
        SnapshotCurrentDeviceVolumes();
        foreach (var row in _rows)
        {
            row.PropertyChanged += OnRowChanged;
        }
        RowList.ItemsSource = _rows;
        Closed += OnClosed;
    }

    public bool Saved { get; private set; }

    private static List<VolumeRow> BuildRows(AudioProfile profile, IReadOnlyList<Component> components)
    {
        var rows = new List<VolumeRow>();
        foreach (var component in components)
        {
            switch (component)
            {
                case OutputDeviceComponent o:
                    rows.Add(new VolumeRow(o.Id, o.DeviceId, AudioDeviceDirection.Render,
                        o.Name, "\uE7F6", "AccentOutput",
                        defaultVolume: o.Volume,
                        currentVolume: profile.ResolveVolume(o, o.Volume)));
                    break;
                case InputDeviceComponent i:
                    rows.Add(new VolumeRow(i.Id, i.DeviceId, AudioDeviceDirection.Capture,
                        i.Name, "\uE720", "AccentInput",
                        defaultVolume: i.Volume,
                        currentVolume: profile.ResolveVolume(i, i.Volume)));
                    break;
            }
        }
        return rows;
    }

    private void SnapshotCurrentDeviceVolumes()
    {
        foreach (var row in _rows)
        {
            try
            {
                _originalDeviceVolumes[row.Id] = _volumeController.GetVolume(row.DeviceId, row.Direction);
            }
            catch
            {
                // Device may be unplugged or unreachable; leave the snapshot absent so we don't try to "restore" to a guess.
            }
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VolumeRow.Volume) || sender is not VolumeRow row) return;
        try
        {
            _volumeController.SetVolume(row.DeviceId, row.Direction, row.Volume);
        }
        catch
        {
            // Live preview is best-effort — silent on failure so the slider stays usable.
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _profile.ComponentVolumes.Clear();
        foreach (var row in _rows)
        {
            if (row.Volume != row.DefaultVolume)
            {
                _profile.ComponentVolumes[row.Id] = row.Volume;
            }
        }
        Saved = true;
        _committed = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.Volume = row.DefaultVolume;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_committed) return;
        // Cancelled (or X-clicked): restore each device to whatever volume Windows had when the dialog opened.
        foreach (var row in _rows)
        {
            if (!_originalDeviceVolumes.TryGetValue(row.Id, out var original)) continue;
            try { _volumeController.SetVolume(row.DeviceId, row.Direction, original); }
            catch { /* nothing useful to do here */ }
        }
    }

    private sealed class VolumeRow : INotifyPropertyChanged
    {
        private int _volume;

        public VolumeRow(string id, string deviceId, AudioDeviceDirection direction,
            string name, string glyph, string accentResourceKey, int defaultVolume, int currentVolume)
        {
            Id = id;
            DeviceId = deviceId;
            Direction = direction;
            Name = name;
            GlyphIcon = glyph;
            DefaultVolume = defaultVolume;
            _volume = currentVolume;
            AccentBrush = (Application.Current.TryFindResource(accentResourceKey) as Brush) ?? Brushes.Gray;
        }

        public string Id { get; }

        public string DeviceId { get; }

        public AudioDeviceDirection Direction { get; }

        public string Name { get; }

        public string GlyphIcon { get; }

        public Brush AccentBrush { get; }

        public int DefaultVolume { get; }

        public int Volume
        {
            get => _volume;
            set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (_volume == clamped) return;
                _volume = clamped;
                Raise();
                Raise(nameof(VolumeLabel));
            }
        }

        public string VolumeLabel => $"{_volume}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
