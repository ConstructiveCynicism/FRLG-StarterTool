using System.Text.Json;

namespace FRLG.StarterTool.Core.Settings;

public static class PresetFile
{
    public static void Write<T>(string path, T preset) =>
        File.WriteAllText(path, JsonSerializer.Serialize(preset, SettingsStore.Options));

    public static T? Read<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), SettingsStore.Options);
}
