using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Search;

public sealed class AllSeedSearchResult
{
    public AllSeedSearchResult(List<PokemonRng> matches, int totalMatches)
    {
        Matches = matches;
        TotalMatches = totalMatches;
    }

    public List<PokemonRng> Matches { get; }

    public int TotalMatches { get; }

    public bool Truncated => TotalMatches > Matches.Count;
}

public static class AllSeedSearch
{
    public const int DefaultLimit = 20000;

    public static AllSeedSearchResult Search(PredictorSearchCriteria criteria, int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        SeedOdds.BuildTables(criteria, out bool[] allowed, out int[] thresholds);

        int minFrame = Math.Max(0, criteria.MinFrame);
        int maxFrame = criteria.MaxFrame;
        if (maxFrame < minFrame)
        {
            return new AllSeedSearchResult(new List<PokemonRng>(), 0);
        }

        var counts = new int[SeedOdds.SeedCount];
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.For(0, SeedOdds.SeedCount, options, seed =>
        {
            int found = 0;
            foreach (int _ in Matches(seed, minFrame, maxFrame, allowed, thresholds, cancellationToken))
            {
                found++;
            }
            counts[seed] = found;
        });

        long total = 0;
        foreach (int count in counts) total += count;

        var matches = new List<PokemonRng>(Math.Min(limit, (int)Math.Min(total, int.MaxValue)));
        for (int seed = 0; seed < SeedOdds.SeedCount && matches.Count < limit; seed++)
        {
            if (counts[seed] == 0) continue;

            foreach (int frame in Matches(seed, minFrame, maxFrame, allowed, thresholds, cancellationToken))
            {
                matches.Add(new PokemonMethod1(new Seed(seed), frame));
                if (matches.Count >= limit) break;
            }
        }

        return new AllSeedSearchResult(matches, (int)Math.Min(total, int.MaxValue));
    }

    private static IEnumerable<int> Matches(int seed, int minFrame, int maxFrame, bool[] allowed,
        int[] thresholds, CancellationToken cancellationToken)
    {
        var rng = new Gen3Rng(seed);
        rng.Advance(minFrame);

        int v0 = rng.Value;
        int v1 = Next(v0);
        int v2 = Next(v1);
        int v3 = Next(v2);

        for (int frame = minFrame; frame <= maxFrame; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    yield return frame;
                }
            }

            v0 = v1;
            v1 = v2;
            v2 = v3;
            v3 = Next(v3);
        }
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
