namespace AudioSwitch.Core.Interfaces;

public interface IHotkeyService : IDisposable
{
    bool Register(string hotkeyId, string hotkey, Action onPressed);

    void Unregister(string hotkeyId);

    void UnregisterAll();
}
