using System.IO;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class PortableModeTests : IDisposable
{
    private readonly string _tempDir;

    public PortableModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"audioswitch-portable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // === Happy path ===

    [Fact]
    public void IsActive_MarkerFilePresent_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, PortableMode.MarkerFileName), string.Empty);

        Assert.True(PortableMode.IsActive(_tempDir));
    }

    [Fact]
    public void IsActive_MarkerFileAbsent_ReturnsFalse()
    {
        Assert.False(PortableMode.IsActive(_tempDir));
    }

    [Fact]
    public void MarkerFileName_IsPortableFlag()
    {
        Assert.Equal("portable.flag", PortableMode.MarkerFileName);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: caller passes a null, empty, or whitespace base directory.
    /// Who: App composition during startup if Environment.ProcessPath returns null (rare — shouldn't happen in published WPF builds, but .NET docs permit it).
    /// What: IsActive returns false rather than throwing ArgumentNullException or letting Path.Combine crash.
    /// Why: A null base dir means "we don't know where we are" — portable mode requires a concrete directory to look in. Returning false is the conservative answer and keeps AppHost construction from blowing up.
    /// Where: PortableMode.IsActive early-out guard on the baseDir argument.
    /// How: Theory with null, empty, and whitespace; all must return false without throwing.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsActive_NullOrWhitespaceBaseDir_ReturnsFalse(string? baseDir)
    {
        Assert.False(PortableMode.IsActive(baseDir));
    }
}
