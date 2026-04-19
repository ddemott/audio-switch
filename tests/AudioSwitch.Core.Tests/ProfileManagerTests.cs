using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;
using AudioSwitch.Core.Tests.Fakes;

namespace AudioSwitch.Core.Tests;

public sealed class ProfileManagerTests
{
    private static (ProfileManager Manager, FakeProfileStore Store, FakeAudioDeviceService Devices, FakeVolumeController Volume, FakeSpatialAudioController Spatial) CreateManager(
        ProfileStoreData? initial = null)
    {
        var store = new FakeProfileStore { Data = initial ?? new ProfileStoreData() };
        var devices = new FakeAudioDeviceService();
        var volume = new FakeVolumeController();
        var spatial = new FakeSpatialAudioController();
        var manager = new ProfileManager(store, devices, volume, spatial);
        return (manager, store, devices, volume, spatial);
    }

    private static AudioProfile NewProfile(string name = "Gaming") => new()
    {
        Name = name,
        InputDevice = new DeviceRef { Id = "mic-id", Name = "Mic" },
        OutputDevice = new DeviceRef { Id = "out-id", Name = "Headset" },
        SpatialMode = SpatialAudioMode.WindowsSonic,
        InputVolume = 70,
        OutputVolume = 55,
    };

    // === Happy path ===

    [Fact]
    public void Ctor_LoadsExistingDataFromStore()
    {
        var initial = new ProfileStoreData
        {
            ActiveProfile = "Gaming",
            Profiles = { NewProfile() },
        };
        var (manager, _, _, _, _) = CreateManager(initial);

        Assert.Single(manager.Profiles);
        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
    }

    [Fact]
    public void AddProfile_PersistsAndRaisesEvent()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.AddProfile(NewProfile());

        Assert.Single(manager.Profiles);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void UpdateProfile_ReplacesAndPersists()
    {
        var (manager, store, _, _, _) = CreateManager();
        manager.AddProfile(NewProfile());

        var updated = NewProfile();
        updated.OutputVolume = 25;
        manager.UpdateProfile(updated);

        Assert.Equal(25, manager.Profiles.Single().OutputVolume);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public void RemoveProfile_ClearsActiveWhenMatched()
    {
        var initial = new ProfileStoreData
        {
            ActiveProfile = "Gaming",
            Profiles = { NewProfile() },
        };
        var (manager, store, _, _, _) = CreateManager(initial);

        manager.RemoveProfile("Gaming");

        Assert.Empty(manager.Profiles);
        Assert.Null(manager.ActiveProfile);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void RemoveProfile_NonActiveMatch_KeepsActive()
    {
        var initial = new ProfileStoreData
        {
            ActiveProfile = "Gaming",
            Profiles = { NewProfile(), NewProfile("Calls") },
        };
        var (manager, _, _, _, _) = CreateManager(initial);

        manager.RemoveProfile("Calls");

        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
    }

    [Fact]
    public void ApplyProfile_AppliesOutputThenInputAndPersistsActive()
    {
        var (manager, store, devices, volume, spatial) = CreateManager();
        manager.AddProfile(NewProfile());
        ProfileApplyResult? appliedArg = null;
        manager.ProfileApplied += (_, r) => appliedArg = r;

        manager.ApplyProfile("Gaming");

        Assert.Equal(new[]
        {
            ("out-id", AudioDeviceDirection.Render),
            ("mic-id", AudioDeviceDirection.Capture),
        }, devices.SetDefaultCalls);

        Assert.Equal(new[]
        {
            ("out-id", AudioDeviceDirection.Render, 55),
            ("mic-id", AudioDeviceDirection.Capture, 70),
        }, volume.SetVolumeCalls);

        Assert.Equal(("out-id", SpatialAudioMode.WindowsSonic), spatial.SetModeCalls.Single());
        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
        Assert.Equal("Gaming", store.Data.ActiveProfile);
        Assert.Equal("Gaming", appliedArg?.Profile.Name);
        Assert.True(appliedArg?.IsFullSuccess);
    }

    [Fact]
    public void ApplyProfile_OutputOnly_DoesNotTouchInput()
    {
        var (manager, _, devices, volume, spatial) = CreateManager();
        var profile = NewProfile();
        profile.InputDevice = null;
        manager.AddProfile(profile);

        manager.ApplyProfile("Gaming");

        Assert.Equal(("out-id", AudioDeviceDirection.Render), Assert.Single(devices.SetDefaultCalls));
        Assert.Equal(("out-id", AudioDeviceDirection.Render, 55), Assert.Single(volume.SetVolumeCalls));
        Assert.Equal(("out-id", SpatialAudioMode.WindowsSonic), Assert.Single(spatial.SetModeCalls));
    }

    [Fact]
    public void ApplyProfile_InputOnly_DoesNotTouchOutputOrSpatial()
    {
        var (manager, _, devices, volume, spatial) = CreateManager();
        var profile = NewProfile();
        profile.OutputDevice = null;
        manager.AddProfile(profile);

        manager.ApplyProfile("Gaming");

        Assert.Equal(("mic-id", AudioDeviceDirection.Capture), Assert.Single(devices.SetDefaultCalls));
        Assert.Equal(("mic-id", AudioDeviceDirection.Capture, 70), Assert.Single(volume.SetVolumeCalls));
        Assert.Empty(spatial.SetModeCalls);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: AddProfile called with a name that already exists.
    /// Who: ProfileEditor view-model "Save" handler when the user types an existing name.
    /// What: throws InvalidOperationException naming the duplicate; no profile added, no persist, no event.
    /// Why: Profile name is the unique key for lookup, apply, and active-profile tracking — a collision corrupts every lookup.
    /// Where: ProfileManager.AddProfile FindProfile guard before _data.Profiles.Add.
    /// How: Add a profile, then call AddProfile again with the same name.
    /// </summary>
    [Fact]
    public void AddProfile_DuplicateName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();
        manager.AddProfile(NewProfile());
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        Assert.Throws<InvalidOperationException>(() => manager.AddProfile(NewProfile()));
        Assert.Single(manager.Profiles);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: UpdateProfile called for a name that was never added.
    /// Who: ProfileEditor view-model "Save" handler if the underlying profile was deleted in another window.
    /// What: throws InvalidOperationException naming the missing profile; no list mutation, no persist.
    /// Why: Update must not silently insert — that would mask the deletion and bypass duplicate-checks in Add.
    /// Where: ProfileManager.UpdateProfile FindIndex &lt; 0 guard before list assignment.
    /// How: Call UpdateProfile against an empty manager.
    /// </summary>
    [Fact]
    public void UpdateProfile_MissingName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.UpdateProfile(NewProfile()));
        Assert.Empty(manager.Profiles);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// Sad path: RemoveProfile called with a name that does not exist.
    /// Who: Stale tray-menu click after the profile was removed in the dashboard window.
    /// What: returns silently (no throw); no persist, no ProfilesChanged event.
    /// Why: Remove is naturally idempotent — re-throwing on missing would force every caller to wrap in try/catch.
    /// Where: ProfileManager.RemoveProfile RemoveAll == 0 early return.
    /// How: Call RemoveProfile against an empty manager and assert no save / no event.
    /// </summary>
    [Fact]
    public void RemoveProfile_UnknownName_NoOps()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.RemoveProfile("Nope");

        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: ApplyProfile called with a name that does not exist.
    /// Who: Hotkey handler firing for a profile whose hotkey wasn't unregistered after delete.
    /// What: throws InvalidOperationException naming the missing profile; no device/volume/spatial calls; ActiveProfile unchanged.
    /// Why: Silently no-oping would leave the user wondering why their hotkey did nothing — louder fail is correct.
    /// Where: ProfileManager.ApplyProfile FindProfile null-coalesce-throw guard.
    /// How: Call ApplyProfile against an empty manager.
    /// </summary>
    [Fact]
    public void ApplyProfile_MissingName_Throws()
    {
        var (manager, _, devices, volume, spatial) = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.ApplyProfile("Nope"));
        Assert.Empty(devices.SetDefaultCalls);
        Assert.Empty(volume.SetVolumeCalls);
        Assert.Empty(spatial.SetModeCalls);
        Assert.Null(manager.ActiveProfile);
    }

    /// <summary>
    /// Sad path: ApplyProfile encounters a partial-failure result from the applier (one device unplugged).
    /// Who: User triggers a hotkey for a profile whose output device just disconnected.
    /// What: Manager still marks the profile active, persists, and raises ProfileApplied with the error-bearing result.
    /// Why: Audio is mission-critical; a partial apply (input switched, output failed) is more useful than total bailout, and the UI needs the result to show a toast.
    /// Where: ProfileManager.ApplyProfile passes the applier's result through unconditionally.
    /// How: Inject a throwing OnSetDefault into the fake; assert ActiveProfile is updated and event arg has errors.
    /// </summary>
    [Fact]
    public void ApplyProfile_PartialFailure_StillActivatesAndRaisesWithErrors()
    {
        var (manager, store, devices, _, _) = CreateManager();
        manager.AddProfile(NewProfile());
        devices.OnSetDefault = (_, dir) =>
        {
            if (dir == AudioDeviceDirection.Render) throw new InvalidOperationException("missing");
        };
        ProfileApplyResult? appliedArg = null;
        manager.ProfileApplied += (_, r) => appliedArg = r;

        manager.ApplyProfile("Gaming");

        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
        Assert.Equal("Gaming", store.Data.ActiveProfile);
        Assert.NotNull(appliedArg);
        Assert.False(appliedArg!.IsFullSuccess);
        Assert.Contains(appliedArg.Errors, e => e.Step == "SetDefaultOutput");
    }

    /// <summary>
    /// Sad path: a profile with both InputDevice and OutputDevice null is applied.
    /// Who: A user who created a profile and never picked devices (UI allows incomplete profiles for V1).
    /// What: ApplyProfile completes without any device/volume/spatial calls but still marks the profile active.
    /// Why: A "no-op" profile is a valid concept (e.g., placeholder) — refusing to apply would surprise the user.
    /// Where: ProfileApplier null-pattern guards on profile.OutputDevice / profile.InputDevice.
    /// How: Add an AudioProfile with default (null) device refs and apply it.
    /// </summary>
    [Fact]
    public void ApplyProfile_BothDevicesNull_NoSideEffectsButActivates()
    {
        var (manager, _, devices, volume, spatial) = CreateManager();
        var profile = new AudioProfile { Name = "Empty" };
        manager.AddProfile(profile);

        manager.ApplyProfile("Empty");

        Assert.Empty(devices.SetDefaultCalls);
        Assert.Empty(volume.SetVolumeCalls);
        Assert.Empty(spatial.SetModeCalls);
        Assert.Equal("Empty", manager.ActiveProfile?.Name);
    }
}
