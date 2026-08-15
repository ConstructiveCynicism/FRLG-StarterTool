using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.Core.Npc;

public readonly record struct FenceFrameState(
    int Frame,
    int X,
    int Y,
    Direction Facing,
    int WalkStep)
{
    public bool Walking => WalkStep > 0;
}

public readonly record struct FenceCandidate(
    int ExitFrame,
    int OakFrame,
    IReadOnlyList<NpcEvent> Eastward,
    IReadOnlyList<NpcEvent> LeadWalk,
    IReadOnlyList<FenceFrameState> Motion,
    int EastwardRolls,
    int LeadWalkRolls,
    int AdvancesBeforeLabLoad,
    int TotalAdvances,
    double AnchorWeight = 1.0,
    int ManualAdvances = 0,
    HiddenMoves Hidden = default,
    SpawnRead SpawnReadSide = SpawnRead.PostVBlank,
    SpawnRead RespawnReadSide = SpawnRead.PostVBlank,
    SpawnReadSides MergedReads = SpawnReadSides.None)
{
    private int PressFrame => OakFrame + RouteTimeline.AnchorCorrectionFrames;

    public int LeadWalkStartFrame => PressFrame + RouteTimeline.LeadWalkFatManRespawnFrames;

    public int LeadWalkVisibleFrame => PressFrame + RouteTimeline.LeadWalkFatManVisibleFrames;

    public int LeadWalkFullyVisibleFrame =>
        PressFrame + RouteTimeline.LeadWalkFatManFullyVisibleFrames;

    public int FirstRequiredEvent
    {
        get
        {
            int index = 0;
            while (index < LeadWalk.Count && LeadWalk[index].Frame < FullyVisibleFrame) index++;
            return index;
        }
    }

    public const int VisibleFrame =
        RouteTimeline.LeadWalkFatManVisibleFrames - RouteTimeline.LeadWalkFatManRespawnFrames;

    public const int FullyVisibleFrame =
        RouteTimeline.LeadWalkFatManFullyVisibleFrames - RouteTimeline.LeadWalkFatManRespawnFrames;

    public int MissedCorrection(int oakPressFrame) =>
        oakPressFrame + RouteTimeline.OakTextToLabLoadFrames
        - TotalAdvances - RouteTimeline.BallGenerationAdvances
        + VariableOffsetCalculator.TidLagFrames;

    public string ParityLabel => $"{Letter(SpawnReadSide)}/{Letter(RespawnReadSide)}";

    public SpawnReadSides CompatibleReads =>
        MergedReads | SpawnReadSet.Of(SpawnReadSide, RespawnReadSide);

    public string ParitySuffix =>
        SpawnReadSide == SpawnRead.PostVBlank && RespawnReadSide == SpawnRead.PostVBlank
            ? ""
            : $" (parity {ParityLabel})";

    private static string Letter(SpawnRead read) =>
        read == SpawnRead.PreVBlank ? "pre" : "post";

    public override string ToString() =>
        $"exit {ExitFrame} oak {OakFrame} -> {TotalAdvances} ({LeadWalk.Count} seen){ParitySuffix}";
}

public static class FenceRun
{
    public static IReadOnlyList<FenceCandidate> Build(int seed, double exitElapsedMs,
        double oakElapsedMs, double fps, double contextMs, int manualAdvances = 0,
        FenceGuyParity parity = FenceGuyParity.Post)
    {
        double shiftMs = RouteTimeline.AnchorCorrectionFrames * 1000.0 / fps;
        double window = contextMs + StartUncertaintyMs;
        double exitAt = exitElapsedMs - shiftMs;
        double oakAt = oakElapsedMs - shiftMs;

        return Build(seed,
            FrameWindow.Candidates(exitAt, fps, window),
            FrameWindow.Candidates(oakAt, fps, window),
            frame => FrameWindow.Weight(exitAt, fps, window, frame),
            frame => FrameWindow.Weight(oakAt, fps, window, frame),
            manualAdvances, parity);
    }

    public const double StartUncertaintyMs = 100.0;

    public const double AnchorSharpness = 1.5;

    public const double PreReadPrior = 66.0 / 76.0;

    public static IReadOnlyList<FenceCandidate> Build(int seed,
        IEnumerable<int> exitFrames, IEnumerable<int> oakFrames, int manualAdvances = 0,
        FenceGuyParity parity = FenceGuyParity.Post) =>
        Build(seed, exitFrames, oakFrames, null, null, manualAdvances, parity);

    private static IReadOnlyList<FenceCandidate> Build(int seed,
        IEnumerable<int> exitFrames, IEnumerable<int> oakFrames,
        Func<int, double>? exitWeight, Func<int, double>? oakWeight, int manualAdvances,
        FenceGuyParity parity = FenceGuyParity.Post)
    {
        List<int> exit = exitFrames.ToList();
        List<int> oak = oakFrames.ToList();

        (SpawnRead Spawn, SpawnRead Respawn)[] sides = parity switch
        {
            FenceGuyParity.Pre => new[] { (SpawnRead.PreVBlank, SpawnRead.PreVBlank) },
            FenceGuyParity.Both => new[]
            {
                (SpawnRead.PostVBlank, SpawnRead.PostVBlank),
                (SpawnRead.PostVBlank, SpawnRead.PreVBlank),
                (SpawnRead.PreVBlank, SpawnRead.PostVBlank),
                (SpawnRead.PreVBlank, SpawnRead.PreVBlank),
            },
            _ => new[] { (SpawnRead.PostVBlank, SpawnRead.PostVBlank) },
        };

        int perSide = exit.Count * oak.Count;
        var all = new FenceCandidate[perSide * sides.Length];
        Parallel.For(0, all.Length, i =>
        {
            (SpawnRead spawn, SpawnRead respawn) = sides[i / perSide];
            int pair = i % perSide;
            all[i] = Simulate(seed, exit[pair / oak.Count], oak[pair % oak.Count], manualAdvances,
                spawn, respawn);
        });

        var index = new Dictionary<string, int>();
        var candidates = new List<FenceCandidate>();
        var weights = new List<double>();

        for (int i = 0; i < all.Length; i++)
        {
            int pairIndex = i % perSide;
            (SpawnRead spawnSide, SpawnRead respawnSide) = sides[i / perSide];
            double prior =
                (spawnSide == SpawnRead.PreVBlank ? PreReadPrior : 1.0)
                * (respawnSide == SpawnRead.PreVBlank ? PreReadPrior : 1.0);
            double weight = prior * (exitWeight is null || oakWeight is null
                ? 1.0
                : Math.Pow(exitWeight(exit[pairIndex / oak.Count]) * oakWeight(oak[pairIndex % oak.Count]),
                    AnchorSharpness));

            if (index.TryGetValue(Observable(all[i]), out int at))
            {
                weights[at] += weight;

                candidates[at] = candidates[at] with
                {
                    MergedReads = candidates[at].CompatibleReads
                        | SpawnReadSet.Of(all[i].SpawnReadSide, all[i].RespawnReadSide),
                };

                continue;
            }

            index[Observable(all[i])] = candidates.Count;
            candidates.Add(all[i]);
            weights.Add(weight);
        }

        double total = weights.Sum();
        for (int i = 0; i < candidates.Count; i++)
        {
            candidates[i] = candidates[i] with
            {
                AnchorWeight = total > 0.0 ? weights[i] / total : 1.0 / candidates.Count,
            };
        }

        return candidates;
    }

    private static string Observable(FenceCandidate candidate) =>
        candidate.TotalAdvances + "|" + string.Join(",",
            candidate.LeadWalk.Select(e => $"{e.Direction}@{e.Frame}"));

    public static FenceCandidate Simulate(int seed, int exitFrame, int oakFrame,
        int manualAdvances = 0, SpawnRead spawnRead = SpawnRead.PostVBlank,
        SpawnRead respawnRead = SpawnRead.PostVBlank)
    {
        var rng = new GameRng(seed);

        for (int i = 0; i < Math.Max(0, exitFrame); i++) rng.VBlank();

        for (int i = 0; i < Math.Max(0, manualAdvances); i++) rng.Random();

        RouteTimeline.RunPlayersHouse(rng);

        int walkFrames = Math.Max(0, oakFrame - exitFrame
            - RouteTimeline.ExitHouseToPalletControlFrames
            - RouteTimeline.TriggerToOakTextFrames);

        int beforeEastward = rng.Advances;
        var eastward = new List<NpcEvent>();
        OverworldSim pallet = RouteTimeline.RunPalletTown(rng, walkFrames, eastward, spawnRead);
        int eastwardRolls = rng.Advances - beforeEastward
            - (RouteTimeline.FatManSpawnToControlFrames + walkFrames);

        ObjectEventSim fatMan = pallet.Objects.First(o => o.Slot == MapObjects.PalletFatMan);
        int cutsceneFrames = RouteTimeline.TriggerToOakTextFrames + RouteTimeline.OakTextToLabLoadFrames;
        int respawn = RouteTimeline.CutsceneRespawnFrame(cutsceneFrames);

        int respawnOnSimAxis = pallet.Frame + respawn;

        int beforeLeadWalk = rng.Advances;
        var leadWalk = new List<NpcEvent>();
        var motion = new List<FenceFrameState>();

        RouteTimeline.RunFrozenCutscene(pallet, cutsceneFrames, leadWalk, (frame, _) =>
        {
            if (frame < respawn) return;
            motion.Add(new FenceFrameState(frame - respawn, fatMan.X, fatMan.Y, fatMan.FacingDirection,
                fatMan.SingleMovementActive && fatMan.Action == MovementAction.WalkNormal
                    ? fatMan.WalkStepNo
                    : 0));
        }, respawnRead);

        int leadWalkRolls = rng.Advances - beforeLeadWalk - cutsceneFrames;

        int beforeLabLoad = rng.Advances;
        RouteTimeline.EnterLab(rng, 0);

        var walk = leadWalk
            .Where(e => e.Npc == NpcId.FatMan)
            .Select(e => e with { Frame = e.Frame - respawnOnSimAxis })
            .ToList();

        var seen = walk.Where(e => !e.Silent).ToList();

        HiddenMoves hidden = HiddenMoves
            .Count(NpcId.FatMan, eastward.Where(e => e.Npc == NpcId.FatMan), _ => false)
            .Plus(HiddenMoves.Count(NpcId.FatMan, walk, e => e.Frame >= FenceCandidate.VisibleFrame));

        return new FenceCandidate(exitFrame, oakFrame, eastward, seen, motion,
            eastwardRolls, leadWalkRolls, beforeLabLoad, rng.Advances, 1.0, manualAdvances, hidden,
            spawnRead, respawnRead);
    }

    public static HiddenMoves SimulateEastward(int seed, int exitFrame, int manualAdvances = 0,
        SpawnRead spawnRead = SpawnRead.PostVBlank)
    {
        var rng = new GameRng(seed);

        for (int i = 0; i < Math.Max(0, exitFrame); i++) rng.VBlank();
        for (int i = 0; i < Math.Max(0, manualAdvances); i++) rng.Random();
        RouteTimeline.RunPlayersHouse(rng);

        var eastward = new List<NpcEvent>();
        RouteTimeline.RunPalletTown(rng,
            RouteTimeline.FatManActiveFrames - RouteTimeline.FatManSpawnToControlFrames, eastward,
            spawnRead);

        return HiddenMoves
            .Count(NpcId.FatMan, eastward.Where(e => e.Npc == NpcId.FatMan), _ => false)
            with { Partial = true };
    }
}
