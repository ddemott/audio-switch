using System.Runtime.InteropServices;
using System.Windows.Interop;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Services;

public sealed class HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Registration> _byNumericId = new();
    private readonly Dictionary<string, int> _byHotkeyId = new();
    private int _nextId = 9000;
    private bool _disposed;

    public HotkeyService(HwndSource source)
    {
        _source = source;
        _hwnd = source.Handle;
        _source.AddHook(WndProc);
    }

    public bool Register(string hotkeyId, string hotkey, Action onPressed)
    {
        ThrowIfDisposed();

        if (!HotkeyParser.TryParse(hotkey, out var binding))
        {
            return false;
        }

        Unregister(hotkeyId);

        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, (uint)binding.Modifiers, binding.VirtualKey))
        {
            return false;
        }

        _byNumericId[id] = new Registration(hotkeyId, onPressed);
        _byHotkeyId[hotkeyId] = id;
        return true;
    }

    public void Unregister(string hotkeyId)
    {
        if (!_byHotkeyId.TryGetValue(hotkeyId, out var id))
        {
            return;
        }
        UnregisterHotKey(_hwnd, id);
        _byHotkeyId.Remove(hotkeyId);
        _byNumericId.Remove(id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _byNumericId.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }
        _byHotkeyId.Clear();
        _byNumericId.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        var id = wParam.ToInt32();
        if (_byNumericId.TryGetValue(id, out var registration))
        {
            try
            {
                registration.OnPressed();
            }
            catch
            {
                // Hotkey callbacks must never crash the message loop.
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyService));
    }

    private sealed record Registration(string HotkeyId, Action OnPressed);
}
