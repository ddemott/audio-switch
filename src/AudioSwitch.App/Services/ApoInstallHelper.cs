using System.Diagnostics;
using System.IO;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Services;

// Locates the bundled Equalizer APO installer, launches it (UAC auto-elevates
// from the installer's own manifest), and asks Windows to reboot once the
// install completes. Doesn't poll APO state itself — IsInstalled defers to
// ApoInstallation, which probes the filesystem each call.
public sealed class ApoInstallHelper
{
    public const string InstallerFileName = "EqualizerAPO64.exe";

    private readonly string _installerPath;

    public ApoInstallHelper()
    {
        var baseDir = AppContext.BaseDirectory;
        _installerPath = Path.Combine(baseDir, "Assets", "Tools", InstallerFileName);
    }

    public string InstallerPath => _installerPath;

    public bool IsInstallerBundled => File.Exists(_installerPath);

    public bool IsInstalled => ApoInstallation.IsInstalled();

    // Returns the launched Process (with EnableRaisingEvents = true) so the
    // caller can hook Exited. Returns null when the bundled installer file
    // is missing — caller should surface that as a separate UX path.
    public Process? LaunchInstaller()
    {
        if (!IsInstallerBundled) return null;
        var psi = new ProcessStartInfo(_installerPath)
        {
            UseShellExecute = true,  // required for UAC elevation
        };
        var process = Process.Start(psi);
        if (process is not null)
        {
            process.EnableRaisingEvents = true;
        }
        return process;
    }

    public void RebootWindows()
    {
        // /t 5 gives the user 5 seconds to abort via `shutdown /a` if they
        // change their mind; /c provides the toast message Windows shows.
        var psi = new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"AudioSwitch — Equalizer APO install reboot\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }
}
