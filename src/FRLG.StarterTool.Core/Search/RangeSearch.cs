using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Search;

public sealed class RangeSearchCriteria
{
    public RangeSearchCriteria(PredictorSearchCriteria filter, bool backup = false, int backupWithin = 2)
    {
        Filter = filter;
        Backup = backup;
        BackupWithin = backupWithin;
    }

    public PredictorSearchCriteria Filter { get; }

    public bool Backup { get; }

    public int BackupWithin { get; }
}

public static class RangeSearch
{
    public static List<PokemonRng> Search(
        IReadOnlyList<RangeSearchCriteria> ranges, CancellationToken cancellationToken = default)
    {
        var rows = new Dictionary<int, PokemonRng>();
        var winner = new Dictionary<int, int>();
        var nonBackup = new HashSet<int>();

        for (int index = 0; index < ranges.Count; index++)
        {
            RangeSearchCriteria range = ranges[index];
            foreach (PokemonRng pkm in PredictorSearch.Search(range.Filter, cancellationToken))
            {
                if (!rows.ContainsKey(pkm.Frame))
                {
                    rows[pkm.Frame] = pkm;
                    winner[pkm.Frame] = index;
                }

                if (!range.Backup) nonBackup.Add(pkm.Frame);
            }
        }

        var frames = new List<int>(rows.Keys);
        frames.Sort();

        var kept = new List<PokemonRng>(frames.Count);
        foreach (int frame in frames)
        {
            int index = winner[frame];
            if (!Listable(frame, index, ranges, nonBackup, frames)) continue;

            PokemonRng pkm = rows[frame];
            pkm.RangeIndex = index;
            kept.Add(pkm);
        }

        return kept;
    }

    public static AllSeedSearchResult AllSeeds(
        IReadOnlyList<RangeSearchCriteria> ranges, int limit = AllSeedSearch.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (ranges.Count == 1)
        {
            AllSeedSearchResult single = AllSeedSearch.Search(ranges[0].Filter, limit, cancellationToken);
            foreach (PokemonRng row in single.Matches) row.RangeIndex = 0;
            return single;
        }

        var rows = new SortedDictionary<int, SortedDictionary<int, PokemonRng>>();
        var winner = new Dictionary<(int Seed, int Frame), int>();
        var nonBackup = new HashSet<(int Seed, int Frame)>();
        int total = 0;
        bool truncated = false;

        for (int index = 0; index < ranges.Count; index++)
        {
            RangeSearchCriteria range = ranges[index];
            AllSeedSearchResult found = AllSeedSearch.Search(range.Filter, limit, cancellationToken);
            total += found.TotalMatches;
            truncated |= found.Truncated;

            foreach (PokemonRng pkm in found.Matches)
            {
                if (!rows.TryGetValue(pkm.Seed, out SortedDictionary<int, PokemonRng>? seedRows))
                {
                    seedRows = new SortedDictionary<int, PokemonRng>();
                    rows[pkm.Seed] = seedRows;
                }

                if (seedRows.TryAdd(pkm.Frame, pkm)) winner[(pkm.Seed, pkm.Frame)] = index;
                if (!range.Backup) nonBackup.Add((pkm.Seed, pkm.Frame));
            }
        }

        var merged = new List<PokemonRng>();
        foreach ((int seed, SortedDictionary<int, PokemonRng> seedRows) in rows)
        {
            var frames = new List<int>(seedRows.Keys);
            var seedNonBackup = new HashSet<int>();
            foreach (int frame in frames)
            {
                if (nonBackup.Contains((seed, frame))) seedNonBackup.Add(frame);
            }

            foreach (int frame in frames)
            {
                int index = winner[(seed, frame)];
                if (!Listable(frame, index, ranges, seedNonBackup, frames)) continue;

                PokemonRng pkm = seedRows[frame];
                pkm.RangeIndex = index;
                merged.Add(pkm);
            }
        }

        if (!truncated) total = merged.Count;
        if (merged.Count > limit) merged.RemoveRange(limit, merged.Count - limit);

        return new AllSeedSearchResult(merged, Math.Max(total, merged.Count));
    }

    public static double Odds(
        IReadOnlyList<RangeSearchCriteria> ranges, CancellationToken cancellationToken = default)
    {
        var counted = new List<PredictorSearchCriteria>();
        foreach (RangeSearchCriteria range in ranges)
        {
            if (!range.Backup) counted.Add(range.Filter);
        }
        if (counted.Count == 0)
        {
            foreach (RangeSearchCriteria range in ranges) counted.Add(range.Filter);
        }

        return SeedOdds.CalculateAny(counted, cancellationToken);
    }

    private static bool Listable(
        int frame, int index, IReadOnlyList<RangeSearchCriteria> ranges,
        HashSet<int> nonBackupFrames, List<int> frames)
    {
        if (index < 0 || index >= ranges.Count) return true;
        if (!ranges[index].Backup) return true;

        if (nonBackupFrames.Contains(frame)) return true;
        if (nonBackupFrames.Count == 0) return true;

        int within = ranges[index].BackupWithin;
        foreach (int other in frames)
        {
            if (other > frame + within) break;
            if (other < frame - within) continue;
            if (nonBackupFrames.Contains(other)) return true;
        }

        return false;
    }
}
