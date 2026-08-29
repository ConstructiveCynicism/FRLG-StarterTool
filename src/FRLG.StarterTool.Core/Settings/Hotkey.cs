using System.Text.Json.Serialization;

namespace FRLG.StarterTool.Core.Settings;

public sealed class Hotkey
{
    public List<InputChord> Chords { get; set; } = new();

    public bool Global { get; set; } = true;

    [JsonIgnore]
    public bool IsBound => Chords.Count > 0;

    public int MatchLength(InputCode trigger, Func<InputCode, bool> isDown)
    {
        int best = 0;
        foreach (InputChord chord in Chords)
        {
            if (chord.Count > best && chord.Matches(trigger, isDown)) best = chord.Count;
        }
        return best;
    }

    public bool IsDown(Func<InputCode, bool> isDown)
    {
        foreach (InputChord chord in Chords)
        {
            if (chord.AllDown(isDown)) return true;
        }
        return false;
    }

    public bool Contains(InputChord chord) => Chords.Contains(chord);

    public bool Toggle(InputChord chord)
    {
        if (chord.IsEmpty) return false;

        if (Chords.Remove(chord)) return false;

        Chords.Add(chord);
        return true;
    }

    public void Clear() => Chords.Clear();

    public void Normalize()
    {
        Chords ??= new List<InputChord>();
        var kept = new List<InputChord>(Chords.Count);
        foreach (InputChord chord in Chords)
        {
            if (chord is { IsEmpty: false } && !kept.Contains(chord)) kept.Add(chord);
        }
        Chords = kept;
    }
}

public enum KeyMethod
{
    OnPress = 0,
    OnRelease = 1
}
