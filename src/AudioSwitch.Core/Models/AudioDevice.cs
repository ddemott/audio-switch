namespace AudioSwitch.Core.Models;

public enum AudioDeviceDirection
{
    Render,
    Capture,
}

public sealed record AudioDevice(
    string Id,
    string Name,
    AudioDeviceDirection Direction,
    bool IsDefault);
