namespace FRLG.StarterTool.Core.Encounters;

public sealed class EncounterMatch
{
    public EncounterMatch(PressFrame press, int wildSeed, IReadOnlyList<int> pathCounts, double rate)
    {
        Press = press;
        WildSeed = wildSeed;
        PathCounts = pathCounts;
        Rate = rate;
    }

    public PressFrame Press { get; }

    public int WildSeed { get; }

    public IReadOnlyList<int> PathCounts { get; }

    public double Rate { get; }

    public bool EitherSound { get; internal set; }

    public int Total
    {
        get
        {
            int total = 0;
            foreach (int count in PathCounts) total += count;
            return total;
        }
    }
}

public readonly record struct EncounterOutcome(int WildSeed, IReadOnlyList<int> PathCounts, double Rate,
    IReadOnlyList<int> ModePathCounts, double ModeRate)
{
    public int Total => Sum(PathCounts);

    public int ModeTotal => Sum(ModePathCounts);

    private static int Sum(IReadOnlyList<int> counts)
    {
        int total = 0;
        foreach (int count in counts) total += count;
        return total;
    }
}

public readonly record struct EncounterTileShare(int Tile, double Share);

public sealed record EncounterPathTiles(int ModeCount, double ModeShare, IReadOnlyList<EncounterTileShare> Tiles);

public sealed class EncounterSearchResult
{
    public EncounterSearchResult(List<EncounterMatch> matches, int totalMatches, int seedsMatched)
    {
        Matches = matches;
        TotalMatches = totalMatches;
        SeedsMatched = seedsMatched;
    }

    public List<EncounterMatch> Matches { get; }

    public int TotalMatches { get; }

    public int SeedsMatched { get; }

    public bool Truncated => TotalMatches > Matches.Count;
}

public static class EncounterSearch
{
    public const int DefaultSamples = 32;

    public const int ScanSamples = 1 << 16;

    public const int DefaultLimit = 500;

    public static EncounterSearchResult Search(IReadOnlyList<EncounterPath> route,
        int samples = DefaultSamples, int limit = DefaultLimit,
        int cycles = TitleSeedTable.CycleOffset,
        TitleProtocol protocol = TitleProtocol.Sweep,
        TitleVariant variant = default,
        CancellationToken cancellationToken = default,
        IReadOnlyList<TitleVariant>? variants = null,
        int maxResetFrame = 0)
    {
        IReadOnlyList<TitleVariant> asked = variants is { Count: > 0 } ? variants : new[] { variant };
        if (route.Count == 0)
        {
            return new EncounterSearchResult(new List<EncounterMatch>(), 0, 0);
        }

        int lanes = EncounterModel.WildSeedCount;

        var hits = new int[lanes];
        var bestSample = new int[lanes];
        var bestShape = new byte[lanes * route.Count];
        Array.Fill(bestSample, int.MaxValue);
        var merge = new object();

        uint[] streams = MainStreams(samples);

        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.For(0, streams.Length, options,
            () => new Tally(lanes, route.Count),
            (index, _, tally) =>
            {
                EncounterModel.CountAll(streams[index], route, tally.Counts);

                for (int seed = 0; seed < lanes; seed++)
                {
                    bool matched = true;
                    for (int path = 0; path < route.Count && matched; path++)
                    {
                        int? target = route[path].TargetEncounters;
                        if (target is not null && tally.Counts[path * lanes + seed] != target) matched = false;
                    }
                    if (!matched) continue;

                    tally.Hits[seed]++;
                    if (index >= tally.Sample[seed]) continue;

                    tally.Sample[seed] = index;
                    for (int path = 0; path < route.Count; path++)
                    {
                        tally.Shape[path * lanes + seed] = tally.Counts[path * lanes + seed];
                    }
                }

                return tally;
            },
            tally =>
            {
                lock (merge)
                {
                    for (int seed = 0; seed < lanes; seed++)
                    {
                        hits[seed] += tally.Hits[seed];
                        if (tally.Sample[seed] >= bestSample[seed]) continue;

                        bestSample[seed] = tally.Sample[seed];
                        for (int path = 0; path < route.Count; path++)
                        {
                            bestShape[path * lanes + seed] = tally.Shape[path * lanes + seed];
                        }
                    }
                }
            });

        int seedsMatched = 0;
        for (int seed = 0; seed < lanes; seed++)
        {
            if (hits[seed] > 0) seedsMatched++;
        }
        if (seedsMatched == 0)
        {
            return new EncounterSearchResult(new List<EncounterMatch>(), 0, 0);
        }

        var found = new List<EncounterMatch>();
        var shapes = new Dictionary<int, int[]>();
        void Look(int titleSeed, TitleVariant one)
        {
            int wildSeed = EncounterModel.WildSeedOf(titleSeed);
            if (hits[wildSeed] == 0) return;

            PressFrame? press = TitleSeedTable.Find(titleSeed, cycles, protocol, one, maxResetFrame);
            if (press is null) return;

            if (!shapes.TryGetValue(wildSeed, out int[]? shape))
            {
                shape = new int[route.Count];
                for (int path = 0; path < route.Count; path++) shape[path] = bestShape[path * lanes + wildSeed];
                shapes[wildSeed] = shape;
            }
            found.Add(new EncounterMatch(press.Value, wildSeed, shape, (double)hits[wildSeed] / streams.Length));
        }

        if (protocol == TitleProtocol.Rta)
        {
            foreach (TitleVariant one in asked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (int titleSeed in TitleSeedTable.ReachableSeeds(one, cycles)) Look(titleSeed, one);
            }
        }
        else
        {
            for (int titleSeed = 0; titleSeed < lanes; titleSeed++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (TitleVariant one in asked) Look(titleSeed, one);
            }
        }

        found.Sort(Cheapest);

        var bySound = new Dictionary<(TitleVariant, int, int, int, int), EncounterMatch>();
        found.RemoveAll(match =>
        {
            PressFrame press = match.Press;
            var key = (press.Variant with { Sound = TitleSoundMode.Mono }, press.Offset, press.Pass, press.Seed, press.Window);
            if (!bySound.TryGetValue(key, out EncounterMatch? first))
            {
                bySound[key] = match;
                return false;
            }
            if (first.Press.Variant.Sound == press.Variant.Sound) return false;
            first.EitherSound = true;
            return true;
        });

        var byFrame = new Dictionary<(TitleGame, TitleSaves, TitleButtonMode, int, int, int), int>();
        var keep = new List<EncounterMatch>(found.Count);
        foreach (EncounterMatch match in found)
        {
            PressFrame press = match.Press;
            var key = (press.Variant.Game, press.Variant.Saves, press.Variant.Buttons, press.ResetFrame, press.Pass, press.Seed);
            if (!byFrame.TryGetValue(key, out int at))
            {
                byFrame[key] = keep.Count;
                keep.Add(match);
                continue;
            }
            if (Simpler(match, keep[at])) keep[at] = match;
        }
        found = keep;

        int total = found.Count;
        if (found.Count > limit) found.RemoveRange(limit, found.Count - limit);

        return new EncounterSearchResult(found, total, seedsMatched);
    }

    public static EncounterOutcome Evaluate(IReadOnlyList<EncounterPath> route, int titleSeed, int samples = DefaultSamples)
    {
        int wildSeed = EncounterModel.WildSeedOf(titleSeed);
        var shape = new int[route.Count];
        if (route.Count == 0) return new EncounterOutcome(wildSeed, shape, 0.0, shape, 0.0);

        uint[] streams = MainStreams(samples);
        int hits = 0;
        bool shaped = false;
        var counts = new int[route.Count];
        var seen = new Dictionary<string, (int[] Shape, int Streams)>();
        for (int index = 0; index < streams.Length; index++)
        {
            Array.Clear(counts);
            foreach ((int path, _) in EncounterModel.Simulate((uint)wildSeed, streams[index], route)) counts[path]++;

            bool matched = true;
            for (int path = 0; path < route.Count && matched; path++)
            {
                int? target = route[path].TargetEncounters;
                if (target is not null && counts[path] != target) matched = false;
            }
            if (matched) hits++;
            if (index == 0 || (matched && !shaped))
            {
                Array.Copy(counts, shape, route.Count);
                shaped = matched;
            }

            string key = string.Join(',', counts);
            seen[key] = seen.TryGetValue(key, out var tally)
                ? (tally.Shape, tally.Streams + 1)
                : ((int[])counts.Clone(), 1);
        }

        (int[] Shape, int Streams) mode = default;
        foreach (var tally in seen.Values)
        {
            if (mode.Shape is null || tally.Streams > mode.Streams
                || (tally.Streams == mode.Streams && tally.Shape.Sum() < mode.Shape.Sum()))
            {
                mode = tally;
            }
        }
        return new EncounterOutcome(wildSeed, shape, (double)hits / streams.Length,
            mode.Shape ?? shape, (double)mode.Streams / streams.Length);
    }

    private static bool Simpler(EncounterMatch left, EncounterMatch right)
    {
        int order = Narrowest(right.Press).CompareTo(Narrowest(left.Press));
        if (order == 0) order = right.Press.Window.CompareTo(left.Press.Window);
        if (order == 0) order = Inputs(left.Press).CompareTo(Inputs(right.Press));
        if (order == 0) order = string.CompareOrdinal(left.Press.Variant.Name, right.Press.Variant.Name);
        return order < 0;
    }

    private static int Narrowest(PressFrame press)
    {
        int narrowest = Math.Max(press.Window, 1);
        if (TitleRecipes.Find(press.Variant) is TitleRecipe recipe)
        {
            foreach (int window in recipe.Windows) narrowest = Math.Min(narrowest, window);
        }
        else if (press.Variant.IntroSkipped)
        {
            narrowest = Math.Min(narrowest, Math.Max(press.IntroWindow, 1));
        }
        return narrowest;
    }

    private static int Inputs(PressFrame press)
    {
        if (TitleRecipes.Find(press.Variant) is TitleRecipe recipe)
        {
            return recipe.Steps.Count + recipe.EntrySteps(press.Offset < TitleSeedTable.AnimationEndsOf(press.Variant.Game)).Count;
        }
        int inputs = (press.Variant.IntroSkipped ? 1 : 0) + (press.Variant.LoopSkipped ? 1 : 0);
        return inputs + TitleCombos.Of(press).Count;
    }

    private static int Cheapest(EncounterMatch left, EncounterMatch right)
    {
        int order = right.Press.Measured.CompareTo(left.Press.Measured);
        if (order != 0) return order;

        order = left.Press.Pass.CompareTo(right.Press.Pass);
        if (order != 0) return order;

        order = right.Press.Window.CompareTo(left.Press.Window);
        if (order != 0) return order;

        order = right.Rate.CompareTo(left.Rate);
        if (order != 0) return order;

        order = left.Total.CompareTo(right.Total);
        if (order != 0) return order;

        order = left.Press.Offset.CompareTo(right.Press.Offset);
        if (order != 0) return order;

        order = left.Press.Variant.Intro.CompareTo(right.Press.Variant.Intro);
        if (order != 0) return order;

        order = left.Press.Variant.Sound.CompareTo(right.Press.Variant.Sound);
        if (order != 0) return order;

        order = left.Press.Variant.Buttons.CompareTo(right.Press.Variant.Buttons);
        if (order != 0) return order;

        order = left.Press.Variant.Loop.CompareTo(right.Press.Variant.Loop);
        if (order != 0) return order;

        order = left.Press.Variant.Saves.CompareTo(right.Press.Variant.Saves);
        if (order != 0) return order;

        order = string.CompareOrdinal(left.Press.Variant.ComboKey, right.Press.Variant.ComboKey);
        if (order != 0) return order;

        return left.Press.Seed.CompareTo(right.Press.Seed);
    }

    public static IReadOnlyList<EncounterPathTiles> TilesOf(IReadOnlyList<EncounterPath> route, int titleSeed, int samples = DefaultSamples)
    {
        var result = new List<EncounterPathTiles>(route.Count);
        if (route.Count == 0) return result;

        int wildSeed = EncounterModel.WildSeedOf(titleSeed);
        uint[] streams = MainStreams(samples);
        var tiles = new SortedDictionary<int, int>[route.Count];
        var totals = new Dictionary<int, int>[route.Count];
        for (int path = 0; path < route.Count; path++)
        {
            tiles[path] = new SortedDictionary<int, int>();
            totals[path] = new Dictionary<int, int>();
        }

        var counts = new int[route.Count];
        foreach (uint stream in streams)
        {
            Array.Clear(counts);
            foreach ((int path, int tile) in EncounterModel.Simulate((uint)wildSeed, stream, route))
            {
                counts[path]++;
                tiles[path][tile] = tiles[path].TryGetValue(tile, out int seen) ? seen + 1 : 1;
            }
            for (int path = 0; path < route.Count; path++)
            {
                totals[path][counts[path]] = totals[path].TryGetValue(counts[path], out int seen) ? seen + 1 : 1;
            }
        }

        for (int path = 0; path < route.Count; path++)
        {
            int modeCount = 0, modeStreams = -1;
            foreach ((int count, int seen) in totals[path])
            {
                if (seen > modeStreams || (seen == modeStreams && count < modeCount))
                {
                    modeCount = count;
                    modeStreams = seen;
                }
            }
            result.Add(new EncounterPathTiles(modeCount, (double)modeStreams / streams.Length,
                tiles[path].Select(pair => new EncounterTileShare(pair.Key, (double)pair.Value / streams.Length)).ToList()));
        }
        return result;
    }

    private sealed class Tally
    {
        internal Tally(int lanes, int paths)
        {
            Counts = new byte[lanes * paths];
            Hits = new int[lanes];
            Sample = new int[lanes];
            Shape = new byte[lanes * paths];
            Array.Fill(Sample, int.MaxValue);
        }

        internal byte[] Counts { get; }

        internal int[] Hits { get; }

        internal int[] Sample { get; }

        internal byte[] Shape { get; }
    }

    private static uint[] MainStreams(int samples)
    {
        var streams = new uint[Math.Max(1, samples)];
        ulong state = 0x9E3779B97F4A7C15;
        for (int index = 0; index < streams.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            streams[index] = (uint)(state >> 32);
        }
        return streams;
    }
}
