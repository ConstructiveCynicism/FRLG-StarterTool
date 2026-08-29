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

    public const int DefaultLimit = 500;

    public static EncounterSearchResult Search(IReadOnlyList<EncounterPath> route,
        int samples = DefaultSamples, int limit = DefaultLimit,
        int cycles = TitleSeedTable.CycleOffset,
        TitleProtocol protocol = TitleProtocol.Sweep,
        TitleVariant variant = default,
        CancellationToken cancellationToken = default,
        IReadOnlyList<TitleVariant>? variants = null)
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
        for (int titleSeed = 0; titleSeed < lanes; titleSeed++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int wildSeed = EncounterModel.WildSeedOf(titleSeed);
            if (hits[wildSeed] == 0) continue;

            int[]? shape = null;
            foreach (TitleVariant one in asked)
            {
                PressFrame? press = TitleSeedTable.Find(titleSeed, cycles, protocol, one);
                if (press is null) continue;

                if (shape is null)
                {
                    shape = new int[route.Count];
                    for (int path = 0; path < route.Count; path++) shape[path] = bestShape[path * lanes + wildSeed];
                }
                found.Add(new EncounterMatch(press.Value, wildSeed, shape, (double)hits[wildSeed] / streams.Length));
            }
        }

        found.Sort(Cheapest);

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

        return left.Press.Variant.Sound.CompareTo(right.Press.Variant.Sound);
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
