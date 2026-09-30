using FRLG.StarterTool.Core.Rng;
using FRLG.StarterTool.Core.Search;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.Core.Savestate;

public sealed class StarterRoll
{
    public const int MaxAttempts = 4096;

    private StarterRoll(int trainerId, PokemonRng pokemon, bool backup)
    {
        TrainerId = trainerId;
        Pokemon = pokemon;
        Backup = backup;
    }

    public int TrainerId { get; }

    public PokemonRng Pokemon { get; }

    public bool Backup { get; }

    public int[] Ivs => new[] { Pokemon.Hp, Pokemon.Atk, Pokemon.Def, Pokemon.Spa, Pokemon.Spd, Pokemon.Spe };

    public static StarterRoll? Roll(FilterPreset filter, Random random)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int trainerId = random.Next(0x10000);
            List<RangeSearchCriteria> criteria = RangeSearch.CriteriaOf(filter, trainerId);

            List<PokemonRng> rows = RangeSearch.Search(criteria.Where(range => !range.Backup).ToList());
            bool backup = false;
            if (rows.Count == 0)
            {
                rows = RangeSearch.Search(criteria);
                backup = true;
            }
            if (rows.Count == 0) continue;

            return new StarterRoll(trainerId, rows[random.Next(rows.Count)], backup);
        }

        return null;
    }
}
