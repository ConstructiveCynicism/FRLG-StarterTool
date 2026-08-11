namespace FRLG.StarterTool.Core.Npc;

public enum NpcId
{
    SignLady,

    FatMan,

    ScientistLeft,

    Aide,

    ScientistRight,
}

public static class NpcIds
{
    public static string Name(this NpcId id) => id switch
    {
        NpcId.SignLady => "Sign Lady",

        NpcId.FatMan => "Fence Guy",
        NpcId.ScientistLeft => "Scientist (left)",
        NpcId.Aide => "Aide",
        NpcId.ScientistRight => "Scientist (right)",
        _ => id.ToString(),
    };

    public static string ShortName(this NpcId id) => id switch
    {
        NpcId.SignLady => "Lady",
        NpcId.FatMan => "Fence",
        NpcId.ScientistLeft => "Sci L",
        NpcId.Aide => "Aide",
        NpcId.ScientistRight => "Sci R",
        _ => id.ToString(),
    };
}
