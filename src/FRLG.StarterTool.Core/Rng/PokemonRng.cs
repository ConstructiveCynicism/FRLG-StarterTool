using FRLG.StarterTool.Core.Pokemon;

namespace FRLG.StarterTool.Core.Rng;

public class PokemonRng
{
    public int Frame;
    public long Pid;
    public Nature? Nature;
    public int Hp;
    public int Atk;
    public int Def;
    public int Spa;
    public int Spd;
    public int Spe;

    private int _minGender;

    public PokemonRng()
    {
    }

    public PokemonRng(int pid, Gen3Rng rng)
    {
        Pid = pid;
        Generate(rng);
    }

    protected static int GetSlot(int a)
    {
        a %= 100;
        if (a < 20) return 0;
        if (a < 40) return 1;
        if (a < 50) return 2;
        if (a < 60) return 3;
        if (a < 70) return 4;
        if (a < 80) return 5;
        if (a < 85) return 6;
        if (a < 90) return 7;
        if (a < 94) return 8;
        if (a < 98) return 9;
        if (a == 98) return 10;
        return 11;
    }

    protected void Generate(Gen3Rng rng)
    {
        rng.Advance();
        int top = rng.GetTop();
        Hp = top % 32;
        Atk = top / 32 % 32;
        Def = top / 1024 % 32;

        rng.Advance();
        top = rng.GetTop();
        Spe = top % 32;
        Spa = top / 32 % 32;
        Spd = top / 1024 % 32;

        int genderAux = (int)(Pid & 0xFFL);
        _minGender = genderAux < 31 ? 12
            : genderAux < 64 ? 25
            : genderAux < 127 ? 50
            : genderAux < 191 ? 75
            : 100;
    }

    public int GetTopPid() => (int)((Pid >> 16) & 0xFFFFL);

    public int GetLowPid() => (int)(Pid & 0xFFFFL);

    public bool IsShiny(int tid, int sid) => (tid ^ sid ^ (GetLowPid() ^ GetTopPid())) < 8;

    public bool IsFemale(GenderRate gr) => gr switch
    {
        GenderRate.FemaleOnly => true,
        GenderRate.Genderless => false,
        GenderRate.Male25 => _minGender <= 75,
        GenderRate.Male50 => _minGender <= 50,
        GenderRate.Male75 => _minGender <= 25,
        GenderRate.Male87 => _minGender <= 12,
        GenderRate.MaleOnly => false,
        _ => false
    };
}
