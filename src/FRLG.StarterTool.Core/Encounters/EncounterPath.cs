namespace FRLG.StarterTool.Core.Encounters;

public sealed record EncounterPath(
    string Name,
    int Rate,
    int Tiles,
    bool NewMap,
    int MinSteps,
    int RepelTiles,
    int? TargetEncounters)
{
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
}
