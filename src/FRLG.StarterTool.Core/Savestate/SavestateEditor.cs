using FRLG.StarterTool.Core.Pokemon;

namespace FRLG.StarterTool.Core.Savestate;

public sealed class SavestateEntry
{
    public SavestateEntry(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public string Path { get; }

    public string Name { get; }

    public PartyScan? Scan { get; set; }

    public IReadOnlyList<Gen3Mon> Targets { get; set; } = Array.Empty<Gen3Mon>();

    public string Status { get; set; } = "";

    public bool Editable => Targets.Count > 0;
}

public static class SavestateEditor
{
    public const string Extension = ".gqs";

    public static readonly int[] DefaultTargets = { 7, 8, 9, 150 };

    public static List<SavestateEntry> Scan(string folder, IReadOnlyCollection<int> targetSpecies)
    {
        var entries = new List<SavestateEntry>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return entries;

        foreach (string path in Directory.EnumerateFiles(folder, "*" + Extension).OrderBy(NaturalKey, StringComparer.Ordinal))
        {
            var entry = new SavestateEntry(path);
            try
            {
                GseSavestate state = GseSavestate.Load(path);
                entry.Scan = PartyLocator.Find(state.Ewram);
                entry.Targets = entry.Scan.Party.Where(mon => targetSpecies.Contains(mon.Species)).ToList();
                entry.Status = Describe(entry);
            }
            catch (Exception ex)
            {
                entry.Status = ex is InvalidDataException ? "unreadable" : "cannot open";
                _ = ex;
            }

            entries.Add(entry);
        }

        return entries;
    }

    public static string Apply(SavestateEntry entry, string saveFolder, MonEdit edit, Random random)
    {
        GseSavestate state = GseSavestate.Load(entry.Path);
        PartyScan scan = PartyLocator.Find(state.Ewram);

        var edited = new List<string>();
        foreach (Gen3Mon mon in scan.Party)
        {
            if (!edit.TargetSpecies.Contains(mon.Species)) continue;

            edit.Apply(mon, random);
            mon.WriteTo(state.Ewram);
            edited.Add($"{PokemonSpecies.Get(mon.Species).Name} {mon.Nature.Name}");
        }

        if (edited.Count == 0) return "no target";

        string destination = Path.Combine(saveFolder, Path.GetFileName(entry.Path));
        state.Save(destination);
        return "wrote " + string.Join(", ", edited);
    }

    private static string Describe(SavestateEntry entry)
    {
        if (entry.Scan is not { Found: true }) return "no party found";
        if (entry.Targets.Count == 0)
        {
            return entry.Scan.Party.Count == 0
                ? "empty party"
                : "no target (" + string.Join("/", entry.Scan.Party.Select(NameOf)) + ")";
        }

        return string.Join(", ", entry.Targets.Select(mon =>
            $"{NameOf(mon)} Lv{mon.Level} {mon.Nature.Name} {string.Join("/", mon.Ivs)}"));
    }

    private static string NameOf(Gen3Mon mon) =>
        mon.Species >= 1 ? PokemonSpecies.Get(mon.Species).Name : "?";

    private static string NaturalKey(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        var key = new System.Text.StringBuilder(name.Length + 8);
        for (int at = 0; at < name.Length;)
        {
            if (!char.IsDigit(name[at]))
            {
                key.Append(char.ToUpperInvariant(name[at]));
                at++;
                continue;
            }

            int start = at;
            while (at < name.Length && char.IsDigit(name[at])) at++;
            key.Append(name.AsSpan(start, at - start).ToString().TrimStart('0').PadLeft(10, '0'));
        }
        return key.ToString();
    }
}
