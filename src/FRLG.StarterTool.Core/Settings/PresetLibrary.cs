using System.Text.Json;

namespace FRLG.StarterTool.Core.Settings;

public sealed class PresetLibrary
{
    private readonly string _root;

    public PresetLibrary(string root) => _root = root;

    public static PresetLibrary Default => new(SettingsStore.DefaultDirectory);

    public static PresetLibrary For(string settingsPath) =>
        new(Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? SettingsStore.DefaultDirectory);

    public string FiltersDirectory => Path.Combine(_root, "filters");

    public string SeedsDirectory => Path.Combine(_root, "seeds");

    public string LegacyReferenceDirectory => Path.Combine(_root, "references");

    public List<FilterPreset> ReadFilters() =>
        FilterFiles().Select(entry => entry.Preset).ToList();

    public void DeleteFilter(string name)
    {
        foreach ((string path, FilterPreset preset) in FilterFiles())
        {
            if (FilterPreset.NameEquals(preset.Name, name)) File.Delete(path);
        }
    }

    public List<EncounterRoutePreset> ReadRoutes() =>
        RouteFolders().Select(entry => entry.Preset).ToList();

    public string? RouteDirectory(string name) =>
        RouteFolders().FirstOrDefault(entry => EncounterRoutePreset.NameEquals(entry.Preset.Name, name)).Directory;

    public string EnsureRouteDirectory(EncounterRoutePreset route) =>
        RouteDirectory(route.Name) ?? WriteRoute(route, RouteFolders(), pictures: null);

    public string? ReferencePath(string routeName, CaptureReference reference)
    {
        string? directory = RouteDirectory(routeName);
        if (directory == null || reference.Image.Length == 0) return null;
        string path = Path.Combine(directory, Path.GetFileName(reference.Image));
        return File.Exists(path) ? path : null;
    }

    public void DeleteRoute(string name)
    {
        foreach ((string directory, _, EncounterRoutePreset preset) in RouteFolders())
        {
            if (EncounterRoutePreset.NameEquals(preset.Name, name)) Directory.Delete(directory, recursive: true);
        }
    }

    public void RenameRoute(string oldName, string newName)
    {
        List<(string Directory, string File, EncounterRoutePreset Preset)> folders = RouteFolders();
        var found = folders.FirstOrDefault(entry => EncounterRoutePreset.NameEquals(entry.Preset.Name, oldName));
        if (found.Directory == null) return;

        EncounterRoutePreset route = found.Preset;
        route.Name = newName;
        string directory = found.Directory;
        string stem = PresetFile.Stem(newName);
        if (!string.Equals(Path.GetFileName(directory), stem, StringComparison.Ordinal))
        {
            string target = string.Equals(Path.GetFileName(directory), stem, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(SeedsDirectory, stem)
                : FreeDirectory(newName, folders);
            string step = directory + ".rename-" + Guid.NewGuid().ToString("N");
            Directory.Move(directory, step);
            Directory.Move(step, target);
            directory = target;
        }

        File.Delete(Path.Combine(directory, Path.GetFileName(found.File)));
        PresetFile.Write(RouteFile(directory), route);
    }

    public static EncounterRoutePreset? ReadImport(string path, out Dictionary<string, byte[]> pictures)
    {
        pictures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        EncounterRoutePreset? route = PresetFile.Read<EncounterRoutePreset>(path);
        if (route == null) return null;

        route.Normalize();
        if (route.Name.Length == 0) route.Name = Path.GetFileNameWithoutExtension(path);

        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        foreach (CaptureReference reference in route.References)
        {
            string picture = Path.Combine(directory, reference.Image);
            if (File.Exists(picture)) pictures[reference.Image] = File.ReadAllBytes(picture);
        }
        return route;
    }

    public EncounterRoutePreset InstallRoute(EncounterRoutePreset route, IReadOnlyDictionary<string, byte[]> pictures)
    {
        DeleteRoute(route.Name);
        WriteRoute(route, RouteFolders(), image => pictures.TryGetValue(image, out byte[]? bytes) ? bytes : null);
        return route;
    }

    public string ExportRoute(EncounterRoutePreset route, string parent)
    {
        string stem = PresetFile.Stem(route.Name);
        string target = Path.Combine(parent, stem);
        for (int n = 2; Directory.Exists(target) || File.Exists(target); n++)
        {
            target = Path.Combine(parent, $"{stem} ({n})");
        }
        Directory.CreateDirectory(target);

        EncounterRoutePreset copy = route.Clone();
        var kept = new List<CaptureReference>();
        foreach (CaptureReference reference in copy.References)
        {
            string? source = ReferencePath(route.Name, reference);
            if (source == null) continue;
            File.Copy(source, Path.Combine(target, reference.Image), overwrite: true);
            kept.Add(reference);
        }
        copy.References = kept;
        PresetFile.Write(Path.Combine(target, stem + ".json"), copy);
        return target;
    }

    public void Sync(IEnumerable<FilterPreset> filters, IEnumerable<EncounterRoutePreset> routes)
    {
        List<(string Path, FilterPreset Preset)> filterFiles = FilterFiles();
        foreach (FilterPreset filter in filters)
        {
            if (filter.Name.Length == 0) continue;
            string? path = filterFiles.FirstOrDefault(entry => FilterPreset.NameEquals(entry.Preset.Name, filter.Name)).Path;
            if (path == null)
            {
                path = FreeFile(filter.Name, filterFiles.Select(entry => entry.Path));
                filterFiles.Add((path, filter));
            }
            WriteIfChanged(path, filter);
        }

        List<(string Directory, string File, EncounterRoutePreset Preset)> folders = RouteFolders();
        foreach (EncounterRoutePreset route in routes)
        {
            if (route.Name.Length == 0) continue;
            WriteRoute(route, folders, pictures: null);
        }
    }

    private string WriteRoute(EncounterRoutePreset route,
        List<(string Directory, string File, EncounterRoutePreset Preset)> folders,
        Func<string, byte[]?>? pictures)
    {
        string? directory = folders.FirstOrDefault(entry => EncounterRoutePreset.NameEquals(entry.Preset.Name, route.Name)).Directory;
        string file;
        if (directory == null)
        {
            directory = FreeDirectory(route.Name, folders);
            file = RouteFile(directory);
            folders.Add((directory, file, route));
        }
        else
        {
            file = folders.First(entry => entry.Directory == directory).File;
        }
        Directory.CreateDirectory(directory);

        var kept = new List<CaptureReference>();
        var frames = new HashSet<int>();
        foreach (CaptureReference reference in route.References)
        {
            if (!frames.Add(reference.Frames)) continue;

            string name = CaptureReference.FileName(reference.Frames);
            string target = Path.Combine(directory, name);
            if (!string.Equals(reference.Image, name, StringComparison.OrdinalIgnoreCase) || !File.Exists(target))
            {
                string image = Path.GetFileName(reference.Image);
                byte[]? bytes = ReadIfThere(Path.Combine(directory, image))
                    ?? pictures?.Invoke(reference.Image)
                    ?? ReadIfThere(Path.Combine(LegacyReferenceDirectory, image));
                if (bytes == null) continue;
                File.WriteAllBytes(target, bytes);
            }
            kept.Add(new CaptureReference { Image = name, Frames = reference.Frames });
        }
        route.References = kept;

        WriteIfChanged(file, route);
        return directory;
    }

    private static byte[]? ReadIfThere(string path) =>
        path.Length > 0 && File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static void WriteIfChanged<T>(string path, T preset)
    {
        string text = JsonSerializer.Serialize(preset, SettingsStore.Options);
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private List<(string Path, FilterPreset Preset)> FilterFiles()
    {
        var found = new List<(string, FilterPreset)>();
        if (!Directory.Exists(FiltersDirectory)) return found;

        foreach (string path in Directory.EnumerateFiles(FiltersDirectory, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            FilterPreset? preset = TryRead<FilterPreset>(path);
            if (preset == null) continue;
            preset.Normalize();
            if (preset.Name.Length == 0) preset.Name = Path.GetFileNameWithoutExtension(path);
            found.Add((path, preset));
        }
        return found;
    }

    private List<(string Directory, string File, EncounterRoutePreset Preset)> RouteFolders()
    {
        var found = new List<(string, string, EncounterRoutePreset)>();
        if (!Directory.Exists(SeedsDirectory)) return found;

        foreach (string directory in Directory.EnumerateDirectories(SeedsDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            string named = RouteFile(directory);
            string? file = File.Exists(named)
                ? named
                : Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (file == null) continue;

            EncounterRoutePreset? preset = TryRead<EncounterRoutePreset>(file);
            if (preset == null) continue;
            preset.Normalize();
            if (preset.Name.Length == 0) preset.Name = Path.GetFileName(directory);
            found.Add((directory, file, preset));
        }
        return found;
    }

    private static T? TryRead<T>(string path) where T : class
    {
        try
        {
            return PresetFile.Read<T>(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string RouteFile(string directory) =>
        Path.Combine(directory, Path.GetFileName(directory) + ".json");

    private string FreeFile(string name, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        string stem = PresetFile.Stem(name);
        string path = Path.Combine(FiltersDirectory, stem + ".json");
        for (int n = 2; used.Contains(path) || File.Exists(path); n++)
        {
            path = Path.Combine(FiltersDirectory, $"{stem} ({n}).json");
        }
        return path;
    }

    private string FreeDirectory(string name, IEnumerable<(string Directory, string File, EncounterRoutePreset Preset)> taken)
    {
        var used = new HashSet<string>(taken.Select(entry => entry.Directory), StringComparer.OrdinalIgnoreCase);
        string stem = PresetFile.Stem(name);
        string path = Path.Combine(SeedsDirectory, stem);
        for (int n = 2; used.Contains(path) || Directory.Exists(path) || File.Exists(path); n++)
        {
            path = Path.Combine(SeedsDirectory, $"{stem} ({n})");
        }
        return path;
    }
}
