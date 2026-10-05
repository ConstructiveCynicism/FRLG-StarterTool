using System.Globalization;
using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.App;

public sealed record ContextAdvice(bool NeedWalking, bool Sure, string Text, LabLive? Live, int Window,
    int LadyDelay = 0)
{
    public static ContextAdvice BeforeLab(LabParity parity, bool sure, double share = 1.0)
    {
        int delay = parity.LadyDelay;
        string lead = Lead(parity.NeedWalking, sure, share);

        string what;
        if (!parity.NeedWalking)
        {
            what = delay <= RouteTimeline.LabObservableFrames - ObjectEventSim.NormalWalkFrames
                ? string.Format(CultureInfo.InvariantCulture,
                    "Lady steps at {0} - turn off Oak, stop before the ball, turn to it and press on {1}",
                    delay, LabRoute.HeldOkPressFrame)
                : delay < LabRoute.DirectPressFrame
                    ? string.Format(CultureInfo.InvariantCulture,
                        "wait for the Lady to finish her step ({0}-{1}), then press", delay,
                        delay + ObjectEventSim.NormalWalkFrames)
                    : string.Format(CultureInfo.InvariantCulture,
                        "press before the Lady steps (~{0})", delay);
        }
        else
        {
            what = delay <= RouteTimeline.LabObservableFrames - ObjectEventSim.NormalWalkFrames
                ? string.Format(CultureInfo.InvariantCulture,
                    "her step at {0} is over by the ball - wait for her NEXT step and press while she moves",
                    delay)
                : string.Format(CultureInfo.InvariantCulture,
                    "press while the Lady is stepping ({0}-{1})", delay,
                    delay + ObjectEventSim.NormalWalkFrames);
        }

        return new ContextAdvice(parity.NeedWalking, sure, lead + " · " + what, null, 0, delay);
    }

    private static string Lead(bool needWalking, bool sure, double share)
    {
        string verdict = needWalking ? "Parity WRONG" : "Parity OK";
        if (sure) return verdict;

        int pct = Math.Min(99, (int)Math.Floor(share * 100.0));
        return string.Format(CultureInfo.InvariantCulture, "Likely {0} ({1}%)", verdict, pct);
    }

    internal static int EarliestNextStep(int delay) =>
        delay + ObjectEventSim.NormalWalkFrames + ObjectEventSim.MovementDelaysMedium.Min() - 1;

    public static ContextAdvice InLab(bool needWalking, LabLive live, int window, bool sure, double share = 1.0)
    {
        var walks = live.LadyWalks
            .Where(w => w.End > RouteTimeline.LabObservableFrames - ObjectEventSim.NormalWalkFrames
                && w.Start <= LabRun.AdapterMaxWindowFrames)
            .ToList();

        string lead = Lead(needWalking, sure, share);
        string steps = walks.Count == 0
            ? "she does not step"
            : "steps " + string.Join(", ", walks.Select(w =>
                string.Format(CultureInfo.InvariantCulture, "{0}-{1}", w.Start, w.End)));

        string what = needWalking
            ? walks.Count == 0
                ? "the Lady never steps in time - the frame is out of reach; pick another"
                : "press while the Lady is stepping (" + steps + ")"
            : "press while the Lady stands still (" + steps + ")";

        return new ContextAdvice(needWalking, sure, lead + " · " + what, live, window);
    }

    public static ContextAdvice Assumed(bool toldToWalk, LabLive live, int window) =>
        new(!toldToWalk, false,
            toldToWalk
                ? "Other parity · assumed the Lady was NOT interrupted at the ball"
                : "Other parity · assumed the Lady was interrupted at the ball",
            live, window) { IsAssumption = true };

    public bool IsAssumption { get; init; }

    public static ContextAdvice AfterBall(bool reachable, bool ladyWalked, int window, LabLive live, bool sure,
        double share = 1.0)
    {
        string lead = Lead(!reachable, sure, share);
        string pressed = string.Format(CultureInfo.InvariantCulture,
            "ball pressed on {0} while the Lady {1}", window, ladyWalked ? "stepped" : "stood");
        string what = reachable
            ? pressed + " - this frame is in reach"
            : pressed + " - this frame is out of reach; pick the other parity";

        return new ContextAdvice(!reachable, sure, lead + " · " + what, live, window) { IsMeasured = true };
    }

    public bool IsMeasured { get; init; }

    public bool HasCue => Live != null && !IsAssumption && !IsMeasured;

    public LabRoute? Route => Live == null ? LabRoute.For(new LabParity(NeedWalking, LadyDelay)) : null;

    public bool? PressNow(int frame) =>
        Live is { } live ? live.LadyWalking(frame) == NeedWalking : null;

    public bool? StillReachable(int frame)
    {
        if (Live is not { } live) return null;

        for (int f = Math.Max(frame, 0); f <= LabRun.AdapterMaxWindowFrames; f++)
        {
            if (live.LadyWalking(f) == NeedWalking) return true;
        }

        return false;
    }
}
