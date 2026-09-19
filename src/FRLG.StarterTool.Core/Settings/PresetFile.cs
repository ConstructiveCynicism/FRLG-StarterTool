using System.Text.Json;

namespace FRLG.StarterTool.Core.Settings;

public static class PresetFile
{
    public static void Write<T>(string path, T preset) =>
        File.WriteAllText(path, JsonSerializer.Serialize(preset, SettingsStore.Options));

    public static T? Read<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), SettingsStore.Options);

    public static string Stem(string name)
    {
        char[] illegal = Path.GetInvalidFileNameChars();
        string stem = new string(name.Trim().Select(c => illegal.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return stem.Length == 0 ? "preset" : stem;
    }
}
