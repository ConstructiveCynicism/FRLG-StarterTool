namespace FRLG.StarterTool.Core.Pokemon;

public static class StatCalculator
{
    public static int[] Calculate(int[] baseStats, int[] ivs, int level, Nature nature)
    {
        var stats = new int[6];

        stats[0] = (ivs[0] + 2 * baseStats[0] + 100) * level / 100 + 10;

        for (int stat = 1; stat < 6; stat++)
        {
            double value = ((ivs[stat] + 2 * baseStats[stat]) * level / 100 + 5) * nature.GetNatureBoost(stat);

            int truncated = (int)value;
            if (Math.Abs(truncated - value) > 0.9999) truncated++;
            stats[stat] = truncated;
        }

        return stats;
    }
}
