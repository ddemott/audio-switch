using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AudioSwitch.Core.Models;

namespace AudioSwitch.App.Views;

public partial class EqualizerEditorWindow : Window
{
    private readonly EqualizerComponent _component;
    private readonly List<BandRow> _rows;

    public EqualizerEditorWindow(EqualizerComponent component)
    {
        InitializeComponent();
        _component = component;
        NameBox.Text = component.Name;
        _rows = component.Bands.Select(b => new BandRow(b)).ToList();
        BandList.ItemsSource = _rows;
    }

    public bool Saved { get; private set; }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _rows)
        {
            r.Gain = 0;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _component.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? _component.Name : NameBox.Text.Trim();
        for (var i = 0; i < _rows.Count && i < _component.Bands.Count; i++)
        {
            _component.Bands[i].Gain = _rows[i].Gain;
        }
        Saved = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class BandRow : INotifyPropertyChanged
    {
        private readonly EqualizerBand _band;
        private double _gain;

        public BandRow(EqualizerBand band)
        {
            _band = band;
            _gain = band.Gain;
        }

        public int Frequency => _band.Frequency;

        public string FrequencyLabel => _band.Frequency >= 1000
            ? $"{_band.Frequency / 1000}k"
            : $"{_band.Frequency}";

        public double Gain
        {
            get => _gain;
            set
            {
                if (Math.Abs(_gain - value) < 0.0001) return;
                _gain = value;
                Raise();
                Raise(nameof(GainLabel));
            }
        }

        public string GainLabel => _gain switch
        {
            0 => "0",
            > 0 => $"+{_gain:F1}",
            _ => $"{_gain:F1}",
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
