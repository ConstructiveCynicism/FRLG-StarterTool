using System.Reflection;

namespace FRLG.StarterTool.Core.Npc;

public enum Collision
{
    None = 0,
    OutsideRange = 1,
    Impassable = 2,
    ElevationMismatch = 3,
    ObjectEvent = 4,
}

public sealed class GameMap
{
    public const int MapOffset = 7;

    public const int MapOffsetW = MapOffset * 2 + 1;

    public const int MapOffsetH = MapOffset * 2;

    private static Dictionary<string, GameMap>? _maps;

    private readonly byte[] _collision;
    private readonly byte[] _elevation;

    private GameMap(string name, int width, int height, byte[] collision, byte[] elevation)
    {
        Name = name;
        Width = width;
        Height = height;
        _collision = collision;
        _elevation = elevation;
    }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public static GameMap PalletTown => Get("PALLET_TOWN");

    public static GameMap OaksLab => Get("OAKS_LAB");

    public static GameMap Get(string name)
    {
        Dictionary<string, GameMap> maps = Load();
        if (!maps.TryGetValue(name, out GameMap? map))
        {
            throw new ArgumentException($"No map data for '{name}'.", nameof(name));
        }
        return map;
    }

    public int CollisionAt(int x, int y) =>
        InBounds(x, y) ? _collision[y * Width + x] : 1;

    public int ElevationAt(int x, int y) =>
        InBounds(x, y) ? _elevation[y * Width + x] : 0;

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public bool IsTerrainBlockedFor(int elevation, int x, int y) =>
        !InBounds(x, y) || CollisionAt(x, y) != 0 || IsElevationMismatchAt(elevation, x, y);

    public bool IsElevationMismatchAt(int elevation, int x, int y)
    {
        if (elevation == 0) return false;

        int mapElevation = ElevationAt(x, y);
        if (mapElevation == 0 || mapElevation == 15) return false;

        return mapElevation != elevation;
    }

    public static bool AreElevationsCompatible(int a, int b)
    {
        if (a == 0 || b == 0) return true;
        return a == b;
    }

    private static Dictionary<string, GameMap> Load()
    {
        if (_maps != null) return _maps;

        var maps = new Dictionary<string, GameMap>(StringComparer.Ordinal);
        Assembly assembly = typeof(GameMap).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("maps.txt", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);

        string? name = null;
        int width = 0, height = 0, row = 0;
        byte[] collision = Array.Empty<byte>();
        byte[] elevation = Array.Empty<byte>();

        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            if (name == null || row == height)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                name = parts[0];
                width = int.Parse(parts[1]);
                height = int.Parse(parts[2]);
                collision = new byte[width * height];
                elevation = new byte[width * height];
                row = 0;
                maps[name] = new GameMap(name, width, height, collision, elevation);
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                collision[row * width + x] = (byte)Nibble(line[x * 2]);
                elevation[row * width + x] = (byte)Nibble(line[x * 2 + 1]);
            }
            row++;
        }

        return _maps = maps;
    }

    private static int Nibble(char c) =>
        c <= '9' ? c - '0' : (char.ToUpperInvariant(c) - 'A' + 10);
}
