using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Search;

public static class SeedOdds
{
    public const int SeedCount = 65536;

    public static double Calculate(PredictorSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        BuildTables(criteria, out bool[] allowed, out int[] thresholds);

        int minFrame = Math.Max(0, criteria.MinFrame);
        int maxFrame = criteria.MaxFrame;
        if (maxFrame < minFrame)
        {
            return 0.0;
        }

        int matches = 0;
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.For(0, SeedCount, options, () => 0, (seed, _, local) =>
        {
            if (HasMatch(seed, minFrame, maxFrame, allowed, thresholds))
            {
                local++;
            }
            return local;
        }, local => Interlocked.Add(ref matches, local));

        return (double)matches / SeedCount;
    }

    public static double CalculateAny(
        IReadOnlyList<PredictorSearchCriteria> filters, CancellationToken cancellationToken = default)
    {
        if (filters.Count == 0) return 0.0;
        if (filters.Count == 1) return Calculate(filters[0], cancellationToken);

        var allowed = new bool[filters.Count][];
        var thresholds = new int[filters.Count][];
        var minFrames = new int[filters.Count];
        var maxFrames = new int[filters.Count];
        for (int i = 0; i < filters.Count; i++)
        {
            BuildTables(filters[i], out allowed[i], out thresholds[i]);
            minFrames[i] = Math.Max(0, filters[i].MinFrame);
            maxFrames[i] = filters[i].MaxFrame;
        }

        int matches = 0;
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.For(0, SeedCount, options, () => 0, (seed, _, local) =>
        {
            for (int i = 0; i < filters.Count; i++)
            {
                if (maxFrames[i] < minFrames[i]) continue;
                if (!HasMatch(seed, minFrames[i], maxFrames[i], allowed[i], thresholds[i])) continue;

                local++;
                break;
            }
            return local;
        }, local => Interlocked.Add(ref matches, local));

        return (double)matches / SeedCount;
    }

    public static bool HasMatch(int seed, PredictorSearchCriteria criteria)
    {
        BuildTables(criteria, out bool[] allowed, out int[] thresholds);
        return HasMatch(seed, Math.Max(0, criteria.MinFrame), criteria.MaxFrame, allowed, thresholds);
    }

    internal static void BuildTables(PredictorSearchCriteria criteria, out bool[] allowed, out int[] thresholds)
    {
        allowed = new bool[Nature.NatureCount];
        thresholds = new int[Nature.NatureCount * 6];

        for (int id = 0; id < Nature.NatureCount; id++)
        {
            allowed[id] = criteria.Natures == null || criteria.Natures[id];

            var nature = new Nature(id);
            StatPack pack = criteria.Neutral;
            for (int stat = 1; stat < 6; stat++)
            {
                double boost = nature.GetNatureBoost(stat);
                if (boost > 1.01) pack.SetStat(stat, criteria.Plus.GetStat(stat));
                if (boost < 0.99) pack.SetStat(stat, criteria.Minus.GetStat(stat));
            }

            for (int stat = 0; stat < 6; stat++)
            {
                thresholds[id * 6 + stat] = pack.GetStat(stat);
            }
        }
    }

    private static bool HasMatch(int seed, int minFrame, int maxFrame, bool[] allowed, int[] thresholds)
    {
        var rng = new Gen3Rng(seed);
        rng.Advance(minFrame);

        int v0 = rng.Value;
        int v1 = Next(v0);
        int v2 = Next(v1);
        int v3 = Next(v2);

        for (int frame = minFrame; frame <= maxFrame; frame++)
        {
            long pid = ((long)Top(v1) << 16) + Top(v0);
            int nature = (int)(pid % Nature.NatureCount);

            if (allowed[nature])
            {
                int top = Top(v2);
                int hp = top % 32;
                int atk = top / 32 % 32;
                int def = top / 1024 % 32;

                top = Top(v3);
                int spe = top % 32;
                int spa = top / 32 % 32;
                int spd = top / 1024 % 32;

                int b = nature * 6;
                if (thresholds[b] <= hp
                    && thresholds[b + 1] <= atk
                    && thresholds[b + 2] <= def
                    && thresholds[b + 3] <= spa
                    && thresholds[b + 4] <= spd
                    && thresholds[b + 5] <= spe)
                {
                    return true;
                }
            }

            v0 = v1;
            v1 = v2;
            v2 = v3;
            v3 = Next(v3);
        }

        return false;
    }

    private static int Next(int value)
    {
        unchecked
        {
            return value * 1103515245 + 24691;
        }
    }

    private static int Top(int value) => (value >> 16) & 0xFFFF;
}
