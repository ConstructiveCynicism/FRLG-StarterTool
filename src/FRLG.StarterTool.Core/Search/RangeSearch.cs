using FRLG.StarterTool.Core.Pokemon;
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
        => Search(ranges, null, null, cancellationToken);

    public static List<PokemonRng> Search(
        IReadOnlyList<RangeSearchCriteria> ranges, int? fromFrame, Action? onlyFrom,
        CancellationToken cancellationToken = default)
    {
        var kept = new List<PokemonRng>();
        if (ranges.Count == 0) return kept;

        int count = ranges.Count;
        var walkers = new Walker[count];
        int windowMin = int.MaxValue;
        int windowMax = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            walkers[i] = new Walker(ranges[i].Filter);
            if (walkers[i].MaxFrame < walkers[i].MinFrame) continue;
            windowMin = Math.Min(windowMin, walkers[i].MinFrame);
            windowMax = Math.Max(windowMax, walkers[i].MaxFrame);
        }
        if (windowMax < windowMin) return kept;

        var rows = new List<(int Frame, int Seed, int Winner)>();
        var nonBackup = new HashSet<int>();
        var frames = new List<int>();

        bool decided = fromFrame is null || onlyFrom is null;
        int from = fromFrame ?? 0;
        var pending = new List<(int Frame, int Within)>();

        for (int frame = windowMin; frame <= windowMax; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int winner = -1;
            bool target = false;
            int seed = 0;
            for (int i = 0; i < count; i++)
            {
                if (!walkers[i].Accepts(frame)) continue;

                if (winner < 0)
                {
                    winner = i;
                    seed = walkers[i].Seed;
                }
                if (!ranges[i].Backup)
                {
                    target = true;
                    break;
                }
            }
            if (winner < 0) continue;

            rows.Add((frame, seed, winner));
            frames.Add(frame);
            if (target) nonBackup.Add(frame);

            if (decided) continue;

            if (target)
            {
                decided = true;
                if (frame < from) continue;

                bool listsAnEarlierBackup = false;
                foreach ((int backupFrame, int within) in pending)
                {
                    if (frame - backupFrame <= within)
                    {
                        listsAnEarlierBackup = true;
                        break;
                    }
                }
                if (!listsAnEarlierBackup) onlyFrom!();
            }
            else if (frame < from)
            {
                pending.Add((frame, ranges[winner].BackupWithin));
            }
            else if (pending.Count == 0)
            {
                decided = true;
                onlyFrom!();
            }
        }

        foreach ((int frame, int seed, int winner) in rows)
        {
            if (!Listable(frame, winner, ranges, nonBackup, frames)) continue;

            var pkm = new PokemonMethod1(new Seed(seed), frame) { RangeIndex = winner };
            kept.Add(pkm);
        }

        return kept;
    }

    private struct Walker
    {
        private readonly bool[] _allowed;
        private readonly int[] _thresholds;
        private int _v0, _v1, _v2, _v3;
        private int _frame;

        public Walker(PredictorSearchCriteria filter)
        {
            Seed = filter.Seed;
            MinFrame = Math.Max(0, filter.MinFrame);
            MaxFrame = filter.MaxFrame;
            SeedOdds.BuildTables(filter, out _allowed, out _thresholds);

            var rng = new Gen3Rng(filter.Seed);
            rng.Advance(MinFrame);
            _v0 = rng.Value;
            _v1 = Next(_v0);
            _v2 = Next(_v1);
            _v3 = Next(_v2);
            _frame = MinFrame;
        }

        public int Seed { get; }
        public int MinFrame { get; }
        public int MaxFrame { get; }

        public bool Accepts(int frame)
        {
            if (frame < MinFrame || frame > MaxFrame) return false;

            while (_frame < frame)
            {
                _v0 = _v1;
                _v1 = _v2;
                _v2 = _v3;
                _v3 = Next(_v3);
                _frame++;
            }

            long pid = ((long)Top(_v1) << 16) + Top(_v0);
            int nature = (int)(pid % Nature.NatureCount);
            if (!_allowed[nature]) return false;

            int top = Top(_v2);
            int hp = top % 32;
            int atk = top / 32 % 32;
            int def = top / 1024 % 32;

            top = Top(_v3);
            int spe = top % 32;
            int spa = top / 32 % 32;
            int spd = top / 1024 % 32;

            int b = nature * 6;
            return _thresholds[b] <= hp
                   && _thresholds[b + 1] <= atk
                   && _thresholds[b + 2] <= def
                   && _thresholds[b + 3] <= spa
                   && _thresholds[b + 4] <= spd
                   && _thresholds[b + 5] <= spe;
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
