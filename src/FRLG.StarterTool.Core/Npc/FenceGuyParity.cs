namespace FRLG.StarterTool.Core.Npc;

public enum SpawnRead
{
    PostVBlank,

    PreVBlank
}

[Flags]
public enum SpawnReadSides
{
    None = 0,

    PostPost = 1,
    PostPre = 2,
    PrePost = 4,
    PrePre = 8
}

public static class SpawnReadSet
{
    public static SpawnReadSides Of(SpawnRead spawn, SpawnRead respawn) => (spawn, respawn) switch
    {
        (SpawnRead.PostVBlank, SpawnRead.PostVBlank) => SpawnReadSides.PostPost,
        (SpawnRead.PostVBlank, SpawnRead.PreVBlank) => SpawnReadSides.PostPre,
        (SpawnRead.PreVBlank, SpawnRead.PostVBlank) => SpawnReadSides.PrePost,
        _ => SpawnReadSides.PrePre
    };

    public static string? Resolved(SpawnReadSides sides) => sides switch
    {
        SpawnReadSides.PostPost => "post",
        SpawnReadSides.PrePre => "pre",
        _ => null
    };
}

public enum FenceGuyParity
{
    Post,

    Pre,

    Both
}
