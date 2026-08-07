using System.Globalization;
using System.Reflection;

namespace FRLG.StarterTool.Core.Pokemon;

public sealed class PokemonSpecies
{
    public const int Gen3DexSize = 386;

    private static List<PokemonSpecies>? _byName;
    private static PokemonSpecies[]? _byId;

    private PokemonSpecies(int id, string name, GenderRate genderRate, int[] baseStats)
    {
        Id = id;
        Name = name;
        GenderRate = genderRate;
        BaseStats = baseStats;
    }

    public int Id { get; }

    public string Name { get; }

    public GenderRate GenderRate { get; }

    public int[] BaseStats { get; }

    public override string ToString() => Name;

    public static PokemonSpecies Get(int id)
    {
        PokemonSpecies[] table = LoadById();
        return id >= 1 && id < table.Length ? table[id] : table[1];
    }

    public static List<PokemonSpecies> GetList()
    {
        if (_byName != null) return _byName;

        PokemonSpecies[] table = LoadById();
        var list = new List<PokemonSpecies>(Gen3DexSize);
        for (int id = 1; id <= Gen3DexSize; id++)
        {
            list.Add(table[id]);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return _byName = list;
    }

    private static PokemonSpecies[] LoadById()
    {
        if (_byId != null) return _byId;

        string[][] species = ReadCsv("pokemon.csv");
        string[][] stats = ReadCsv("pokemonBaseStats.csv");

        var table = new PokemonSpecies[Gen3DexSize + 1];
        for (int id = 0; id <= Gen3DexSize; id++)
        {
            var baseStats = new int[6];
            for (int stat = 0; stat < 6; stat++)
            {
                baseStats[stat] = int.Parse(stats[id][stat + 1], CultureInfo.InvariantCulture);
            }

            table[id] = new PokemonSpecies(
                id,
                species[id][1],
                GenderRateExtensions.FromInt(int.Parse(species[id][4], CultureInfo.InvariantCulture)),
                baseStats);
        }

        return _byId = table;
    }

    private static string[][] ReadCsv(string name)
    {
        Assembly assembly = typeof(PokemonSpecies).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"FRLG.StarterTool.Core.Data.{name}")
            ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
        using var reader = new StreamReader(stream);

        var rows = new List<string[]>(Gen3DexSize + 1);
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0) rows.Add(line.Split(','));
        }
        return rows.ToArray();
    }
}
