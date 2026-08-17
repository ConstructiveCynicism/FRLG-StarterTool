using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Search;

namespace FRLG.StarterTool.Core.Savestate;

public enum EditMode
{
    Keep,

    Specific,

    Random
}

public sealed class MonEdit
{
    public EditMode NatureMode { get; set; } = EditMode.Keep;

    public int NatureId { get; set; }

    public bool[]? AllowedNatures { get; set; }

    public EditMode IvMode { get; set; } = EditMode.Keep;

    public int[] Ivs { get; set; } = new int[6];

    public StatPack IvMinus { get; set; }

    public StatPack IvNeutral { get; set; }

    public StatPack IvPlus { get; set; }

    public EditMode EvMode { get; set; } = EditMode.Keep;

    public int[] Evs { get; set; } = new int[6];

    public IReadOnlyCollection<int> TargetSpecies { get; set; } = Array.Empty<int>();

    public bool ChangesNothing =>
        NatureMode == EditMode.Keep && IvMode == EditMode.Keep && EvMode == EditMode.Keep;

    public void Apply(Gen3Mon mon, Random random)
    {
        if (NatureMode == EditMode.Specific)
        {
            mon.Repersonalize(new Nature(NatureId), random);
        }
        else if (NatureMode == EditMode.Random)
        {
            mon.Repersonalize(new Nature(RollNature(random)), random);
        }

        if (IvMode == EditMode.Specific)
        {
            mon.SetIvs(Ivs);
        }
        else if (IvMode == EditMode.Random)
        {
            mon.SetIvs(RollIvs(mon.Nature, random));
        }

        if (EvMode == EditMode.Specific)
        {
            mon.SetEvs(Evs);
        }

        int species = mon.Species;
        if (species >= 1)
        {
            mon.RecalculateStats(PokemonSpecies.Get(species).BaseStats);
        }
    }

    public int RollNature(Random random)
    {
        var allowed = new List<int>(Nature.NatureCount);
        for (int id = 0; id < Nature.NatureCount; id++)
        {
            if (AllowedNatures == null || AllowedNatures.Length <= id || AllowedNatures[id]) allowed.Add(id);
        }
        if (allowed.Count == 0)
        {
            for (int id = 0; id < Nature.NatureCount; id++) allowed.Add(id);
        }
        return allowed[random.Next(allowed.Count)];
    }

    public int[] RollIvs(Nature nature, Random random)
    {
        StatPack thresholds = IvNeutral;
        for (int stat = 1; stat < 6; stat++)
        {
            double boost = nature.GetNatureBoost(stat);
            if (boost > 1.01) thresholds.SetStat(stat, IvPlus.GetStat(stat));
            if (boost < 0.99) thresholds.SetStat(stat, IvMinus.GetStat(stat));
        }

        var ivs = new int[6];
        for (int stat = 0; stat < 6; stat++)
        {
            int floor = Math.Clamp(thresholds.GetStat(stat), 0, 31);
            ivs[stat] = random.Next(floor, 32);
        }
        return ivs;
    }
}
