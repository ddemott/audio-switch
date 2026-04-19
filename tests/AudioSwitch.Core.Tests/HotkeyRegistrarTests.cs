using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;
using AudioSwitch.Core.Tests.Fakes;

namespace AudioSwitch.Core.Tests;

public sealed class HotkeyRegistrarTests
{
    // === Happy path ===

    [Fact]
    public void RegisterAll_ProfilesWithHotkeys_RegistersEach()
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);
        var profiles = new[]
        {
            new AudioProfile { Name = "Gaming", Hotkey = "Ctrl+Shift+1" },
            new AudioProfile { Name = "Music", Hotkey = "Ctrl+Shift+2" },
        };

        registrar.RegisterAll(profiles, _ => { });

        Assert.Equal(2, hotkeys.Registered.Count);
        Assert.Contains(("Gaming", "Ctrl+Shift+1"), hotkeys.Registered);
        Assert.Contains(("Music", "Ctrl+Shift+2"), hotkeys.Registered);
    }

    [Fact]
    public void RegisterAll_ClearsPreviousRegistrationsFirst()
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);

        registrar.RegisterAll(new[] { new AudioProfile { Name = "A", Hotkey = "Ctrl+1" } }, _ => { });
        registrar.RegisterAll(new[] { new AudioProfile { Name = "B", Hotkey = "Ctrl+2" } }, _ => { });

        Assert.Equal(2, hotkeys.UnregisterAllCount);
        var only = Assert.Single(hotkeys.Registered);
        Assert.Equal(("B", "Ctrl+2"), only);
    }

    [Fact]
    public void Hotkey_Callback_InvokesApplyProfileWithProfileName()
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);
        var applied = new List<string>();
        var profiles = new[] { new AudioProfile { Name = "Gaming", Hotkey = "Ctrl+Shift+1" } };

        registrar.RegisterAll(profiles, name => applied.Add(name));
        hotkeys.Callbacks["Gaming"]();

        Assert.Equal(new[] { "Gaming" }, applied);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: profile exists in the list but has an empty or whitespace-only hotkey string.
    /// Who: User created a profile via "Save current links" without ever assigning a hotkey, or cleared it during edit.
    /// What: Profile is silently skipped — no Register call is made for it.
    /// Why: Registering an empty hotkey would either throw (implementation-dependent) or register a useless binding that shadows nothing; neither helps the user.
    /// Where: HotkeyRegistrar.RegisterAll string.IsNullOrWhiteSpace guard before calling _hotkeyService.Register.
    /// How: Mix profiles with populated and empty hotkeys; assert only the populated ones were registered.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterAll_ProfileWithoutHotkey_IsSkipped(string? hotkey)
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);
        var profiles = new[]
        {
            new AudioProfile { Name = "NoKey", Hotkey = hotkey },
            new AudioProfile { Name = "HasKey", Hotkey = "Ctrl+Shift+9" },
        };

        registrar.RegisterAll(profiles, _ => { });

        var only = Assert.Single(hotkeys.Registered);
        Assert.Equal("HasKey", only.Id);
    }

    /// <summary>
    /// Sad path: a hotkey fires AFTER the owning profile has been deleted (race between WM_HOTKEY and profile removal).
    /// Who: The Win32 message loop delivering a WM_HOTKEY for a hotkey id we registered moments ago; the user has since deleted that profile.
    /// What: The applyProfile callback throws (InvalidOperationException "Profile not found"), the registrar swallows it — no user-visible crash, no propagation to the message loop.
    /// Why: By the time WM_HOTKEY arrives, our own UnregisterAll + re-register from ProfilesChanged has already run, but the in-flight message is still in the queue. Crashing the message loop would freeze the app's UI thread.
    /// Where: HotkeyRegistrar.RegisterAll inner lambda try/catch wrapping applyProfile.
    /// How: Register a hotkey whose applyProfile delegate throws; invoke the callback; assert no exception escapes.
    /// </summary>
    [Fact]
    public void HotkeyCallback_ApplyThrows_SwallowsExceptionToProtectMessageLoop()
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);
        var profiles = new[] { new AudioProfile { Name = "Gone", Hotkey = "Ctrl+1" } };

        registrar.RegisterAll(profiles, _ => throw new InvalidOperationException("Profile 'Gone' not found."));

        var ex = Record.Exception(() => hotkeys.Callbacks["Gone"]());
        Assert.Null(ex);
    }

    [Fact]
    public void RegisterAll_EmptyList_JustClearsExisting()
    {
        var hotkeys = new FakeHotkeyService();
        var registrar = new HotkeyRegistrar(hotkeys);

        registrar.RegisterAll(Array.Empty<AudioProfile>(), _ => { });

        Assert.Equal(1, hotkeys.UnregisterAllCount);
        Assert.Empty(hotkeys.Registered);
    }
}
