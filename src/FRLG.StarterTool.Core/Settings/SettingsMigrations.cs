using System.Text.Json;
using System.Text.Json.Nodes;

namespace FRLG.StarterTool.Core.Settings;

public static class SettingsMigrations
{
    public const int CurrentVersion = 4;

    private static readonly Dictionary<int, Action<JsonObject>> Steps = new()
    {
        [1] = HotkeysToChords,
        [2] = EncounterDelayToMilliseconds,
        [3] = HideConstraintsToViewSwitches
    };

    public static int VersionOf(JsonObject root)
    {
        if (root["Version"] is JsonValue value && value.TryGetValue(out int version) && version > 0)
        {
            return version;
        }

        return 1;
    }

    public static int Upgrade(JsonObject root)
    {
        int from = VersionOf(root);
        if (from >= CurrentVersion) return 0;

        for (int version = from; version < CurrentVersion; version++)
        {
            if (Steps.TryGetValue(version, out Action<JsonObject>? step)) step(root);
            root["Version"] = version + 1;
        }

        return from;
    }

    private static void HotkeysToChords(JsonObject root)
    {
        foreach ((string name, JsonNode? node) in root.ToList())
        {
            if (node is not JsonObject hotkey) continue;
            if (!hotkey.ContainsKey("Primary") && !hotkey.ContainsKey("Secondary")) continue;
            if (hotkey.ContainsKey("Chords")) continue;

            var chords = new JsonArray();
            foreach (string key in new[] { "Primary", "Secondary" })
            {
                if (hotkey[key] is JsonValue value && value.TryGetValue(out int vk) && vk > 0)
                {
                    string chord = InputCode.Key(vk).ToString();
                    if (!chords.Any(c => c?.GetValue<string>() == chord)) chords.Add(chord);
                }
            }

            var replaced = new JsonObject { ["Chords"] = chords };
            if (hotkey["Global"] is JsonValue global && global.TryGetValue(out bool isGlobal))
            {
                replaced["Global"] = isGlobal;
            }

            root[name] = replaced;
        }
    }

    private static void EncounterDelayToMilliseconds(JsonObject root)
    {
        MoveDelayToMs(root, "EncounterDelay", "EncounterDelayMs");
        if (root["EncounterRoutes"] is JsonArray routes)
        {
            foreach (JsonNode? node in routes)
            {
                if (node is JsonObject route) MoveDelayToMs(route, "Delay", "DelayMs");
            }
        }
    }

    private static void MoveDelayToMs(JsonObject owner, string from, string to)
    {
        if (owner[from] is JsonValue value && value.TryGetValue(out int frames))
        {
            if (!owner.ContainsKey(to)) owner[to] = (int)Math.Round(frames * 1000.0 / GbaFps);
        }
        owner.Remove(from);
    }

    private static void HideConstraintsToViewSwitches(JsonObject root)
    {
        if (root["HideConstraints"] is JsonValue value && value.TryGetValue(out bool hidden) && hidden)
        {
            if (!root.ContainsKey("ViewConstraints")) root["ViewConstraints"] = false;
        }

        root.Remove("HideConstraints");
        root.Remove("NpcGridVisible");
    }

    private const double GbaFps = 59.7275;

    public static string BackupPath(string settingsPath, int version)
    {
        string directory = Path.GetDirectoryName(settingsPath) ?? "";
        string name = Path.GetFileNameWithoutExtension(settingsPath);
        string extension = Path.GetExtension(settingsPath);
        return Path.Combine(directory, name + ".v" + version + extension);
    }

    public static JsonObject? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
