using System.IO;

namespace AudioSwitch.Core.Services;

public static class PortableMode
{
    public const string MarkerFileName = "portable.flag";

    public static bool IsActive(string? baseDir)
    {
        if (string.IsNullOrWhiteSpace(baseDir)) return false;
        return File.Exists(Path.Combine(baseDir, MarkerFileName));
    }
}
