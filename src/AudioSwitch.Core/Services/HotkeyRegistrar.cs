using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

public sealed class HotkeyRegistrar
{
    private readonly IHotkeyService _hotkeyService;

    public HotkeyRegistrar(IHotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService;
    }

    public void RegisterAll(IEnumerable<AudioProfile> profiles, Action<string> applyProfile)
    {
        _hotkeyService.UnregisterAll();
        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Hotkey))
            {
                continue;
            }
            var name = profile.Name;
            _hotkeyService.Register(name, profile.Hotkey, () =>
            {
                try { applyProfile(name); }
                catch { /* missing-profile race after delete is benign */ }
            });
        }
    }
}
