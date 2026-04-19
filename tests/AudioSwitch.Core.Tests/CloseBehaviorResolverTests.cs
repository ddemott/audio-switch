using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class CloseBehaviorResolverTests
{
    // === Happy path ===

    [Fact]
    public void UpdatePreference_Remember_MinimizeToTray_PersistsMinimize()
    {
        var next = CloseBehaviorResolver.UpdatePreference(
            WindowCloseBehavior.Prompt, CloseAction.MinimizeToTray, rememberChoice: true);

        Assert.Equal(WindowCloseBehavior.MinimizeToTray, next);
    }

    [Fact]
    public void UpdatePreference_Remember_Exit_PersistsExit()
    {
        var next = CloseBehaviorResolver.UpdatePreference(
            WindowCloseBehavior.Prompt, CloseAction.Exit, rememberChoice: true);

        Assert.Equal(WindowCloseBehavior.Exit, next);
    }

    [Fact]
    public void UpdatePreference_NoRemember_LeavesPreferenceUnchanged()
    {
        var next = CloseBehaviorResolver.UpdatePreference(
            WindowCloseBehavior.Prompt, CloseAction.MinimizeToTray, rememberChoice: false);

        Assert.Equal(WindowCloseBehavior.Prompt, next);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: user clicks Cancel on the close prompt with "remember" checked.
    /// Who: MainWindow Closing handler after the user dismissed the prompt without choosing an outcome.
    /// What: Preference is left untouched — Cancel is not a choice to remember.
    /// Why: "Remember my choice" should only apply when the user actually chose an action; otherwise we would lock them out of seeing the prompt again despite no real preference.
    /// Where: CloseBehaviorResolver switch default arm for CloseAction.Cancel.
    /// How: Call with Cancel + rememberChoice=true; assert current preference returned unchanged.
    /// </summary>
    [Fact]
    public void UpdatePreference_CancelWithRemember_DoesNotOverwritePreference()
    {
        var next = CloseBehaviorResolver.UpdatePreference(
            WindowCloseBehavior.MinimizeToTray, CloseAction.Cancel, rememberChoice: true);

        Assert.Equal(WindowCloseBehavior.MinimizeToTray, next);
    }

    /// <summary>
    /// Sad path: user had a previously-remembered preference (Exit) and, on some later interaction where
    /// we ran the prompt again (e.g. Shift-click forced the prompt), they chose Minimize but did not tick "remember".
    /// Who: MainWindow Closing handler invoking the resolver with rememberChoice=false.
    /// What: Old preference (Exit) is preserved — this close uses Minimize, but the saved preference doesn't change.
    /// Why: Unchecked "remember" means "just do it this once." Overwriting the saved preference would silently surprise the user next time.
    /// Where: CloseBehaviorResolver early-return when rememberChoice is false.
    /// How: Call with current=Exit, chosen=MinimizeToTray, rememberChoice=false; assert Exit preserved.
    /// </summary>
    [Fact]
    public void UpdatePreference_NoRemember_PreservesExistingRememberedChoice()
    {
        var next = CloseBehaviorResolver.UpdatePreference(
            WindowCloseBehavior.Exit, CloseAction.MinimizeToTray, rememberChoice: false);

        Assert.Equal(WindowCloseBehavior.Exit, next);
    }
}
