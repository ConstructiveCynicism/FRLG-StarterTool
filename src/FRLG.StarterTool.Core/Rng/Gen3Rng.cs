namespace FRLG.StarterTool.Core.Rng;

public class Gen3Rng
{
    private int _value;
    private int _frame;

    private static readonly int[] A =
    {
        1103515245, -1029531031, -301564143, -807543007, 1601471041, -1959914367, 1979738369,
        387043841, -1100504063, -572569599, 1073647617, -1862459391, 1710899201, -604733439,
        -135725055, -271450111, -542900223, -1085800447, 2123366401, -48234495, -96468991,
        -192937983, -385875967, -771751935, -1543503871, 1207959553, -1879048191, 0x20000001,
        0x40000001, -2147483647, 1, 1
    };

    private static readonly int[] B =
    {
        24691, -377586838, 833674724, 1742452232, -878238448, 489270816, -1165693888, 2045778048,
        142160128, 1800823296, -2143501312, -1497790464, -428666880, 820387840, -238272512,
        597196800, 1194393600, -1906180096, 482607104, 965214208, 1930428416, -434110464,
        -868220928, -1736441856, 0x31000000, 0x62000000, -1006632960, -2013265920, 0x10000000,
        0x20000000, 0x40000000, int.MinValue
    };

    public Gen3Rng(int v)
    {
        _value = v;
        _frame = 0;
    }

    public Gen3Rng(Seed seed)
    {
        _value = seed.Value;
        _frame = 0;
    }

    public Gen3Rng(int v, int f)
    {
        _value = v;
        _frame = f;
    }

    public void Advance()
    {
        unchecked
        {
            _value = _value * 1103515245 + 24691;
        }
        ++_frame;
    }

    public void Advance(int n)
    {
        _frame += n;
        int i = 0;
        while (n > 0)
        {
            if (n % 2 != 0)
            {
                unchecked
                {
                    _value = _value * A[i] + B[i];
                }
            }
            n >>= 1;
            if (++i >= 32) break;
        }
    }

    public void Decrease()
    {
        unchecked
        {
            _value = _value * -289805467 + 171270561;
        }
        --_frame;
    }

    public void Decrease(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Decrease();
        }
    }

    public void GotoFrame(int n)
    {
        int diff = n - _frame;
        if (diff >= 0)
        {
            Advance(diff);
        }
        else
        {
            for (int i = 0; i < diff; i++)
            {
                unchecked
                {
                    _value = _value * -289805467 + 171270561;
                }
            }
        }
        _frame = n;
    }

    public Gen3Rng GetCopy() => new(Value, Frame);

    public int Value
    {
        get => _value;
        set => _value = value;
    }

    public int Frame => _frame;

    public int GetTop() => (_value >> 16) & 0xFFFF;

    public int ModuloCheck(int check) => GetTop() % check;

    public void Copy(Gen3Rng other)
    {
        _value = other.Value;
        _frame = other.Frame;
    }
}
