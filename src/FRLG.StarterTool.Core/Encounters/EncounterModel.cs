namespace FRLG.StarterTool.Core.Encounters;

public static class EncounterModel
{
    private const uint Mult = 1103515245;
    private const uint MainAdd = 24691;
    private const uint WildAdd = 12345;

    private const int ImmuneRate = 5;

    private const int PatchRate = 60;

    private const int MaxRate = 1600;

    public const int WildSeedCount = 1 << 16;

    public static uint NextMain(uint value) => unchecked(Mult * value + MainAdd);

    public static uint NextWild(uint value) => unchecked(Mult * value + WildAdd);

    public const int WildSeedAdvances = 2;

    public static int WildSeedOf(int titleSeed)
    {
        uint value = (uint)titleSeed;
        for (int advance = 0; advance < WildSeedAdvances; advance++) value = NextMain(value);
        return (int)((value >> 16) & 0xFFFF);
    }

    public static List<(int Path, int Tile)> Simulate(uint wild, uint main, IReadOnlyList<EncounterPath> route)
    {
        var hits = new List<(int, int)>();
        int buff = 0, steps = 0;

        for (int path = 0; path < route.Count; path++)
        {
            EncounterPath spec = route[path];
            if (spec.NewMap)
            {
                buff = 0;
                steps = 0;
            }

            for (int tile = 0; tile < spec.Tiles; tile++)
            {
                bool repel = tile < spec.RepelTiles;

                bool proceed;
                if (steps >= spec.MinSteps)
                {
                    proceed = true;
                }
                else
                {
                    steps++;
                    main = NextMain(main);
                    proceed = (main >> 16) % 100 < ImmuneRate;
                }
                if (!proceed) continue;

                if (tile == 0)
                {
                    main = NextMain(main);
                    if ((main >> 16) % 100 >= PatchRate) continue;
                }

                int effective = Math.Min(MaxRate, spec.Rate * 16 + buff * 16 / 200);
                wild = NextWild(wild);
                bool hit = (wild >> 16) % MaxRate < effective;

                if (!hit)
                {
                    buff = repel ? 0 : buff + spec.Rate;
                    continue;
                }

                if (repel)
                {
                    buff = 0;
                    continue;
                }

                hits.Add((path, tile + 1));
                buff = 0;
                steps = 0;
            }
        }

        return hits;
    }

    public static void CountAll(uint main, IReadOnlyList<EncounterPath> route, byte[] counts)
    {
        int lanes = WildSeedCount;
        if (counts.Length < lanes * route.Count)
        {
            throw new ArgumentException("counts is too short for this route", nameof(counts));
        }
        Array.Clear(counts, 0, lanes * route.Count);

        var wild = new uint[lanes];
        var main0 = new uint[lanes];
        var buff = new int[lanes];
        var steps = new int[lanes];
        for (int seed = 0; seed < lanes; seed++)
        {
            wild[seed] = (uint)seed;
            main0[seed] = main;
        }

        for (int path = 0; path < route.Count; path++)
        {
            EncounterPath spec = route[path];
            if (spec.NewMap)
            {
                Array.Clear(buff);
                Array.Clear(steps);
            }

            int pathBase = path * lanes;

            for (int tile = 0; tile < spec.Tiles; tile++)
            {
                bool repel = tile < spec.RepelTiles;
                bool patch = tile == 0;
                int rate = spec.Rate;
                int minSteps = spec.MinSteps;

                for (int seed = 0; seed < lanes; seed++)
                {
                    bool proceed;
                    if (steps[seed] >= minSteps)
                    {
                        proceed = true;
                    }
                    else
                    {
                        steps[seed]++;
                        uint m = NextMain(main0[seed]);
                        main0[seed] = m;
                        proceed = (m >> 16) % 100 < ImmuneRate;
                    }
                    if (!proceed) continue;

                    if (patch)
                    {
                        uint m = NextMain(main0[seed]);
                        main0[seed] = m;
                        if ((m >> 16) % 100 >= PatchRate) continue;
                    }

                    int effective = Math.Min(MaxRate, rate * 16 + buff[seed] * 16 / 200);
                    uint w = NextWild(wild[seed]);
                    wild[seed] = w;

                    if ((w >> 16) % MaxRate >= effective)
                    {
                        buff[seed] = repel ? 0 : buff[seed] + rate;
                        continue;
                    }

                    if (repel)
                    {
                        buff[seed] = 0;
                        continue;
                    }

                    if (counts[pathBase + seed] < byte.MaxValue) counts[pathBase + seed]++;
                    buff[seed] = 0;
                    steps[seed] = 0;
                }
            }
        }
    }
}
