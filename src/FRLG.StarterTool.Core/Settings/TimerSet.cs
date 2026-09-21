using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.Core.Settings;

public sealed class FixedTimerEntry
{
    public string Name { get; set; } = "Timer";

    public string Offsets { get; set; } = "5000";

    public string Interval { get; set; } = "500";

    public string NumBeeps { get; set; } = "5";

    public FixedTimerEntry Clone() => (FixedTimerEntry)MemberwiseClone();

    public FixedTimerEntry Normalize()
    {
        Name ??= "";
        Offsets ??= "";
        Interval ??= "";
        NumBeeps ??= "";
        return this;
    }
}

public sealed class TimerSet
{
    public const string UnitMs = "ms";
    public const string UnitFrames = "frames";

    public string Name { get; set; } = "";

    public string Unit { get; set; } = UnitMs;

    public List<FixedTimerEntry> Timers { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public FixedTargetUnit TargetUnit
    {
        get => Unit == UnitFrames ? FixedTargetUnit.Frames : FixedTargetUnit.Milliseconds;
        set => Unit = value == FixedTargetUnit.Frames ? UnitFrames : UnitMs;
    }

    public static bool NameEquals(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    public TimerSet Clone() => new()
    {
        Name = Name,
        Unit = Unit,
        Timers = Timers.Select(timer => timer.Clone()).ToList()
    };

    public TimerSet Normalize()
    {
        Name = (Name ?? "").Trim();
        Unit = Unit == UnitFrames ? UnitFrames : UnitMs;
        Timers ??= new List<FixedTimerEntry>();
        Timers.RemoveAll(timer => timer == null);
        foreach (FixedTimerEntry timer in Timers) timer.Normalize();
        return this;
    }

    public bool SameTimersAs(TimerSet other)
    {
        if (Unit != other.Unit || Timers.Count != other.Timers.Count) return false;

        for (int i = 0; i < Timers.Count; i++)
        {
            FixedTimerEntry a = Timers[i], b = other.Timers[i];
            if (a.Name != b.Name || a.Offsets != b.Offsets || a.Interval != b.Interval || a.NumBeeps != b.NumBeeps)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class IgtTimerEntry
{
    public string Name { get; set; } = "Timer";
    public string Frame { get; set; } = "0";
    public string Offsets { get; set; } = "0";
    public string Interval { get; set; } = "250";
    public string NumBeeps { get; set; } = "3";

    public IgtTimerEntry Clone() => (IgtTimerEntry)MemberwiseClone();

    public IgtTimerEntry Normalize()
    {
        Name ??= "";
        Frame ??= "";
        Offsets ??= "";
        Interval ??= "";
        NumBeeps ??= "";
        return this;
    }
}

public sealed class IgtTimersFile
{
    public List<IgtTimerEntry> Timers { get; set; } = new();
}

public sealed class IgtDelayer
{
    public string Name { get; set; } = "";
    public double Delay { get; set; }
}

public sealed class IgtGame
{
    public string Game { get; set; } = "";
    public List<IgtDelayer> Delayers { get; set; } = new();
}

public sealed class IgtDelayersFile
{
    public List<IgtGame> Games { get; set; } = new();

    public static IgtDelayersFile Defaults() => new()
    {
        Games =
        {
            new IgtGame
            {
                Game = "Red/Blue",
                Delayers =
                {
                    new IgtDelayer { Name = "Encounter", Delay = 9.10 },
                    new IgtDelayer { Name = "Encounter (forest)", Delay = 6.35 },
                    new IgtDelayer { Name = "Heal/Swap (outdoors)", Delay = 6.60 },
                    new IgtDelayer { Name = "Heal/Swap (indoors)", Delay = 3.90 },
                    new IgtDelayer { Name = "Heal (in battle)", Delay = 1.05 }
                }
            },
            new IgtGame
            {
                Game = "Yellow",
                Delayers =
                {
                    new IgtDelayer { Name = "Color lag", Delay = 2.00 },
                    new IgtDelayer { Name = "Pika death", Delay = 83.00 },
                    new IgtDelayer { Name = "Encounter", Delay = 51.35 },
                    new IgtDelayer { Name = "Encounter (forest)", Delay = 15.85 },
                    new IgtDelayer { Name = "Heal (indoors)", Delay = 12.00 },
                    new IgtDelayer { Name = "Heal (in battle)", Delay = 5.00 }
                }
            }
        }
    };

    public IgtDelayersFile Normalize()
    {
        Games ??= new List<IgtGame>();
        Games.RemoveAll(game => game == null || string.IsNullOrWhiteSpace(game.Game));
        foreach (IgtGame game in Games)
        {
            game.Delayers ??= new List<IgtDelayer>();
            game.Delayers.RemoveAll(delayer => delayer == null);
            foreach (IgtDelayer delayer in game.Delayers) delayer.Name ??= "";
        }

        return this;
    }
}
