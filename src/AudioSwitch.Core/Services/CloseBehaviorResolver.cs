using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

public static class CloseBehaviorResolver
{
    public static WindowCloseBehavior UpdatePreference(
        WindowCloseBehavior current,
        CloseAction chosen,
        bool rememberChoice)
    {
        if (!rememberChoice) return current;
        return chosen switch
        {
            CloseAction.MinimizeToTray => WindowCloseBehavior.MinimizeToTray,
            CloseAction.Exit => WindowCloseBehavior.Exit,
            _ => current,
        };
    }
}
