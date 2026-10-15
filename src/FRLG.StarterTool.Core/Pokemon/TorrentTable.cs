namespace FRLG.StarterTool.Core.Pokemon;

public sealed record TorrentRow(int Level, PokemonSpecies Species, int HpEvs, int MaxHp, int Gain, int TorrentHp);

public enum TorrentRoute
{
    Glitchless,

    Round2
}

public enum MoonOptional
{
    Josh,
    Robby,
    Rocket,

    Hiker
}

public static class TorrentTable
{
    private static readonly (int Level, int Evs)[] GlitchlessEvs =
    {
        (15, 3), (16, 5), (23, 6), (24, 7), (25, 8), (35, 13), (38, 15), (40, 16), (49, 26), (52, 29), (53, 31)
    };

    private static readonly (int Level, int Evs)[] GlitchlessRobbyEvs =
    {
        (15, 3), (16, 5), (22, 7), (23, 8), (24, 9), (25, 10), (35, 15), (38, 17), (40, 18), (49, 28), (52, 31), (53, 33)
    };

    private static readonly (int Level, int Evs)[] Round2Evs =
    {
        (14, 2), (15, 3), (16, 4), (17, 5), (23, 6), (24, 7), (25, 8), (28, 9), (29, 11), (30, 13),
        (37, 15), (41, 16), (43, 18), (44, 19), (46, 24), (49, 32), (52, 34), (59, 35)
    };

    private static readonly (int Level, int Evs)[] Round2RobbyEvs =
    {
        (14, 2), (15, 3), (16, 6), (17, 7), (23, 8), (24, 9), (25, 10), (28, 11), (29, 13), (30, 15),
        (37, 17), (41, 18), (43, 20), (44, 21), (46, 26), (49, 34), (52, 36), (59, 37)
    };

    public static int HpEvsAt(TorrentRoute route, MoonOptional optional, int level)
    {
        bool robby = optional == MoonOptional.Robby;
        (int Level, int Evs)[] table = route == TorrentRoute.Round2
            ? robby ? Round2RobbyEvs : Round2Evs
            : robby ? GlitchlessRobbyEvs : GlitchlessEvs;

        int evs = 0;
        foreach ((int at, int value) in table)
        {
            if (at > level) break;
            evs = value;
        }

        return evs;
    }

    public static int MaxHp(int baseHp, int iv, int ev, int level) =>
        (2 * baseHp + iv + ev / 4) * level / 100 + level + 10;

    public static int Threshold(int maxHp) => maxHp / 3;

    public static PokemonSpecies SpeciesAt(PokemonSpecies species, int level)
    {
        int id = species.Id;
        if (id == 7 && level >= 16) id = 8;
        if (id == 8 && level >= 36) id = 9;
        return id == species.Id ? species : PokemonSpecies.Get(id);
    }

    public static IReadOnlyList<TorrentRow> Build(
        PokemonSpecies species, int hpIv, Func<int, int> hpEvsAt, int level, int count)
    {
        level = Math.Clamp(level, 1, 100);
        hpIv = Math.Clamp(hpIv, 0, 31);

        int now = MaxHp(SpeciesAt(species, level).BaseStats[0], hpIv, hpEvsAt(level), level);
        var rows = new List<TorrentRow>(count);
        for (int at = level; at <= 100 && rows.Count < count; at++)
        {
            PokemonSpecies then = SpeciesAt(species, at);
            int evs = hpEvsAt(at);
            int max = MaxHp(then.BaseStats[0], hpIv, evs, at);
            int gain = max - now;
            rows.Add(new TorrentRow(at, then, evs, max, gain, Threshold(max) - gain));
        }

        return rows;
    }
}
