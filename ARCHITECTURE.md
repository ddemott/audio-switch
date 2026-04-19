# Architecture

## Tech Stack
- **Language:** C# (.NET 8)
- **UI Framework:** WPF
- **Audio API:** Windows Core Audio API (MMDevice / WASAPI) via NAudio for COM interop
- **Profile Storage:** JSON (System.Text.Json)
- **System Tray:** Hardcodet.NotifyIcon.Wpf
- **Hotkeys:** Win32 RegisterHotKey via P/Invoke

## Project Structure

```
AudioSwitch/
├── AudioSwitch.sln
├── src/
│   ├── AudioSwitch.App/              # WPF application (entry point)
│   │   ├── App.xaml                   # Application startup, tray icon init
│   │   ├── Views/
│   │   │   ├── DashboardWindow.xaml   # Main profile management UI
│   │   │   ├── ProfileEditorView.xaml # Create/edit a profile
│   │   │   └── TrayIconMenu.xaml      # Context menu for system tray
│   │   └── ViewModels/
│   │       ├── DashboardViewModel.cs
│   │       └── ProfileEditorViewModel.cs
│   │
│   ├── AudioSwitch.Core/             # Business logic (no UI dependencies)
│   │   ├── Models/
│   │   │   ├── AudioProfile.cs        # Profile definition (name, devices, spatial mode, volumes, hotkey)
│   │   │   └── AudioDevice.cs         # Device descriptor (id, name, type, direction)
│   │   ├── Services/
│   │   │   ├── AudioDeviceService.cs  # Enumerate/switch devices via Core Audio API
│   │   │   ├── ProfileManager.cs      # CRUD profiles, apply profile (orchestrator)
│   │   │   ├── ProfileStore.cs        # Load/save profiles to JSON
│   │   │   └── HotkeyService.cs       # Register/unregister global hotkeys
│   │   └── Interfaces/
│   │       ├── IAudioDeviceService.cs
│   │       ├── IProfileManager.cs
│   │       ├── IProfileStore.cs
│   │       └── IHotkeyService.cs
│   │
│   └── AudioSwitch.Audio/            # Low-level audio platform code
│       ├── CoreAudioController.cs     # MMDevice COM interop, set default device
│       ├── SpatialAudioController.cs  # Toggle spatial audio format (stereo/Sonic/THX/Atmos)
│       └── VolumeController.cs        # Get/set device volume levels
│
└── tests/
    ├── AudioSwitch.Core.Tests/
    └── AudioSwitch.Audio.Tests/
```

## Layer Responsibilities

### AudioSwitch.App (Presentation)
WPF application layer. Owns the system tray icon, dashboard window, and all XAML views. Uses MVVM pattern — ViewModels bind to Core services via interfaces. This layer has no audio logic.

### AudioSwitch.Core (Business Logic)
Platform-agnostic business logic. Defines the profile model, orchestrates profile switching, manages hotkey registration, and handles JSON persistence. Depends on interfaces, not implementations.

Key flow — **applying a profile:**
```
ProfileManager.ApplyProfile(profile)
  → AudioDeviceService.SetDefaultInput(profile.InputDeviceId)
  → AudioDeviceService.SetDefaultOutput(profile.OutputDeviceId)
  → SpatialAudioController.SetMode(profile.SpatialMode)
  → VolumeController.SetVolume(profile.OutputDeviceId, profile.OutputVolume)
  → VolumeController.SetVolume(profile.InputDeviceId, profile.InputVolume)
```

### AudioSwitch.Audio (Platform)
Thin wrapper around Windows Core Audio COM APIs. Handles the actual P/Invoke and COM interop. Isolated so the rest of the app never touches COM directly.

## Data Flow

```
User Action (hotkey / tray click / dashboard)
    │
    ▼
ViewModel or TrayIcon event handler
    │
    ▼
ProfileManager.ApplyProfile(profileName)
    │
    ├──► AudioDeviceService  ──► CoreAudioController (COM)
    ├──► SpatialAudioController (COM/Registry)
    └──► VolumeController (COM)
```

## Profile JSON Schema

```json
{
  "profiles": [
    {
      "name": "Teams Call",
      "hotkey": "Ctrl+Shift+1",
      "inputDevice": {
        "id": "{0.0.1.00000000}.{guid}",
        "name": "USB Headset Mic"
      },
      "outputDevice": {
        "id": "{0.0.0.00000000}.{guid}",
        "name": "USB Headset"
      },
      "spatialMode": "Stereo",
      "inputVolume": 80,
      "outputVolume": 65
    }
  ],
  "activeProfile": "Teams Call"
}
```

Profiles stored at: `%APPDATA%/AudioSwitch/profiles.json`

## Key Dependencies
| Package | Purpose |
|---|---|
| NAudio | Core Audio API COM interop (MMDevice enumeration, endpoint control) |
| Hardcodet.NotifyIcon.Wpf | System tray icon with WPF context menu support |
| CommunityToolkit.Mvvm | MVVM source generators (ObservableProperty, RelayCommand) |
| System.Text.Json | Profile serialization |

## V2 Extension Point: Equalizer
The `AudioSwitch.Audio` project will gain an `EqualizerController.cs` that swaps Equalizer APO config files per profile. Each profile will reference an APO config file. No real-time DSP — config file swap only, zero latency.
