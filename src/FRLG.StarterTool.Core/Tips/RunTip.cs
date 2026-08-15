using System.Globalization;

using FRLG.StarterTool.Core.Training;

namespace FRLG.StarterTool.Core.Tips;

public static class RunTip
{
    public const double LikelyHitChance = 0.5;

    public const int RecentWindow = 5;

    public const int MissStreakTip = 5;

    public const int GoodRunHits = 3;

    public const double ConsoleContextWindowMs = 10.0;

    public const double ReviveTipFromMinutes = 30.0;

    public const double ReviveTipToMinutes = 120.0;

    public const double SoonTipFromMinutes = 10.0;

    public const double SoonTipToMinutes = 15.0;

    public const double KicksTipFromMinutes = 20.0;

    public const double KicksTipToMinutes = 40.0;

    public const double FeelingTipMinutes = 3.0;

    public const double FeelingTipChance = 0.05;

    public const int RapidTripleRuns = 3;

    public const double RapidTripleMinutes = 3.0;

    public const int ShinyOdds = 8192;

    public const string ShinyTip = "You just got a shiny tip. Yay! 1/8192 chance btw";

    public static string Pick(in TipFacts facts, Random random)
    {
        if (random.Next(ShinyOdds) == 0) return ShinyTip;

        IReadOnlyList<string> specific = Applicable(facts);
        IReadOnlyList<string> pool = specific.Count > 0 ? specific : General(facts);

        return pool.Count == 0 ? "" : pool[random.Next(pool.Count)];
    }

    public static IReadOnlyList<string> Applicable(in TipFacts facts)
    {
        var tips = new List<string>();

        if (facts.BallPressUnseen)
        {
            tips.Add("You can press Start/Anchor when picking your Squirtle to predict it and fix offsets!");
        }

        if (!facts.TrainerUsed)
        {
            tips.Add("You can train your offset using the built in Offset-Trainer!");
        }

        if (facts.MissStreak >= MissStreakTip && facts.SuggestedOffsetMs is { } offset)
        {
            tips.Add(string.Format(CultureInfo.InvariantCulture,
                "Try adjusting your offset to: {0} ms", offset));
        }

        if (facts.ContextWindowMs < ConsoleContextWindowMs)
        {
            tips.Add("On Console, its recommend to increase Context Window to at least 10ms");
        }

        if (facts.DefaultWindowSize)
        {
            tips.Add("Window too big? Try adjusting the zoom or hiding constraints.");
        }

        if (!facts.OddsCalculated)
        {
            tips.Add("You can calculate odds of finding a squirtle on any Trainer ID using Calculate");
        }

        if (facts.DefaultStatBoxColors)
        {
            tips.Add("Customize your stat box colors in the settings!");
        }

        if (!facts.FenceStopReported)
        {
            tips.Add("The toggle hotkey / Finished button can let the program know fence guy stopped");
        }

        if (!facts.CuedLabPress)
        {
            tips.Add("You can use an audio cue instead of a 3rd anchor in lab");
        }

        if (facts.RecentAttempts >= RecentWindow && facts.RecentLikelyHits >= GoodRunHits
            && facts.LastLikelyHit)
        {
            tips.Add("Great job on those squirtles!");
        }

        if (facts.OffsetsShared)
        {
            tips.Add("You can train audio/visual offsets independently. "
                + "Most people will not have the same offset for each");
        }

        if (!facts.CaptureOn)
        {
            tips.Add("You can use a Browser Capture in OBS for your stat box!");
        }

        if (facts.RapidTriple)
        {
            tips.Add("Lab streams are in fact content");
        }

        if (facts.MinutesSinceLastRun is { } gap)
        {
            if (gap is >= ReviveTipFromMinutes and <= ReviveTipToMinutes)
            {
                tips.Add("Grabbing the revive in Mt Moon can be a great safety strat!");
            }

            if (gap is >= SoonTipFromMinutes and <= SoonTipToMinutes)
            {
                tips.Add("Back so soon? Was it Liam?");
            }

            if (gap is >= KicksTipFromMinutes and <= KicksTipToMinutes)
            {
                tips.Add("Missing kicks is bad.");
            }

            if (gap < FeelingTipMinutes && facts.HitChance > FeelingTipChance)
            {
                tips.Add("This one's it, just have a feeling.");
            }
        }

        if (facts.TrainerIdLastSeen is { } seen)
        {
            tips.Add(string.Format(CultureInfo.InvariantCulture,
                "Yes, you have gotten this TID before, on {0:yyyy-MM-dd}", seen));
        }

        return tips;
    }

    public static IReadOnlyList<string> General(in TipFacts facts)
    {
        var tips = new List<string>
        {
            "Light mode exists, if for some reason you want that",
            "You can copy your stats to clipboard, vertical or horizontal, for interactive notes!",
            "You can save different constraint filters for different categories!",
            "You can toggle off global hotkeys during your run after you hit your squirtle",
            "Hit prediction can be off slightly due to computer delay",
            "Did you know fence guy can roll RNG differently based on audio lag?",
            "If you're reading this, your run has now been blessed.",
            "Sometimes this tip window has funny jokes. I couldn't come up with one this time.",
            "Good luck!",
        };

        if (facts.HiddenRolls > 0)
        {
            tips.Add(string.Format(CultureInfo.InvariantCulture,
                "Context manip has tracked {0} undetectable movements from NPCs. Wow.",
                facts.HiddenRolls));
        }

        if (facts.Attempts > 0)
        {
            tips.Add(string.Format(CultureInfo.InvariantCulture,
                "You've likely hit {0} squirtles out of {1}", facts.LikelyHits, facts.Attempts));
        }

        return tips;
    }

    public static int? SuggestedOffsetMs(IEnumerable<TipAttempt> recent, int currentOffsetMs, double fps)
    {
        if (fps <= 0.0) return null;

        var tuner = new OffsetTuner();
        foreach (TipAttempt attempt in recent)
        {
            if (attempt.DeltaMs is not { } delta) continue;

            tuner.Observe((delta - (attempt.OffsetMs - currentOffsetMs)) / 1000.0 * fps);
        }

        if (tuner.Observations == 0) return null;

        int recommended = tuner.RecommendedOffsetMs(currentOffsetMs, fps);
        return recommended == currentOffsetMs ? null : recommended;
    }
}
