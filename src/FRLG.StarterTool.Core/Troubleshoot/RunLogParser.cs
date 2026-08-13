using System.Globalization;
using System.Text.RegularExpressions;

using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.Core.Troubleshoot;

public static class RunLogParser
{
    public static IReadOnlyList<RunRecord> ReadFolder(string directory, int limit = 200)
    {
        var records = new List<RunRecord>();

        try
        {
            IEnumerable<FileInfo> files = new DirectoryInfo(directory)
                .EnumerateFiles("*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(limit);

            foreach (FileInfo file in files)
            {
                try
                {
                    records.Add(Parse(File.ReadAllLines(file.FullName), file.Name, file.FullName,
                        file.LastWriteTime));
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception)
        {
        }

        return records;
    }

    public static RunRecord Parse(IEnumerable<string> lines, string fileName = "", string path = "",
        DateTime started = default)
    {
        int seed = 0;
        int manual = 0;
        int labWindow = 0;
        int? labPress = null;
        int? correction = null;
        int? effective = null;
        int? landed = null;
        int? target = null;
        int? oakPress = null;
        double fps = RunRecord.DefaultFps;
        int labFocus = 0;
        int refused = 0;
        string outcome = "";

        var fence = new List<FenceRow>();
        var lab = new List<LabRow>();
        var taps = new List<Direction>();

        foreach (string raw in lines)
        {
            string line = Strip(raw);

            Match match;

            if ((match = FieldPattern.Match(line)).Success)
            {
                seed = Int(match, 1);
                manual = Int(match, 2);

                fence.Clear();
                continue;
            }

            if ((match = FencePattern.Match(line)).Success)
            {
                fence.Add(new FenceRow(
                    Int(match, 1), Int(match, 2), Int(match, 3), Int(match, 4), Int(match, 5),
                    ParseMoves(match.Groups[6].Value, out _)));
                continue;
            }

            if ((match = LabHeaderPattern.Match(line)).Success)
            {
                labWindow = Int(match, 2);
                labPress = Int(match, 3);
                lab.Clear();
                labFocus = 0;
                continue;
            }

            if ((match = LabPattern.Match(line)).Success)
            {
                IReadOnlyList<StripMove> moves = ParseMoves(match.Groups[6].Value, out var byNpc);

                lab.Add(new LabRow(
                    Int(match, 2), Int(match, 3), Int(match, 4), Int(match, 5),
                    Pick(byNpc, NpcId.Aide),
                    Pick(byNpc, NpcId.ScientistRight),
                    match.Groups[1].Value == "*"));

                _ = moves;
                continue;
            }

            if ((match = ArmedPattern.Match(line)).Success)
            {
                target = Int(match, 1);
                correction = Int(match, 2);
                effective = Int(match, 3);

                if (double.TryParse(match.Groups[4].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double armedFps)
                    && armedFps > 0.0)
                {
                    fps = armedFps;
                }

                continue;
            }

            if ((match = LandingPattern.Match(line)).Success)
            {
                target ??= Int(match, 1);
                landed = match.Groups[2].Value == "not anchored" ? null : Int(match, 2);
                continue;
            }

            if ((match = SearchPattern.Match(line)).Success)
            {
                if (seed == 0) seed = Int(match, 1);
                continue;
            }

            if ((match = TapPattern.Match(line)).Success)
            {
                if (line.Contains("REFUSED", StringComparison.Ordinal))
                {
                    refused++;
                    continue;
                }

                Direction tapped = Directions.FromLetter(match.Groups[1].Value[0]);
                if (tapped != Direction.None) taps.Add(tapped);
                continue;
            }

            if ((match = FocusPattern.Match(line)).Success)
            {
                labFocus = Int(match, 1);
                continue;
            }

            if ((match = AnchorPattern.Match(line)).Success)
            {
                if (match.Groups[1].Value == "Oak text") oakPress = Int(match, 2);
                continue;
            }

            if ((match = StoppedPattern.Match(line)).Success)
            {
                outcome = match.Groups[1].Value.Trim();
            }
        }

        if (labFocus >= 1 && labFocus <= lab.Count)
        {
            for (int i = 0; i < lab.Count; i++) lab[i] = lab[i] with { Focused = i == labFocus - 1 };
        }

        return new RunRecord
        {
            FileName = fileName,
            Path = path,
            Started = started,
            Seed = seed,
            ManualAdvances = manual,
            Fence = fence,
            Lab = lab,
            LabWindowFrames = labWindow,
            Correction = correction,
            EffectiveFrame = effective,
            LandedFrame = landed,
            TargetFrame = target,
            Outcome = outcome,
            Taps = taps,
            RefusedTaps = refused,
            OakPressFrame = oakPress,
            LabPressFrame = labPress,
            Fps = fps
        };
    }

    private static string Strip(string line)
    {
        Match match = StampPattern.Match(line ?? "");
        return match.Success ? line![match.Length..] : (line ?? "");
    }

    private static int Int(Match match, int group) =>
        int.TryParse(match.Groups[group].Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int value)
            ? value
            : 0;

    private static IReadOnlyList<StripMove> Pick(
        Dictionary<NpcId, List<StripMove>> byNpc, NpcId npc) =>
        byNpc.TryGetValue(npc, out List<StripMove>? moves)
            ? moves
            : Array.Empty<StripMove>();

    private static IReadOnlyList<StripMove> ParseMoves(string text,
        out Dictionary<NpcId, List<StripMove>> byNpc)
    {
        var all = new List<StripMove>();
        byNpc = new Dictionary<NpcId, List<StripMove>>();

        foreach (Match match in EventPattern.Matches(text))
        {
            Direction direction = Directions.FromLetter(match.Groups[2].Value[0]);
            if (direction == Direction.None) continue;

            var move = new StripMove(Int(match, 3), direction, match.Groups[4].Value.Length == 0);
            all.Add(move);

            string name = match.Groups[1].Value.Trim();
            if (name.Length == 0) continue;

            NpcId? npc = FromShortName(name);
            if (npc is not { } id) continue;

            if (!byNpc.TryGetValue(id, out List<StripMove>? list))
            {
                byNpc[id] = list = new List<StripMove>();
            }
            list.Add(move);
        }

        return all;
    }

    private static NpcId? FromShortName(string name) => name switch
    {
        "Lady" => NpcId.SignLady,
        "Fence" => NpcId.FatMan,
        "Sci L" => NpcId.ScientistLeft,
        "Aide" => NpcId.Aide,
        "Sci R" => NpcId.ScientistRight,
        _ => null,
    };

    private static readonly Regex StampPattern =
        new(@"^\d{2}:\d{2}:\d{2}\s+", RegexOptions.Compiled);

    private static readonly Regex FieldPattern =
        new(@"^\s*seed (\d+), \+(-?\d+) manual, (\d+) candidates:", RegexOptions.Compiled);

    private static readonly Regex FencePattern =
        new(@"^\s*exit (\d+) oak (\d+)\s+respawn (\d+)\s+visible (\d+)\s+advances (\d+)\s+\[(.*)\]\s*$",
            RegexOptions.Compiled);

    private static readonly Regex LabHeaderPattern =
        new(@"^\s*(\d+) lab boxes, window (\d+) frames, press frame (\d+):", RegexOptions.Compiled);

    private static readonly Regex LabPattern =
        new(@"^\s*(\*)?.*?x(\d+)\s+lab (\d+) frozen (\d+) \(cue [+-]?\d+\)\s+advances (\d+)\s+\[(.*)\]\s*$",
            RegexOptions.Compiled);

    private static readonly Regex ArmedPattern =
        new(@"^\s*armed (\d+): correction ([+-]?\d+) -> effective frame (\d+).*?(?:fps ([\d.]+))?$",
            RegexOptions.Compiled);

    private static readonly Regex LandingPattern =
        new(@"^\s*landing on (\d+).*?likely (not anchored|\d+)", RegexOptions.Compiled);

    private static readonly Regex SearchPattern =
        new(@"^\s*search: TID (\d+)", RegexOptions.Compiled);

    private static readonly Regex StoppedPattern =
        new(@"^\s*--- run stopped:?(.*?)---", RegexOptions.Compiled);

    private static readonly Regex TapPattern =
        new(@"^\s*tap ([NSEW]) at ", RegexOptions.Compiled);

    private static readonly Regex FocusPattern =
        new(@"^\s*box (\d+) \[.*\]: \d+ candidates, advances \d+ at the close",
            RegexOptions.Compiled);

    private static readonly Regex AnchorPattern =
        new(@"^\s*anchor (house exit|Oak text|lab text) at [\d.]+ ms, frame (\d+)",
            RegexOptions.Compiled);

    private static readonly Regex EventPattern =
        new(@"([A-Za-z ]*?)([NSEW])@(\d+)(~?)", RegexOptions.Compiled);
}
