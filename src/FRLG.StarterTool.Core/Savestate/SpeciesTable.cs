using System.Globalization;
using System.Reflection;

namespace FRLG.StarterTool.Core.Savestate;

public static class SpeciesTable
{
    public const int MaxInternalId = 411;

    private static int[]? _toNational;
    private static int[]? _toInternal;
    private static int[][]? _evYield;

    public static int ToNational(int internalId)
    {
        Load();
        return internalId >= 1 && internalId <= MaxInternalId ? _toNational![internalId] : 0;
    }

    public static int ToInternal(int nationalId)
    {
        Load();
        return nationalId >= 1 && nationalId < _toInternal!.Length ? _toInternal[nationalId] : 0;
    }

    public static int[] EvYield(int nationalId)
    {
        Load();
        int internalId = ToInternal(nationalId);
        return internalId == 0 ? new int[6] : (int[])_evYield![internalId].Clone();
    }

    private static void Load()
    {
        if (_toNational != null) return;

        var toNational = new int[MaxInternalId + 1];
        var toInternal = new int[Pokemon.PokemonSpecies.Gen3DexSize + 1];
        var yields = new int[MaxInternalId + 1][];
        for (int i = 0; i <= MaxInternalId; i++) yields[i] = new int[6];

        Assembly assembly = typeof(SpeciesTable).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("FRLG.StarterTool.Core.Data.speciesEvYield.csv")
            ?? throw new InvalidOperationException("Embedded resource speciesEvYield.csv is missing.");
        using var reader = new StreamReader(stream);

        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            string[] cells = line.Split(',');

            int internalId = int.Parse(cells[0], CultureInfo.InvariantCulture);
            int national = int.Parse(cells[1], CultureInfo.InvariantCulture);
            if (internalId < 1 || internalId > MaxInternalId) continue;

            if (national >= 1 && national <= Pokemon.PokemonSpecies.Gen3DexSize)
            {
                toNational[internalId] = national;
                toInternal[national] = internalId;
            }

            for (int stat = 0; stat < 6; stat++)
            {
                yields[internalId][stat] = int.Parse(cells[stat + 2], CultureInfo.InvariantCulture);
            }
        }

        _evYield = yields;
        _toInternal = toInternal;
        _toNational = toNational;
    }
}
