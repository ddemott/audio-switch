using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Views;

public partial class EqualizerEditorWindow : Window
{
    private readonly EqualizerComponent _component;
    private readonly List<BandRow> _rows;
    private readonly IApoConfigWriter? _apoConfigWriter;
    private readonly IAudioDeviceService? _deviceService;
    private readonly string? _previewDeviceName;
    private readonly string? _previewSnapshot;
    private bool _committed;

    public EqualizerEditorWindow(EqualizerComponent component)
        : this(component, null, null) { }

    public EqualizerEditorWindow(
        EqualizerComponent component,
        IApoConfigWriter? apoConfigWriter,
        IAudioDeviceService? deviceService)
    {
        InitializeComponent();
        _component = component;
        _apoConfigWriter = apoConfigWriter;
        _deviceService = deviceService;
        NameBox.Text = component.Name;
        _rows = component.Bands.Select(b => new BandRow(b)).ToList();

        if (_apoConfigWriter is { IsAvailable: true } && _deviceService is not null)
        {
            try
            {
                var defaultOut = _deviceService.GetDefault(AudioDeviceDirection.Render);
                _previewDeviceName = defaultOut?.Name;
                _previewSnapshot = _apoConfigWriter.Snapshot();
            }
            catch
            {
                _previewDeviceName = null;
            }
        }

        UpdatePreviewBanner();

        if (_previewDeviceName is not null)
        {
            foreach (var row in _rows)
            {
                row.PropertyChanged += OnBandChanged;
            }
        }

        BandList.ItemsSource = _rows;
        Closed += OnClosed;
    }

    public bool Saved { get; private set; }

    private void UpdatePreviewBanner()
    {
        if (_apoConfigWriter is null)
        {
            PreviewBanner.Visibility = Visibility.Collapsed;
            return;
        }
        if (!_apoConfigWriter.IsAvailable)
        {
            PreviewBanner.Text = "Live preview disabled — Equalizer APO is not installed. Bands still save normally.";
            PreviewBanner.Visibility = Visibility.Visible;
            return;
        }
        if (_previewDeviceName is null)
        {
            PreviewBanner.Text = "Live preview disabled — no default output device available.";
            PreviewBanner.Visibility = Visibility.Visible;
            return;
        }
        PreviewBanner.Text = $"Live preview on “{_previewDeviceName}.” Cancel restores the previous EQ.";
        PreviewBanner.Visibility = Visibility.Visible;
    }

    private void OnBandChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BandRow.Gain)) return;
        WritePreview();
    }

    private void WritePreview()
    {
        if (_apoConfigWriter is null || !_apoConfigWriter.IsAvailable || _previewDeviceName is null) return;
        var bands = _rows
            .Select(r => new EqualizerBand { Frequency = r.Frequency, Gain = r.Gain })
            .ToList();
        try
        {
            _apoConfigWriter.Write(new[] { new ApoDeviceEntry(_previewDeviceName, bands) });
        }
        catch
        {
            // Live preview is best-effort — don't break slider movement on a transient write failure.
        }
    }

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
        _committed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_committed || _apoConfigWriter is null || !_apoConfigWriter.IsAvailable) return;
        try
        {
            _apoConfigWriter.Restore(_previewSnapshot);
        }
        catch
        {
            // Restore failure on close is non-fatal — APO will fall back to whatever's in the file.
        }
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
