using System.Windows;
using AudioSwitch.Core.Models;
using Microsoft.Win32;

namespace AudioSwitch.App.Services;

public sealed class ThemeService : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly Application _application;
    private ThemePreference _preference = ThemePreference.System;

    public ThemeService(Application application)
    {
        _application = application;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? AppliedThemeChanged;

    public ThemePreference Preference => _preference;

    public bool IsDark { get; private set; }

    public void Apply(ThemePreference preference)
    {
        _preference = preference;
        ApplyEffective();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        if (_preference != ThemePreference.System) return;
        _application.Dispatcher.Invoke(ApplyEffective);
    }

    private void ApplyEffective()
    {
        var dark = _preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => IsWindowsDark(),
        };

        var resourceUri = dark
            ? new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Themes/Light.xaml", UriKind.Absolute);

        var newDict = new ResourceDictionary { Source = resourceUri };
        var merged = _application.Resources.MergedDictionaries;

        // Remove any previously-installed theme dictionary (matching pack uri).
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? string.Empty;
            if (src.Contains("/Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)
                || src.Contains("/Themes/Light.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }
        merged.Add(newDict);

        IsDark = dark;
        AppliedThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsWindowsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue(AppsUseLightThemeValue) is int appsUseLight)
            {
                return appsUseLight == 0;
            }
        }
        catch
        {
            // Registry unavailable → default to dark (matches current UI heritage).
        }
        return true;
    }
}
