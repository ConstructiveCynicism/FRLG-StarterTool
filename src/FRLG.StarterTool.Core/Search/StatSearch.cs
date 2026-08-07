using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Search;

public sealed class StatSearchCriteria
{
    public int Seed;

    public int MinFrame;
    public int MaxFrame;

    public int[] BaseStats = new int[6];

    public int Level = 5;

    public int[] Stats = new int[6];

    public int NatureId = -1;
}

public static class StatSearch
{
    public static List<PokemonRng> Search(StatSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var results = new List<PokemonRng>();
        var seed = new Seed(criteria.Seed);

        for (int frame = criteria.MinFrame; frame <= criteria.MaxFrame; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pkm = new PokemonMethod1(seed, frame);
            Nature nature = pkm.Nature!;

            if (criteria.NatureId >= 0 && nature.Id != criteria.NatureId)
            {
                continue;
            }

            var ivs = new[] { pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe };
            int[] stats = StatCalculator.Calculate(criteria.BaseStats, ivs, criteria.Level, nature);

            bool match = true;
            for (int stat = 0; stat < 6; stat++)
            {
                if (criteria.Stats[stat] > 0 && criteria.Stats[stat] != stats[stat])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                results.Add(pkm);
            }
        }

        return results;
    }
}
