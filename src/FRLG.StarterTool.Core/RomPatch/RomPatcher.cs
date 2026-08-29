using System.Reflection;
using System.Security.Cryptography;

namespace FRLG.StarterTool.Core.RomPatch;

public sealed record RomKind(string Key, string Name, string Md5);

public static class RomPatcher
{
    public const string VersionError = "Must be a legal V1.1 ROM";

    public static readonly RomKind FireRed =
        new("firered", "FireRed (U) v1.1", "51901a6e40661b3914aa333c802e24e8");

    public static readonly RomKind LeafGreen =
        new("leafgreen", "LeafGreen (U) v1.1", "9d33a02159e018d09073e700e1fd10fd");

    public static readonly RomKind[] Known = { FireRed, LeafGreen };

    public static RomKind? Identify(byte[] rom)
    {
        string md5 = Convert.ToHexString(MD5.HashData(rom)).ToLowerInvariant();
        foreach (RomKind kind in Known)
        {
            if (kind.Md5 == md5) return kind;
        }

        return null;
    }

    public static byte[] BuiltInPatch(RomKind kind)
    {
        Assembly assembly = typeof(RomPatcher).Assembly;
        string name = "FRLG.StarterTool.Core.Data.seed-on-save-" + kind.Key + ".ips";
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(name + " is missing from the assembly");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static RomKind Patch(string romPath, string? patchPath, string outputPath)
    {
        byte[] rom = File.ReadAllBytes(romPath);
        RomKind kind = Identify(rom) ?? throw new InvalidDataException(VersionError);

        byte[] patch = string.IsNullOrWhiteSpace(patchPath)
            ? BuiltInPatch(kind)
            : File.ReadAllBytes(patchPath);

        byte[] patched = IpsPatch.Apply(rom, patch);

        File.WriteAllBytes(outputPath, patched);
        return kind;
    }
}
