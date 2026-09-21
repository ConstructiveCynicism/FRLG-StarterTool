using System.Text.Json;
using System.Text.Json.Nodes;

namespace FRLG.StarterTool.Core.Settings;

public sealed class TimerLibrary
{
    private readonly string _root;

    public TimerLibrary(string root) => _root = root;

    public static TimerLibrary Default => new(SettingsStore.DefaultDirectory);

    public string TimersDirectory => Path.Combine(_root, "timers");

    public string DelayersPath => Path.Combine(_root, "igtdelayers.json");

    public List<TimerSet> ReadSets() => SetFiles().Select(entry => entry.Set).ToList();

    public TimerSet? Find(string name) =>
        SetFiles().FirstOrDefault(entry => TimerSet.NameEquals(entry.Set.Name, name)).Set;

    public void Write(TimerSet set)
    {
        List<(string Path, TimerSet Set)> files = SetFiles();
        string? path = files.FirstOrDefault(entry => TimerSet.NameEquals(entry.Set.Name, set.Name)).Path;
        path ??= FreeFile(set.Name);

        Directory.CreateDirectory(TimersDirectory);
        PresetFile.Write(path, set);
    }

    public void Delete(string name)
    {
        foreach ((string path, TimerSet set) in SetFiles())
        {
            if (TimerSet.NameEquals(set.Name, name)) File.Delete(path);
        }
    }

    public void Rename(string oldName, string newName)
    {
        TimerSet? set = Find(oldName);
        if (set == null) return;

        Delete(oldName);
        Delete(newName);
        set.Name = newName;
        Write(set);
    }

    public static TimerSet? ReadImport(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        TimerSet? set = root switch
        {
            JsonArray array => new TimerSet
            {
                Timers = array.Deserialize<List<FixedTimerEntry>>(SettingsStore.Options) ?? new List<FixedTimerEntry>()
            },
            JsonObject => root.Deserialize<TimerSet>(SettingsStore.Options),
            _ => null
        };
        if (set == null) return null;

        set.Normalize();
        if (set.Timers.Count == 0) return null;
        if (set.Name.Length == 0) set.Name = Path.GetFileNameWithoutExtension(path);
        return set;
    }

    public static List<IgtTimerEntry>? ReadIgtTimers(string path)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));

        List<IgtTimerEntry>? timers = root switch
        {
            JsonArray array => array.Deserialize<List<IgtTimerEntry>>(SettingsStore.Options),
            JsonObject => root.Deserialize<IgtTimersFile>(SettingsStore.Options)?.Timers,
            _ => null
        };
        if (timers == null) return null;

        timers.RemoveAll(timer => timer == null);
        foreach (IgtTimerEntry timer in timers) timer.Normalize();
        return timers.Count == 0 ? null : timers;
    }

    public static void WriteIgtTimers(string path, IEnumerable<IgtTimerEntry> timers) =>
        PresetFile.Write(path, new IgtTimersFile { Timers = timers.Select(timer => timer.Clone()).ToList() });

    public IgtDelayersFile ReadDelayers()
    {
        try
        {
            if (File.Exists(DelayersPath))
            {
                IgtDelayersFile? file = PresetFile.Read<IgtDelayersFile>(DelayersPath);
                if (file?.Normalize().Games.Count > 0) return file;
                return IgtDelayersFile.Defaults();
            }

            IgtDelayersFile defaults = IgtDelayersFile.Defaults();
            Directory.CreateDirectory(_root);
            PresetFile.Write(DelayersPath, defaults);
            return defaults;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return IgtDelayersFile.Defaults();
        }
    }

    private List<(string Path, TimerSet Set)> SetFiles()
    {
        var found = new List<(string, TimerSet)>();
        if (!Directory.Exists(TimersDirectory)) return found;

        foreach (string path in Directory.EnumerateFiles(TimersDirectory, "*.json")
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                TimerSet? set = ReadImport(path);
                if (set != null) found.Add((path, set));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        return found;
    }

    private string FreeFile(string name)
    {
        string stem = PresetFile.Stem(name);
        string path = Path.Combine(TimersDirectory, stem + ".json");
        for (int n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(TimersDirectory, $"{stem} ({n}).json");
        }

        return path;
    }
}
