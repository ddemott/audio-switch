using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using AudioSwitch.Core.Models;
using Component = AudioSwitch.Core.Models.Component;

namespace AudioSwitch.App.Views;

public partial class ProfileVolumesWindow : Window
{
    private readonly List<VolumeRow> _rows;
    private readonly AudioProfile _profile;

    public ProfileVolumesWindow(AudioProfile profile, IReadOnlyList<Component> components)
    {
        InitializeComponent();
        _profile = profile;
        HeaderText.Text = $"Edit volumes — {profile.Name}";
        _rows = BuildRows(profile, components);
        RowList.ItemsSource = _rows;
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
                    rows.Add(new VolumeRow(o.Id, o.Name, "\uE7F6", "AccentOutput",
                        defaultVolume: o.Volume,
                        currentVolume: profile.ResolveVolume(o, o.Volume)));
                    break;
                case InputDeviceComponent i:
                    rows.Add(new VolumeRow(i.Id, i.Name, "\uE720", "AccentInput",
                        defaultVolume: i.Volume,
                        currentVolume: profile.ResolveVolume(i, i.Volume)));
                    break;
            }
        }
        return rows;
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

    private sealed class VolumeRow : INotifyPropertyChanged
    {
        private int _volume;

        public VolumeRow(string id, string name, string glyph, string accentResourceKey, int defaultVolume, int currentVolume)
        {
            Id = id;
            Name = name;
            GlyphIcon = glyph;
            DefaultVolume = defaultVolume;
            _volume = currentVolume;
            AccentBrush = (Application.Current.TryFindResource(accentResourceKey) as Brush) ?? Brushes.Gray;
        }

        public string Id { get; }

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
