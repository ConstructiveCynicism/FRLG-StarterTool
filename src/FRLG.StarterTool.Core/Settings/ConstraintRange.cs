using FRLG.StarterTool.Core.Pokemon;

namespace FRLG.StarterTool.Core.Settings;

public sealed class ConstraintRange
{
    public string Name { get; set; } = "";

    public string MinFrame { get; set; } = "";

    public string MaxFrame { get; set; } = "";

    public bool[] Natures { get; set; } = SettingsArrays.NewNatureFilter();

    public int[] IvMinus { get; set; } = new int[6];
    public int[] IvNeutral { get; set; } = new int[6];
    public int[] IvPlus { get; set; } = new int[6];

    public bool Backup { get; set; }

    public int BackupWithin { get; set; } = 2;

    public int Color { get; set; } = Screen;

    public const int Screen = -2;

    public const int Unset = -1;

    public ConstraintRange Normalize()
    {
        Name = (Name ?? "").Trim();
        MinFrame = (MinFrame ?? "").Trim();
        MaxFrame = (MaxFrame ?? "").Trim();
        BackupWithin = Math.Clamp(BackupWithin, 0, 10000);
        if (Color != Unset && Color != Screen) Color &= 0xFFFFFF;

        Natures = SettingsArrays.Resize(Natures, Nature.NatureCount);
        IvMinus = SettingsArrays.Resize(IvMinus, 6);
        IvNeutral = SettingsArrays.Resize(IvNeutral, 6);
        IvPlus = SettingsArrays.Resize(IvPlus, 6);
        for (int stat = 0; stat < 6; stat++)
        {
            IvMinus[stat] = Math.Clamp(IvMinus[stat], 0, 31);
            IvNeutral[stat] = Math.Clamp(IvNeutral[stat], 0, 31);
            IvPlus[stat] = Math.Clamp(IvPlus[stat], 0, 31);
        }

        return this;
    }

    public ConstraintRange Clone() => new()
    {
        Name = Name,
        MinFrame = MinFrame,
        MaxFrame = MaxFrame,
        Natures = (bool[])Natures.Clone(),
        IvMinus = (int[])IvMinus.Clone(),
        IvNeutral = (int[])IvNeutral.Clone(),
        IvPlus = (int[])IvPlus.Clone(),
        Backup = Backup,
        BackupWithin = BackupWithin,
        Color = Color
    };

    public bool SameRangeAs(ConstraintRange other) =>
        other != null
        && FrameEquals(MinFrame, other.MinFrame)
        && FrameEquals(MaxFrame, other.MaxFrame)
        && Backup == other.Backup
        && BackupWithin == other.BackupWithin
        && Color == other.Color
        && Natures.AsSpan().SequenceEqual(other.Natures)
        && IvMinus.AsSpan().SequenceEqual(other.IvMinus)
        && IvNeutral.AsSpan().SequenceEqual(other.IvNeutral)
        && IvPlus.AsSpan().SequenceEqual(other.IvPlus);

    internal static bool FrameEquals(string? a, string? b) =>
        (a ?? "").Trim().TrimStart('0') == (b ?? "").Trim().TrimStart('0');

    public override string ToString() => Name;
}
