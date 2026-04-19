using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

public static class HotkeyParser
{
    private static readonly Dictionary<string, HotkeyModifiers> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = HotkeyModifiers.Control,
        ["control"] = HotkeyModifiers.Control,
        ["alt"] = HotkeyModifiers.Alt,
        ["shift"] = HotkeyModifiers.Shift,
        ["win"] = HotkeyModifiers.Win,
        ["windows"] = HotkeyModifiers.Win,
    };

    public static bool TryParse(string? input, out HotkeyBinding binding)
    {
        binding = default!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        uint? key = null;

        foreach (var rawToken in input.Split('+'))
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
            {
                return false;
            }

            if (Modifiers.TryGetValue(token, out var mod))
            {
                if ((modifiers & mod) != 0)
                {
                    return false;
                }
                modifiers |= mod;
                continue;
            }

            if (key.HasValue)
            {
                return false;
            }
            if (!TryParseKey(token, out var vk))
            {
                return false;
            }
            key = vk;
        }

        if (!key.HasValue)
        {
            return false;
        }

        binding = new HotkeyBinding(modifiers, key.Value);
        return true;
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        virtualKey = 0;
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                virtualKey = c;
                return true;
            }
            if (c is >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }
            return false;
        }

        if ((token[0] == 'F' || token[0] == 'f')
            && int.TryParse(token.AsSpan(1), out var fnum)
            && fnum is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + (fnum - 1));
            return true;
        }

        return false;
    }
}
