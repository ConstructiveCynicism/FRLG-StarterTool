using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FRLG.StarterTool.Core.Settings;

public static class SettingsStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "frlg-startertool");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public static AppSettings Load(string path, out string? error)
    {
        error = null;

        if (!File.Exists(path))
        {
            return new AppSettings().Normalize();
        }

        try
        {
            string json = File.ReadAllText(path);

            JsonObject? root = SettingsMigrations.Parse(json);
            if (root != null)
            {
                int upgradedFrom = SettingsMigrations.Upgrade(root);
                if (upgradedFrom > 0)
                {
                    json = root.ToJsonString(Options);
                    WriteBack(path, upgradedFrom, json);
                }
            }

            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (settings != null)
            {
                return settings.Normalize();
            }
            error = "The settings file was empty.";
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                                      or InvalidOperationException or NotSupportedException)
        {
            error = e.Message;
        }

        return new AppSettings().Normalize();
    }

    private static void WriteBack(string path, int fromVersion, string upgradedJson)
    {
        try
        {
            string backup = SettingsMigrations.BackupPath(path, fromVersion);
            if (!File.Exists(backup)) File.Copy(path, backup);
            File.WriteAllText(path, upgradedJson);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static bool Save(string path, AppSettings settings, out string? error)
    {
        error = null;
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            settings.Version = SettingsMigrations.CurrentVersion;
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            error = e.Message;
            return false;
        }
    }
}
