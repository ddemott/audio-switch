using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

public sealed class ProfileStore : IProfileStore
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _filePath;

    public ProfileStore() : this(DefaultFilePath()) { }

    public ProfileStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AudioSwitch", "profiles.json");
    }

    public ProfileStoreData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new ProfileStoreData();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProfileStoreData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<ProfileStoreData>(json, JsonOptions);
            if (data is null || data.SchemaVersion < CurrentSchemaVersion)
            {
                QuarantineFile();
                return new ProfileStoreData();
            }
            return data;
        }
        catch (JsonException)
        {
            QuarantineFile();
            return new ProfileStoreData();
        }
    }

    public void Save(ProfileStoreData data)
    {
        data.SchemaVersion = CurrentSchemaVersion;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(_filePath))
        {
            File.Replace(tempPath, _filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _filePath);
        }
    }

    private void QuarantineFile()
    {
        var backup = $"{_filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Move(_filePath, backup, overwrite: true);
        }
        catch
        {
            // Best-effort: if we can't even move it, leave the file and let the next save overwrite.
        }
    }
}
