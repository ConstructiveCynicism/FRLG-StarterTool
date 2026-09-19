namespace FRLG.StarterTool.Core.Encounters;

public sealed record EncounterPath(
    string Name,
    int Rate,
    int Tiles,
    bool NewMap,
    int MinSteps,
    int RepelTiles,
    int? TargetEncounters,
    int Patches = 1)
{
    public bool IsPatchEntry(int tile)
    {
        if (Patches <= 0 || tile < 0 || tile >= Tiles) return false;
        if (tile == 0) return true;

        int open = Tiles - MinSteps;
        int into = tile - MinSteps;
        if (Patches == 1 || open <= 0 || into <= 0) return false;
        if (Patches >= open) return true;

        return (long)into * Patches / open != (long)(into - 1) * Patches / open;
    }

    public static int DefaultMinSteps(int rate) =>
        rate >= 80 ? 0 : rate < 10 ? 8 : 8 - rate / 10;

    public static EncounterPath Of(string name, int rate, int tiles, bool newMap = true,
        int repelTiles = 0, int? target = null) =>
        new(name, rate, tiles, newMap, DefaultMinSteps(rate), repelTiles, target);

    public static IReadOnlyList<EncounterPath> DefaultRoute { get; } = new[]
    {
        Of("R1a", 21, 22),
        Of("R1b", 21, 6),
        Of("R1c", 21, 22),
        Of("R2", 21, 5),
        Of("F1", 14, 42),
        Of("F2", 14, 10, newMap: false),
    };

    public static IReadOnlyList<EncounterPath> PlannerDefault { get; } = new[]
    {
        new EncounterPath("R1a", 21, 22, true, 6, 0, 0, 4),
        new EncounterPath("R1b", 21, 5, true, 6, 0, 0, 1),
        new EncounterPath("R1c", 21, 22, true, 6, 0, 1, 4),
        new EncounterPath("R2", 21, 5, true, 6, 0, 0, 1),
        new EncounterPath("F1", 14, 43, true, 7, 0, 0, 1),
        new EncounterPath("F2", 14, 9, true, 7, 0, 0, 1),
    };

    public const string PlannerRoute =
        "R1a,21,22,1,6,0,0,4\nR1b,21,5,1,6,0,0,1\nR1c,21,22,1,6,0,1,4\n" +
        "R2,21,5,1,6,0,0,1\nF1,14,43,1,7,0,0,1\nF2,14,9,1,7,0,0,1";
}
