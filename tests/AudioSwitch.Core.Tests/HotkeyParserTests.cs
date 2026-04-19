using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class HotkeyParserTests
{
    // === Happy path ===

    [Theory]
    [InlineData("Ctrl+Shift+1", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x31u)]
    [InlineData("Alt+F4", HotkeyModifiers.Alt, 0x73u)]
    [InlineData("Win+A", HotkeyModifiers.Win, 0x41u)]
    [InlineData("Ctrl+Alt+Shift+Win+Z", HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, 0x5Au)]
    [InlineData("F12", HotkeyModifiers.None, 0x7Bu)]
    [InlineData("F24", HotkeyModifiers.None, 0x87u)]
    public void TryParse_ValidInput_ReturnsBinding(string input, HotkeyModifiers expectedMods, uint expectedKey)
    {
        Assert.True(HotkeyParser.TryParse(input, out var binding));
        Assert.Equal(expectedMods, binding.Modifiers);
        Assert.Equal(expectedKey, binding.VirtualKey);
    }

    [Theory]
    [InlineData("ctrl+a")]
    [InlineData("CTRL+A")]
    [InlineData("Control+A")]
    [InlineData("Windows+A")]
    public void TryParse_CaseAndSynonymInsensitive(string input)
    {
        Assert.True(HotkeyParser.TryParse(input, out _));
    }

    [Theory]
    [InlineData(" Ctrl + Shift + 1 ")]
    [InlineData("Ctrl+ Shift+1")]
    public void TryParse_ToleratesWhitespaceAroundTokens(string input)
    {
        Assert.True(HotkeyParser.TryParse(input, out var binding));
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, binding.Modifiers);
        Assert.Equal(0x31u, binding.VirtualKey);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: null/empty/whitespace string passed to parser.
    /// Who: ProfileEditor view-model passing an unset Hotkey field, or HotkeyService iterating profiles where the user cleared the hotkey.
    /// What: TryParse returns false; out binding is left default; nothing is registered downstream.
    /// Why: No hotkey is a legal profile state (mouse-only users) — failing loudly would force callers to special-case it.
    /// Where: HotkeyParser.TryParse string.IsNullOrWhiteSpace guard at the top.
    /// How: Pass null, "", "   " and assert false.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_EmptyOrNull_ReturnsFalse(string? input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }

    /// <summary>
    /// Sad path: only modifiers, no actual key (e.g. user typed "Ctrl+Shift" and forgot the key).
    /// Who: User input via ProfileEditor hotkey-capture box that didn't enforce a non-modifier press.
    /// What: TryParse returns false; no binding is produced.
    /// Why: Win32 RegisterHotKey requires a virtual-key — registering modifiers alone has no meaning.
    /// Where: HotkeyParser.TryParse !key.HasValue final guard.
    /// </summary>
    [Theory]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Win+Alt")]
    public void TryParse_OnlyModifiers_ReturnsFalse(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }

    /// <summary>
    /// Sad path: more than one non-modifier key (e.g. "A+B").
    /// Who: User typed two letters; or a corrupt profiles.json hotkey field.
    /// What: TryParse returns false on the second key.
    /// Why: Win32 RegisterHotKey accepts a single virtual-key — chord keys (multi-key) are not supported by the OS API.
    /// Where: HotkeyParser.TryParse `if (key.HasValue) return false` guard.
    /// </summary>
    [Theory]
    [InlineData("A+B")]
    [InlineData("Ctrl+A+B")]
    [InlineData("F1+F2")]
    public void TryParse_MultipleKeys_ReturnsFalse(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }

    /// <summary>
    /// Sad path: unrecognized token (typo or unsupported key).
    /// Who: User typed "Hyper+A" or migrated a config from a different app.
    /// What: TryParse returns false on the unknown token.
    /// Why: Better to refuse than to silently swallow — caller (HotkeyService) will skip the profile and surface a notice.
    /// Where: HotkeyParser.TryParseKey rejection branch.
    /// </summary>
    [Theory]
    [InlineData("Hyper+A")]
    [InlineData("Ctrl+Mango")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+!")]
    public void TryParse_UnknownToken_ReturnsFalse(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }

    /// <summary>
    /// Sad path: same modifier listed twice ("Ctrl+Ctrl+A").
    /// Who: User input or fat-fingered config edit.
    /// What: TryParse returns false rather than silently OR-ing the duplicate.
    /// Why: Duplicate modifiers signal user error — refusing surfaces the typo instead of binding an unintended hotkey.
    /// Where: HotkeyParser.TryParse `(modifiers &amp; mod) != 0` duplicate-detect guard.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Ctrl+A")]
    [InlineData("Shift+Shift+1")]
    public void TryParse_DuplicateModifier_ReturnsFalse(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }

    /// <summary>
    /// Sad path: trailing or leading "+" produces an empty token after split (e.g. "Ctrl+").
    /// Who: User input mid-typing or trailing-comma-style edit.
    /// What: TryParse returns false because the leftover empty token has no valid interpretation.
    /// Why: An incomplete chord string should not silently parse as just the modifier — that would re-introduce the only-modifiers bug.
    /// Where: HotkeyParser.TryParse token.Length == 0 guard inside the loop.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+")]
    [InlineData("+A")]
    public void TryParse_EmptyToken_ReturnsFalse(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _));
    }
}
