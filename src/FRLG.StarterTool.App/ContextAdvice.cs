using System.Globalization;
using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.App;

public sealed record ContextAdvice(bool NeedWalking, bool Sure, string Text, LabLive? Live, int Window)
{
    public static ContextAdvice BeforeLab(LabParity parity, bool sure)
    {
        int delay = parity.LadyDelay;
        string lead = (sure ? "" : "Likely ") + (parity.NeedWalking ? "Parity WRONG" : "Parity OK");

        string what;
        if (!parity.NeedWalking)
        {
            what = delay <= RouteTimeline.LabObservableFrames - ObjectEventSim.NormalWalkFrames
                ? string.Format(CultureInfo.InvariantCulture,
                    "Lady steps at {0}, done before the ball - press before she steps again (she can from {1})",
                    delay, EarliestNextStep(delay))
                : delay <= RouteTimeline.LabObservableLateFrames
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

        return new ContextAdvice(parity.NeedWalking, sure, lead + " · " + what, null, 0);
    }

    internal static int EarliestNextStep(int delay) =>
        delay + ObjectEventSim.NormalWalkFrames + ObjectEventSim.MovementDelaysMedium.Min() - 1;

    public static ContextAdvice InLab(bool needWalking, LabLive live, int window, bool sure)
    {
        var walks = live.LadyWalks
            .Where(w => w.End > RouteTimeline.LabObservableFrames - ObjectEventSim.NormalWalkFrames
                && w.Start <= LabRun.AdapterMaxWindowFrames)
            .ToList();

        string lead = (sure ? "" : "Likely ") + (needWalking ? "Parity WRONG" : "Parity OK");
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
