using AudioSwitch.Core.Services;
using AudioSwitch.Core.Tests.Fakes;

namespace AudioSwitch.Core.Tests;

public sealed class StartupRegistrationServiceTests
{
    // === Happy path ===

    [Fact]
    public void IsRegistered_EmptyStore_ReturnsFalse()
    {
        var service = new StartupRegistrationService(new InMemoryRegistryStore());

        Assert.False(service.IsRegistered());
    }

    [Fact]
    public void Register_WritesQuotedPathWithStartupArg()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store);

        service.Register(@"C:\app\AudioSwitch.exe");

        Assert.True(service.IsRegistered());
        Assert.Equal(
            "\"C:\\app\\AudioSwitch.exe\" --startup",
            store.GetValue(StartupRegistrationService.DefaultValueName));
    }

    [Fact]
    public void Register_PathWithSpaces_IsQuotedAndKeepsStartupArg()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store);

        service.Register(@"C:\Program Files\AudioSwitch\AudioSwitch.exe");

        Assert.Equal(
            "\"C:\\Program Files\\AudioSwitch\\AudioSwitch.exe\" --startup",
            store.GetValue(StartupRegistrationService.DefaultValueName));
    }

    [Fact]
    public void IsStartupLaunch_ArgsContainFlag_ReturnsTrue()
    {
        Assert.True(StartupRegistrationService.IsStartupLaunch(new[] { "--startup" }));
        Assert.True(StartupRegistrationService.IsStartupLaunch(new[] { "other", "--startup", "more" }));
        Assert.True(StartupRegistrationService.IsStartupLaunch(new[] { "--STARTUP" }));
    }

    [Fact]
    public void IsStartupLaunch_NoFlag_ReturnsFalse()
    {
        Assert.False(StartupRegistrationService.IsStartupLaunch(Array.Empty<string>()));
        Assert.False(StartupRegistrationService.IsStartupLaunch(new[] { "--other" }));
    }

    [Fact]
    public void Register_ReplacesExistingValue()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store);

        service.Register(@"C:\old\AudioSwitch.exe");
        service.Register(@"C:\new\AudioSwitch.exe");

        Assert.Equal(
            "\"C:\\new\\AudioSwitch.exe\" --startup",
            store.GetValue(StartupRegistrationService.DefaultValueName));
    }

    [Fact]
    public void Unregister_RemovesValue()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store);
        service.Register(@"C:\app\AudioSwitch.exe");

        service.Unregister();

        Assert.False(service.IsRegistered());
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: Unregister called when nothing was ever registered (user turned on then off in same session, or preference reconciliation at startup).
    /// Who: Tray toggle handler / AppHost reconciliation that calls Unregister unconditionally when the UI state says "off".
    /// What: No exception is thrown; the registry store is left untouched.
    /// Why: Idempotent unregister means callers never need to pre-check IsRegistered, simplifying the UI toggle path and making reconciliation safe to run on every launch.
    /// Where: StartupRegistrationService.Unregister early-return guard when HasValue is false.
    /// How: Construct service over an empty store; call Unregister; assert no throw and store still empty.
    /// </summary>
    [Fact]
    public void Unregister_NothingRegistered_IsNoOp()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store);

        var ex = Record.Exception(() => service.Unregister());

        Assert.Null(ex);
        Assert.Empty(store.Values);
    }

    /// <summary>
    /// Sad path: Register called with an empty or whitespace path (programming error — caller passed Environment.ProcessPath when it was null).
    /// Who: AppHost startup code that resolves the executable path to persist in HKCU Run.
    /// What: ArgumentException is thrown with paramName="executablePath" — a clear, localizable programming error, not a corrupted registry entry.
    /// Why: Writing an empty Run-key value would silently break auto-start on next boot without surfacing a diagnosable error. Failing loudly at the registration boundary forces the caller to resolve the path correctly.
    /// Where: StartupRegistrationService.Register null/whitespace guard.
    /// How: Call Register with "" and with "   "; assert ArgumentException in both cases.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_EmptyPath_Throws(string bad)
    {
        var service = new StartupRegistrationService(new InMemoryRegistryStore());

        Assert.Throws<ArgumentException>(() => service.Register(bad));
    }

    [Fact]
    public void Register_CustomValueName_UsesThatKey()
    {
        var store = new InMemoryRegistryStore();
        var service = new StartupRegistrationService(store, valueName: "AudioSwitch-Beta");

        service.Register(@"C:\app\AudioSwitch.exe");

        Assert.True(store.HasValue("AudioSwitch-Beta"));
        Assert.False(store.HasValue(StartupRegistrationService.DefaultValueName));
    }
}
