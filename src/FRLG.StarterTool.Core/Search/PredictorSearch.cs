using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Search;

public sealed class PredictorSearchCriteria
{
    public int Seed;

    public int MinFrame;
    public int MaxFrame;

    public bool[]? Natures;

    public StatPack Minus;

    public StatPack Neutral;

    public StatPack Plus;
}

public static class PredictorSearch
{
    public static List<PokemonRng> Search(PredictorSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var results = new List<PokemonRng>();
        var seed = new Seed(criteria.Seed);

        for (int frame = criteria.MinFrame; frame <= criteria.MaxFrame; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pkm = new PokemonMethod1(seed, frame);
            var nature = pkm.Nature!;

            if (criteria.Natures != null && !criteria.Natures[nature.Id])
            {
                continue;
            }

            var thresholds = criteria.Neutral;
            for (int stat = 1; stat < 6; stat++)
            {
                double boost = nature.GetNatureBoost(stat);
                if (boost > 1.01) thresholds.SetStat(stat, criteria.Plus.GetStat(stat));
                if (boost < 0.99) thresholds.SetStat(stat, criteria.Minus.GetStat(stat));
            }

            if (thresholds.Hp <= pkm.Hp
                && thresholds.Atk <= pkm.Atk
                && thresholds.Def <= pkm.Def
                && thresholds.Spa <= pkm.Spa
                && thresholds.Spd <= pkm.Spd
                && thresholds.Spe <= pkm.Spe)
            {
                results.Add(pkm);
            }
        }

        return results;
    }
}
