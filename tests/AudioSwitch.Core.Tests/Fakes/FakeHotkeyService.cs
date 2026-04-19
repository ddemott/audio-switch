using AudioSwitch.Core.Interfaces;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeHotkeyService : IHotkeyService
{
    public List<(string Id, string Hotkey)> Registered { get; } = new();

    public Dictionary<string, Action> Callbacks { get; } = new();

    public int UnregisterAllCount { get; private set; }

    public bool Register(string hotkeyId, string hotkey, Action onPressed)
    {
        Registered.Add((hotkeyId, hotkey));
        Callbacks[hotkeyId] = onPressed;
        return true;
    }

    public void Unregister(string hotkeyId)
    {
        Callbacks.Remove(hotkeyId);
        Registered.RemoveAll(r => r.Id == hotkeyId);
    }

    public void UnregisterAll()
    {
        UnregisterAllCount++;
        Registered.Clear();
        Callbacks.Clear();
    }

    public void Dispose() { }
}
