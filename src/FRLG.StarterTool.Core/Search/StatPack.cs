namespace FRLG.StarterTool.Core.Search;

public struct StatPack
{
    public int Hp;
    public int Atk;
    public int Def;
    public int Spa;
    public int Spd;
    public int Spe;

    public StatPack(int hp, int atk, int def, int spa, int spd, int spe)
    {
        Hp = hp;
        Atk = atk;
        Def = def;
        Spa = spa;
        Spd = spd;
        Spe = spe;
    }

    public int GetStat(int stat) => stat switch
    {
        0 => Hp,
        1 => Atk,
        2 => Def,
        3 => Spa,
        4 => Spd,
        5 => Spe,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    public int[] ToArray() => new[] { Hp, Atk, Def, Spa, Spd, Spe };

    public void SetStat(int stat, int value)
    {
        switch (stat)
        {
            case 0: Hp = value; break;
            case 1: Atk = value; break;
            case 2: Def = value; break;
            case 3: Spa = value; break;
            case 4: Spd = value; break;
            case 5: Spe = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }
}
