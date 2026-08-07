namespace FRLG.StarterTool.Core.Pokemon;

public class Nature
{
    public const int NatureCount = 25;

    private static readonly string[] Names =
    {
        "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
        "Bold", "Docile", "Relaxed", "Impish", "Lax",
        "Timid", "Hasty", "Serious", "Jolly", "Naive",
        "Modest", "Mild", "Quiet", "Bashful", "Rash",
        "Calm", "Gentle", "Sassy", "Careful", "Quirky"
    };

    private double[] _natureBoost = new double[6];

    public Nature(int n)
    {
        Id = n;
        Name = Names[n];
        if (n >= 0 && n < NatureCount)
        {
            SetNature(n);
        }
    }

    public Nature(string name)
    {
        Id = GetTypeId(name);
        Name = name;
        if (Id >= 0 && Id < NatureCount)
        {
            SetNature(Id);
        }
    }

    public int Id { get; private set; }

    public string Name { get; }

    public static int GetTypeId(string nat)
    {
        for (int i = 0; i < NatureCount; i++)
        {
            if (nat == Names[i])
            {
                return i;
            }
        }
        return 0;
    }

    public void SetNature(int n)
    {
        Id = n;
        _natureBoost = new double[6];
        for (int i = 0; i < 6; i++)
        {
            _natureBoost[i] = 1.0;
        }
        if (n % 6 == 0)
        {
            return;
        }
        _natureBoost[NatStatOrder(n / 5 + 1)] = 1.1;
        _natureBoost[NatStatOrder(n % 5 + 1)] = 0.9;
    }

    public double GetNatureBoost(int stat) => _natureBoost[stat];

    private static int NatStatOrder(int n) => n switch
    {
        3 => 5,
        4 => 3,
        5 => 4,
        _ => n
    };

    public Nature GetCopy() => new(Id);

    public static List<Nature> GetList()
    {
        var list = new List<Nature>(NatureCount);
        for (int i = 0; i < NatureCount; i++)
        {
            list.Add(new Nature(i));
        }
        return list;
    }

    public override string ToString() => Name;
}
