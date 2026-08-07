namespace FRLG.StarterTool.Core.Rng;

public class HiddenPowerRng
{
    public static readonly string[] TypeNames =
    {
        "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel",
        "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark"
    };

    public int Type;
    public int Damage;

    public HiddenPowerRng(int type, int damage)
    {
        if (type >= 0 && type < 16)
        {
            SetHiddenPower(type, damage);
        }
    }

    public HiddenPowerRng(PokemonRng pkm)
    {
        SetHiddenPower(pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe);
    }

    public void SetHiddenPower(int hp, int atk, int def, int spa, int spd, int spe)
    {
        Type = (hp % 2 + 2 * (atk % 2) + 4 * (def % 2) + 8 * (spe % 2) + 16 * (spa % 2) + 32 * (spd % 2)) * 15 / 63;
        Damage = 30 + ((hp >> 1) % 2 + 2 * ((atk >> 1) % 2) + 4 * ((def >> 1) % 2) + 8 * ((spe >> 1) % 2)
                       + 16 * ((spa >> 1) % 2) + 32 * ((spd >> 1) % 2)) * 40 / 63;
    }

    public void SetHiddenPower(int type, int damage)
    {
        Type = type;
        Damage = damage;
    }

    public string TypeName => TypeNames[Type];

    public override string ToString() => $"{TypeNames[Type]} {Damage}";
}
