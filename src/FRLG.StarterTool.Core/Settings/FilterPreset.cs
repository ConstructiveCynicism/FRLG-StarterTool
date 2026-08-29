using FRLG.StarterTool.Core.Pokemon;

namespace FRLG.StarterTool.Core.Settings;

public sealed class FilterPreset
{
    public string Name { get; set; } = "";

    public int SpeciesId { get; set; } = SettingsArrays.DefaultSpeciesId;

    public string MinFrame { get; set; } = "0";
    public string MaxFrame { get; set; } = "10000";

    public bool[] Natures { get; set; } = SettingsArrays.NewNatureFilter();

    public int[] IvMinus { get; set; } = new int[6];
    public int[] IvNeutral { get; set; } = new int[6];
    public int[] IvPlus { get; set; } = new int[6];

    public List<ConstraintRange> Ranges { get; set; } = new();

    public FilterPreset Normalize()
    {
        Name = (Name ?? "").Trim();
        if (SpeciesId < 1 || SpeciesId > PokemonSpecies.Gen3DexSize) SpeciesId = SettingsArrays.DefaultSpeciesId;
        MinFrame ??= "0";
        MaxFrame ??= "10000";

        Natures = SettingsArrays.Resize(Natures, Nature.NatureCount);
        IvMinus = SettingsArrays.Resize(IvMinus, 6);
        IvNeutral = SettingsArrays.Resize(IvNeutral, 6);
        IvPlus = SettingsArrays.Resize(IvPlus, 6);

        Ranges = SettingsArrays.Repair(Ranges, Natures, IvMinus, IvNeutral, IvPlus);
        (Natures, IvMinus, IvNeutral, IvPlus) = SettingsArrays.MirrorPrimary(Primary);

        return this;
    }

    public ConstraintRange Primary =>
        Ranges.FirstOrDefault(range => !range.Backup) ?? Ranges.FirstOrDefault() ?? new ConstraintRange();

    public FilterPreset Clone(string? name = null) => new()
    {
        Name = name ?? Name,
        SpeciesId = SpeciesId,
        MinFrame = MinFrame,
        MaxFrame = MaxFrame,
        Natures = (bool[])Natures.Clone(),
        IvMinus = (int[])IvMinus.Clone(),
        IvNeutral = (int[])IvNeutral.Clone(),
        IvPlus = (int[])IvPlus.Clone(),
        Ranges = Ranges.Select(range => range.Clone()).ToList()
    };

    public bool SameFilterAs(FilterPreset other) =>
        other != null
        && SpeciesId == other.SpeciesId
        && FrameEquals(MinFrame, other.MinFrame)
        && FrameEquals(MaxFrame, other.MaxFrame)
        && SameRangesAs(other);

    private bool SameRangesAs(FilterPreset other)
    {
        if (Ranges.Count != other.Ranges.Count) return false;

        for (int i = 0; i < Ranges.Count; i++)
        {
            if (!NameEquals(Ranges[i].Name, other.Ranges[i].Name)) return false;
            if (!Ranges[i].SameRangeAs(other.Ranges[i])) return false;
        }

        return true;
    }

    private static bool FrameEquals(string a, string b) =>
        (a ?? "").Trim().TrimStart('0') == (b ?? "").Trim().TrimStart('0');

    public static bool NameEquals(string a, string b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}

internal static class SettingsArrays
{
    public const int DefaultSpeciesId = 7;

    public static bool[] NewNatureFilter()
    {
        var natures = new bool[Nature.NatureCount];
        Array.Fill(natures, true);
        return natures;
    }

    public static bool[] Resize(bool[]? source, int length)
    {
        if (source != null && source.Length == length) return source;

        var resized = new bool[length];
        Array.Fill(resized, true);
        if (source != null)
        {
            Array.Copy(source, resized, Math.Min(source.Length, length));
        }
        return resized;
    }

    public static List<ConstraintRange> Repair(
        List<ConstraintRange>? ranges, bool[] natures, int[] minus, int[] neutral, int[] plus)
    {
        var repaired = new List<ConstraintRange>();
        foreach (ConstraintRange range in ranges ?? new List<ConstraintRange>())
        {
            if (range != null) repaired.Add(range.Normalize());
        }

        if (repaired.Count == 0)
        {
            repaired.Add(new ConstraintRange
            {
                Name = DefaultRangeName,
                Color = ConstraintRange.Screen,
                Natures = (bool[])natures.Clone(),
                IvMinus = (int[])minus.Clone(),
                IvNeutral = (int[])neutral.Clone(),
                IvPlus = (int[])plus.Clone()
            }.Normalize());
        }

        return repaired;
    }

    public const string DefaultRangeName = "Target";

    public static (bool[] Natures, int[] Minus, int[] Neutral, int[] Plus) MirrorPrimary(ConstraintRange primary) =>
        ((bool[])primary.Natures.Clone(),
            (int[])primary.IvMinus.Clone(),
            (int[])primary.IvNeutral.Clone(),
            (int[])primary.IvPlus.Clone());

    public static int[] Resize(int[]? source, int length)
    {
        if (source != null && source.Length == length) return source;

        var resized = new int[length];
        if (source != null)
        {
            Array.Copy(source, resized, Math.Min(source.Length, length));
        }
        return resized;
    }
}
