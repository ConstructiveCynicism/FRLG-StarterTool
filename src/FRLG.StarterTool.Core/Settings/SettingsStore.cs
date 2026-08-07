using System.Text.Json;

namespace FRLG.StarterTool.Core.Settings;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
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
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            if (settings != null)
            {
                return settings.Normalize();
            }
            error = "The settings file was empty.";
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            error = e.Message;
        }

        return new AppSettings().Normalize();
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
