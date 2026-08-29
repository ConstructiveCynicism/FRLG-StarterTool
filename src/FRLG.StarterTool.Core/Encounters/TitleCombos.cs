namespace FRLG.StarterTool.Core.Encounters;

public enum TitleButton
{
    A,
    Start,
    Select,
    L,
}

public readonly record struct TitleCombo(TitleButton First, TitleButton? Second = null, TitleButton? Third = null)
{
    public static TitleCombo Of(params TitleButton[] order) => order.Length switch
    {
        1 => new TitleCombo(order[0]),
        2 => new TitleCombo(order[0], order[1]),
        3 => new TitleCombo(order[0], order[1], order[2]),
        _ => throw new ArgumentException("a combo is one to three buttons", nameof(order)),
    };

    public int Count => Third is not null ? 3 : Second is not null ? 2 : 1;

    public IReadOnlyList<TitleButton> Order
    {
        get
        {
            var order = new List<TitleButton>(3) { First };
            if (Second is TitleButton second) order.Add(second);
            if (Third is TitleButton third) order.Add(third);
            return order;
        }
    }

    public bool Fits(bool skip, bool speed)
    {
        if (Count != (skip ? 1 : 0) + (speed ? 1 : 0) + 1) return false;

        IReadOnlyList<TitleButton> order = Order;
        int at = 0;
        if (skip && !TitleCombos.CanSkip(order[at++])) return false;
        if (speed && !TitleCombos.CanSpeedUp(order[at++])) return false;
        return TitleCombos.CanEnter(order[at]);
    }

    public static string Name(TitleButton button) => button switch
    {
        TitleButton.Start => "START",
        TitleButton.Select => "SELECT",
        TitleButton.L => "L",
        _ => "A",
    };

    private static string KeyOf(TitleButton button) => button switch
    {
        TitleButton.Start => "start",
        TitleButton.Select => "select",
        TitleButton.L => "l",
        _ => "a",
    };

    private static TitleButton? ButtonOf(string key) => key switch
    {
        "a" => TitleButton.A,
        "start" => TitleButton.Start,
        "select" => TitleButton.Select,
        "l" => TitleButton.L,
        _ => null,
    };

    public string Key => string.Join('>', Order.Select(KeyOf));

    public static TitleCombo? Parse(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        string[] parts = key.Trim().ToLowerInvariant().Split('>');
        if (parts.Length is < 1 or > 3) return null;

        var order = new List<TitleButton>(3);
        foreach (string part in parts)
        {
            if (ButtonOf(part.Trim()) is not TitleButton button) return null;
            if (order.Contains(button)) return null;
            order.Add(button);
        }
        return Of(order.ToArray());
    }

    public string Short => string.Join('→', Order.Select(Name));

    public override string ToString() => string.Join(" -> ", Order.Select(Name));
}

public static class TitleCombos
{
    public static bool CanSkip(TitleButton button) => true;

    public static bool CanSpeedUp(TitleButton button) => button != TitleButton.Select;

    public static bool CanEnter(TitleButton button) => button != TitleButton.Select;

    public static IReadOnlyList<TitleButton> Buttons(TitleButtonMode mode) =>
        mode == TitleButtonMode.LEqualsA
            ? new[] { TitleButton.Select, TitleButton.Start, TitleButton.A, TitleButton.L }
            : new[] { TitleButton.Select, TitleButton.Start, TitleButton.A };

    public static IReadOnlyList<TitleCombo> All(TitleButtonMode mode)
    {
        IReadOnlyList<TitleButton> buttons = Buttons(mode);
        var skips = buttons.Where(CanSkip).ToList();
        var speeds = new[] { TitleButton.Start, TitleButton.A, TitleButton.L }.Where(b => buttons.Contains(b) && CanSpeedUp(b)).ToList();
        var entersSped = new[] { TitleButton.A, TitleButton.Start, TitleButton.L }.Where(b => buttons.Contains(b) && CanEnter(b)).ToList();
        var entersPlayed = new[] { TitleButton.Start, TitleButton.A, TitleButton.L }.Where(b => buttons.Contains(b) && CanEnter(b)).ToList();

        var combos = new List<TitleCombo>();
        foreach (TitleButton skip in skips)
        foreach (TitleButton speed in speeds)
        foreach (TitleButton enter in entersSped)
        {
            if (skip == speed || skip == enter || speed == enter) continue;
            combos.Add(new TitleCombo(skip, speed, enter));
        }

        foreach (TitleButton skip in skips)
        foreach (TitleButton enter in entersPlayed)
        {
            if (skip == enter) continue;
            var combo = new TitleCombo(skip, enter);
            if (!combos.Contains(combo)) combos.Add(combo);
        }

        foreach (TitleButton enter in entersPlayed) combos.Add(new TitleCombo(enter));
        return combos;
    }

    public static TitleCombo Swept(bool skip, bool speed)
    {
        var order = new List<TitleButton>(3);
        if (skip) order.Add(TitleButton.Select);
        if (speed) order.Add(TitleButton.Start);
        order.Add(speed ? TitleButton.A : TitleButton.Start);
        return TitleCombo.Of(order.ToArray());
    }

    public static TitleCombo Swept(PressFrame press) =>
        Swept(press.Variant.IntroSkipped, press.Offset < TitleSeedTable.AnimationEndsOf(press.Variant.Game));

    public static TitleCombo Of(PressFrame press) => press.Variant.Combo ?? Swept(press);

    public static bool IsSwept(PressFrame press) => Of(press) == Swept(press);

    public static bool ReadsAsSwept(PressFrame press)
    {
        TitleCombo combo = Of(press);
        if (combo == Swept(press)) return true;

        IReadOnlyList<TitleButton> order = combo.Order;
        int at = 0;
        if (press.Variant.IntroSkipped)
        {
            TitleButton skip = order[at++];
            if (skip != TitleButton.Select && skip != TitleButton.Start) return false;
        }

        if (press.Offset >= TitleSeedTable.AnimationEndsOf(press.Variant.Game)) return order[at] == TitleButton.Start;
        return order[at] == TitleButton.A && (order[at + 1] == TitleButton.Start || order[at + 1] == TitleButton.L)
            || (order[at] == TitleButton.Start && order[at + 1] == TitleButton.A);
    }

    public static string? OwnTableKey(TitleCombo combo, bool skipped, TitleSoundMode sound = TitleSoundMode.Mono)
    {
        IReadOnlyList<TitleButton> order = combo.Order;
        int at = 0;
        if (skipped)
        {
            at = 1;
            TitleButton skip = order[0];
            if (skip == TitleButton.A || skip == TitleButton.L)
            {
                var own = order.Skip(1).ToList();
                if (own.Count is 0 or > 2) return null;
                if (skip == TitleButton.A && sound == TitleSoundMode.Mono)
                {
                    if (own.Count == 1 && own[0] == TitleButton.L) own[0] = TitleButton.Start;
                    if (own.Count == 2 && own[0] == TitleButton.Start && own[1] == TitleButton.L) own.Reverse();
                }
                return "skip" + TitleCombo.Name(skip).ToLowerInvariant() + "-"
                    + string.Join('-', own.Select(b => TitleCombo.Name(b).ToLowerInvariant()));
            }
        }
        var title = order.Skip(at).ToList();
        if (title.Count is 0 or > 2) return null;
        if (title.Count == 1 && title[0] == TitleButton.Start) return null;
        if (title.Count == 2 && (title[0] == TitleButton.A && (title[1] == TitleButton.Start || title[1] == TitleButton.L)
                                 || title[0] == TitleButton.Start && title[1] == TitleButton.A)) return null;
        return string.Join('-', title.Select(b => TitleCombo.Name(b).ToLowerInvariant()));
    }

    public static bool IsMeasured(PressFrame press) =>
        ReadsAsSwept(press)
        || press.Variant.Table.Combo is not null
        || (press.Variant.Intro == TitleIntro.Skip477 && press.Variant.Combo is not null
            && TitleSeedTable.IntroSkipShiftOf(press.Variant) is not null);

    public static IReadOnlyList<string> Describe(PressFrame press)
    {
        IReadOnlyList<TitleButton> order = Of(press).Order;
        var lines = new List<string>();
        int at = 0;

        if (press.Variant.IntroSkipped)
        {
            int first = TitleSeedTable.IntroFrameOf(press.Variant);
            int last = first + press.IntroWindow - 1;
            lines.Add($"{TitleCombo.Name(order[at++])} on frame {first}-{last} after power-on, and keep it held"
                + $" - skips the intro, anchor then at {TitleSeedTable.IntroAnchorOf(press.Variant.Intro)}");
        }

        if (press.Offset < TitleSeedTable.AnimationEndsOf(press.Variant.Game))
        {
            lines.Add($"{TitleCombo.Name(order[at])} at anchor+{press.Offset}"
                + $", {TitleCombo.Name(order[at + 1])} exactly one frame later (any other gap is another seed)");
        }
        else
        {
            lines.Add($"{TitleCombo.Name(order[at])} alone at anchor+{press.Offset} (the animation has ended)");
        }

        return lines;
    }
}
