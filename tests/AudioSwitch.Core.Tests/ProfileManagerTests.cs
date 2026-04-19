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

    private static OutputDeviceComponent NewOutput(string id = "out-id") =>
        new() { Name = "Headset", DeviceId = id, Volume = 55 };

    private static InputDeviceComponent NewInput(string id = "mic-id") =>
        new() { Name = "Mic", DeviceId = id, Volume = 70 };

    private static (ProfileStoreData Data, OutputDeviceComponent Output, InputDeviceComponent Input) BuildSeededData(string profileName = "Gaming")
    {
        var output = NewOutput();
        var input = NewInput();
        var data = new ProfileStoreData
        {
            Library = new ComponentLibrary(),
            Profiles =
            {
                new AudioProfile
                {
                    Name = profileName,
                    Hotkey = "Ctrl+Shift+1",
                    ComponentIds = { output.Id, input.Id },
                },
            },
        };
        data.Library.Add(output);
        data.Library.Add(input);
        return (data, output, input);
    }

    // === Happy path ===

    [Fact]
    public void Ctor_LoadsExistingDataFromStore()
    {
        var (data, _, _) = BuildSeededData();
        data.ActiveProfile = "Gaming";
        var (manager, _, _, _, _) = CreateManager(data);

        Assert.Single(manager.Profiles);
        Assert.Equal(2, manager.Library.All.Count());
        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
    }

    [Fact]
    public void AddComponent_PersistsAndRaisesLibraryChanged()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.LibraryChanged += (_, _) => raised++;

        Assert.True(manager.AddComponent(NewOutput()));

        Assert.Single(manager.Library.Outputs);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void UpdateComponent_PersistsEditsAndRaisesLibraryChanged()
    {
        var (manager, store, _, _, _) = CreateManager();
        var eq = new EqualizerComponent { Name = "Flat" };
        manager.AddComponent(eq);
        eq.Name = "Heavy Bass";
        eq.Bands[0].Gain = 6.0;
        var raised = 0;
        manager.LibraryChanged += (_, _) => raised++;

        Assert.True(manager.UpdateComponent(eq));
        var stored = Assert.Single(manager.Library.Equalizers);
        Assert.Equal("Heavy Bass", stored.Name);
        Assert.Equal(6.0, stored.Bands[0].Gain);
        Assert.Equal(2, store.SaveCount); // one from Add, one from Update
        Assert.Equal(1, raised);
    }

    /// <summary>
    /// Sad path: UpdateComponent called for an id that isn't in the library.
    /// Who: UI holding a stale component reference after another pane deleted it.
    /// What: Returns false; no save, no LibraryChanged.
    /// Why: Quiet no-op matches RemoveComponent semantics; avoids forcing the UI into try/catch.
    /// Where: ProfileManager.UpdateComponent FindById null guard.
    /// </summary>
    [Fact]
    public void UpdateComponent_UnknownId_ReturnsFalseNoSideEffects()
    {
        var (manager, store, _, _, _) = CreateManager();
        var ghost = new EqualizerComponent { Name = "Ghost" };
        var raised = 0;
        manager.LibraryChanged += (_, _) => raised++;

        Assert.False(manager.UpdateComponent(ghost));
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void RemoveComponent_AlsoStripsIdFromAllProfiles()
    {
        var (data, output, _) = BuildSeededData();
        var (manager, _, _, _, _) = CreateManager(data);
        var libRaised = 0;
        var profilesRaised = 0;
        manager.LibraryChanged += (_, _) => libRaised++;
        manager.ProfilesChanged += (_, _) => profilesRaised++;

        Assert.True(manager.RemoveComponent(output.Id));

        Assert.Empty(manager.Library.Outputs);
        Assert.DoesNotContain(output.Id, manager.Profiles[0].ComponentIds);
        Assert.Equal(1, libRaised);
        Assert.Equal(1, profilesRaised);
    }

    [Fact]
    public void AddProfile_PersistsAndRaisesEvent()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.AddProfile(new AudioProfile { Name = "Gaming" });

        Assert.Single(manager.Profiles);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void UpdateProfile_ReplacesAndPersists()
    {
        var (data, _, _) = BuildSeededData();
        var (manager, store, _, _, _) = CreateManager(data);

        var updated = new AudioProfile { Name = "Gaming", Hotkey = "Ctrl+Shift+9" };
        manager.UpdateProfile(updated);

        Assert.Equal("Ctrl+Shift+9", manager.Profiles.Single().Hotkey);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void RemoveProfile_ClearsActiveWhenMatched()
    {
        var (data, _, _) = BuildSeededData();
        data.ActiveProfile = "Gaming";
        var (manager, _, _, _, _) = CreateManager(data);

        manager.RemoveProfile("Gaming");

        Assert.Empty(manager.Profiles);
        Assert.Null(manager.ActiveProfile);
    }

    [Fact]
    public void ApplyProfile_DispatchesToApplierAndPersistsActive()
    {
        var (data, output, input) = BuildSeededData();
        var (manager, store, devices, volume, _) = CreateManager(data);
        ProfileApplyResult? appliedArg = null;
        manager.ProfileApplied += (_, r) => appliedArg = r;

        manager.ApplyProfile("Gaming");

        Assert.Equal((output.DeviceId, AudioDeviceDirection.Render), devices.SetDefaultCalls[0]);
        Assert.Equal((input.DeviceId, AudioDeviceDirection.Capture), devices.SetDefaultCalls[1]);
        Assert.Equal((output.DeviceId, AudioDeviceDirection.Render, output.Volume), volume.SetVolumeCalls[0]);
        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
        Assert.Equal("Gaming", store.Data.ActiveProfile);
        Assert.True(appliedArg?.IsFullSuccess);
    }

    [Fact]
    public void RenameProfile_UpdatesNameAndPersists()
    {
        var (data, _, _) = BuildSeededData();
        data.ActiveProfile = "Gaming";
        var (manager, store, _, _, _) = CreateManager(data);
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.RenameProfile("Gaming", "Streaming");

        Assert.Equal("Streaming", manager.Profiles.Single().Name);
        Assert.Equal("Streaming", manager.ActiveProfile?.Name);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void RenameProfile_SameName_NoOps()
    {
        var (data, _, _) = BuildSeededData();
        var (manager, store, _, _, _) = CreateManager(data);
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.RenameProfile("Gaming", "Gaming");

        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, raised);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: AddComponent called with a Component whose Id already exists in the library.
    /// Who: A retry of "save component" or duplicate import.
    /// What: AddComponent returns false; library and store are unchanged; LibraryChanged is not raised.
    /// Why: Duplicate ids would silently break FindById's lookup contract for ProfileApplier.
    /// Where: ComponentLibrary.Add duplicate guard, surfaced via ProfileManager.AddComponent return value.
    /// </summary>
    [Fact]
    public void AddComponent_DuplicateId_ReturnsFalseNoSideEffects()
    {
        var (manager, store, _, _, _) = CreateManager();
        var output = NewOutput();
        manager.AddComponent(output);
        var raised = 0;
        manager.LibraryChanged += (_, _) => raised++;

        Assert.False(manager.AddComponent(output));
        Assert.Single(manager.Library.Outputs);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: RemoveComponent for an id that doesn't exist.
    /// Who: Stale UI deleting a component that was already removed elsewhere.
    /// What: Returns false; no save, no events.
    /// Why: Idempotent removal — no need for callers to wrap in try/catch.
    /// Where: ComponentLibrary.Remove returns false when nothing matched.
    /// </summary>
    [Fact]
    public void RemoveComponent_UnknownId_ReturnsFalseNoSideEffects()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.LibraryChanged += (_, _) => raised++;

        Assert.False(manager.RemoveComponent("ghost"));
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: RemoveComponent for an id that no profile references.
    /// Who: User cleaning up an unused EQ preset.
    /// What: LibraryChanged fires; ProfilesChanged does NOT fire (no profile changed).
    /// Why: Spurious ProfilesChanged events would force the UI to re-render the profiles list for no reason.
    /// Where: ProfileManager.RemoveComponent only fires ProfilesChanged when at least one profile mutated.
    /// </summary>
    [Fact]
    public void RemoveComponent_NotInAnyProfile_DoesNotRaiseProfilesChanged()
    {
        var (manager, _, _, _, _) = CreateManager();
        var orphan = NewOutput("orphan");
        manager.AddComponent(orphan);
        var profilesChanged = 0;
        manager.ProfilesChanged += (_, _) => profilesChanged++;

        Assert.True(manager.RemoveComponent(orphan.Id));
        Assert.Equal(0, profilesChanged);
    }

    /// <summary>
    /// Sad path: AddProfile called with a name that already exists.
    /// Who: ProfileEditor save handler when the user types an existing name.
    /// What: throws InvalidOperationException; no profile added, no persist, no event.
    /// Why: Profile name is the unique key for lookup, apply, active-profile tracking.
    /// Where: ProfileManager.AddProfile FindProfile guard.
    /// </summary>
    [Fact]
    public void AddProfile_DuplicateName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();
        manager.AddProfile(new AudioProfile { Name = "X" });
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        Assert.Throws<InvalidOperationException>(() => manager.AddProfile(new AudioProfile { Name = "X" }));
        Assert.Single(manager.Profiles);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: UpdateProfile for a name that was never added.
    /// Who: ProfileEditor save handler if the underlying profile was deleted in another window.
    /// What: throws InvalidOperationException; no list mutation, no persist.
    /// Why: Update must not silently insert — that would mask the deletion.
    /// Where: ProfileManager.UpdateProfile FindIndex &lt; 0 guard.
    /// </summary>
    [Fact]
    public void UpdateProfile_MissingName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.UpdateProfile(new AudioProfile { Name = "Ghost" }));
        Assert.Empty(manager.Profiles);
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// Sad path: RemoveProfile for a name that doesn't exist.
    /// Who: Stale tray-menu click after the profile was removed elsewhere.
    /// What: Silent no-op; no persist, no event.
    /// Why: Idempotent removal.
    /// Where: ProfileManager.RemoveProfile RemoveAll == 0 early return.
    /// </summary>
    [Fact]
    public void RemoveProfile_UnknownName_NoOps()
    {
        var (manager, store, _, _, _) = CreateManager();
        var raised = 0;
        manager.ProfilesChanged += (_, _) => raised++;

        manager.RemoveProfile("Ghost");

        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// Sad path: RenameProfile for a name that doesn't exist.
    /// Who: UI holding a stale reference to a profile that was deleted in another pane.
    /// What: Throws InvalidOperationException naming the missing profile.
    /// Why: Silently inserting would mask the deletion; silently no-op'ing would hide a UI state bug.
    /// Where: ProfileManager.RenameProfile FindProfile(oldName) null-coalesce-throw.
    /// </summary>
    [Fact]
    public void RenameProfile_UnknownOldName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.RenameProfile("Ghost", "New"));
        Assert.Equal(0, store.SaveCount);
    }

    /// <summary>
    /// Sad path: RenameProfile to a name that collides with another existing profile.
    /// Who: User typing a duplicate in the rename dialog.
    /// What: Throws InvalidOperationException; profile is NOT renamed.
    /// Why: Profile name is the unique key used by FindProfile / ApplyProfile / hotkey registration; collisions would corrupt lookup.
    /// Where: ProfileManager.RenameProfile FindProfile(newName) not-null guard.
    /// </summary>
    [Fact]
    public void RenameProfile_DuplicateNewName_Throws()
    {
        var (manager, store, _, _, _) = CreateManager();
        manager.AddProfile(new AudioProfile { Name = "A" });
        manager.AddProfile(new AudioProfile { Name = "B" });

        Assert.Throws<InvalidOperationException>(() => manager.RenameProfile("A", "B"));
        Assert.Equal("A", manager.Profiles[0].Name);
    }

    /// <summary>
    /// Sad path: RenameProfile called with an empty or whitespace-only name.
    /// Who: User hitting OK in the rename dialog without typing anything.
    /// What: Throws InvalidOperationException; profile unchanged.
    /// Why: Empty names are meaningless identifiers and would break every lookup.
    /// Where: ProfileManager.RenameProfile IsNullOrWhiteSpace guard.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameProfile_EmptyNewName_Throws(string newName)
    {
        var (data, _, _) = BuildSeededData();
        var (manager, _, _, _, _) = CreateManager(data);

        Assert.Throws<InvalidOperationException>(() => manager.RenameProfile("Gaming", newName));
    }

    /// <summary>
    /// Sad path: ApplyProfile for a name that doesn't exist.
    /// Who: Hotkey handler firing for a profile whose hotkey wasn't unregistered after delete.
    /// What: throws InvalidOperationException; no controller calls.
    /// Why: Loud failure beats silent no-op so the user notices and re-binds.
    /// Where: ProfileManager.ApplyProfile FindProfile null-coalesce-throw.
    /// </summary>
    [Fact]
    public void ApplyProfile_MissingName_Throws()
    {
        var (manager, _, devices, volume, spatial) = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.ApplyProfile("Ghost"));
        Assert.Empty(devices.SetDefaultCalls);
        Assert.Empty(volume.SetVolumeCalls);
        Assert.Empty(spatial.SetModeCalls);
    }

    /// <summary>
    /// Sad path: ApplyProfile encounters a partial-failure result from the applier (one device unplugged).
    /// Who: User triggers a hotkey for a profile whose output device just disconnected.
    /// What: Manager still marks the profile active, persists, and raises ProfileApplied with the error-bearing result.
    /// Why: Partial apply (input switched, output failed) is more useful than total bailout; UI uses the result for a toast.
    /// Where: ProfileManager.ApplyProfile passes the applier's result through unconditionally.
    /// </summary>
    [Fact]
    public void ApplyProfile_PartialFailure_StillActivatesAndRaisesWithErrors()
    {
        var (data, _, _) = BuildSeededData();
        var (manager, store, devices, _, _) = CreateManager(data);
        devices.OnSetDefault = (_, dir) =>
        {
            if (dir == AudioDeviceDirection.Render) throw new InvalidOperationException("missing");
        };
        ProfileApplyResult? appliedArg = null;
        manager.ProfileApplied += (_, r) => appliedArg = r;

        manager.ApplyProfile("Gaming");

        Assert.Equal("Gaming", manager.ActiveProfile?.Name);
        Assert.Equal("Gaming", store.Data.ActiveProfile);
        Assert.False(appliedArg!.IsFullSuccess);
        Assert.Contains(appliedArg.Errors, e => e.Step == "SetDefaultOutput");
    }
}
