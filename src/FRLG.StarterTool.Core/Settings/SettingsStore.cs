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
            return WithLibrary(new AppSettings(), path);
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
                return WithLibrary(settings, path);
            }
            error = "The settings file was empty.";
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                                      or InvalidOperationException or NotSupportedException)
        {
            error = e.Message;
        }

        return WithLibrary(new AppSettings(), path);
    }

    private static readonly string[] LibraryKeys = { nameof(AppSettings.Presets), nameof(AppSettings.EncounterRoutes) };

    private static AppSettings WithLibrary(AppSettings settings, string path)
    {
        PresetLibrary library = PresetLibrary.For(path);
        List<FilterPreset> legacyFilters = (settings.Presets ?? new()).Where(preset => preset != null).ToList();
        List<EncounterRoutePreset> legacyRoutes = (settings.EncounterRoutes ?? new()).Where(route => route != null).ToList();

        List<FilterPreset> filters = library.ReadFilters();
        List<FilterPreset> newFilters = legacyFilters
            .Where(legacy => !filters.Any(filter => FilterPreset.NameEquals(filter.Name, legacy.Name)))
            .ToList();
        List<EncounterRoutePreset> routes = library.ReadRoutes();
        List<EncounterRoutePreset> newRoutes = legacyRoutes
            .Where(legacy => !routes.Any(route => EncounterRoutePreset.NameEquals(route.Name, legacy.Name)))
            .ToList();

        settings.Presets = filters.Concat(newFilters).ToList();
        settings.EncounterRoutes = routes.Concat(newRoutes).ToList();
        settings.Normalize();

        if (newFilters.Count > 0 || newRoutes.Count > 0)
        {
            try
            {
                library.Sync(newFilters.Where(settings.Presets.Contains), newRoutes.Where(settings.EncounterRoutes.Contains));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        return settings;
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

            bool libraryWritten = true;
            try
            {
                PresetLibrary.For(path).Sync(settings.Presets, settings.EncounterRoutes);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                libraryWritten = false;
                error = e.Message;
            }

            settings.Version = SettingsMigrations.CurrentVersion;
            JsonObject root = JsonSerializer.SerializeToNode(settings, Options)!.AsObject();
            if (libraryWritten)
            {
                foreach (string key in LibraryKeys) root.Remove(key);
            }
            File.WriteAllText(path, root.ToJsonString(Options));
            return libraryWritten;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            error = e.Message;
            return false;
        }
    }
}
