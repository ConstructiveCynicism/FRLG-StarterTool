using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRLG.StarterTool.Core.Settings;

[JsonConverter(typeof(InputChordJsonConverter))]
public sealed class InputChord : IEquatable<InputChord>
{
    private readonly InputCode[] _inputs;

    public InputChord(IEnumerable<InputCode> inputs)
    {
        var kept = new List<InputCode>();
        foreach (InputCode input in inputs)
        {
            if (!input.IsNone && !kept.Contains(input)) kept.Add(input);
        }
        _inputs = kept.ToArray();
    }

    public InputChord(params InputCode[] inputs) : this((IEnumerable<InputCode>)inputs)
    {
    }

    public IReadOnlyList<InputCode> Inputs => _inputs;

    public int Count => _inputs.Length;

    public bool IsEmpty => _inputs.Length == 0;

    public InputCode Trigger => _inputs.Length == 0 ? InputCode.None : _inputs[^1];

    public bool Contains(InputCode input) => Array.IndexOf(_inputs, input) >= 0;

    public bool Matches(InputCode trigger, Func<InputCode, bool> isDown)
    {
        if (_inputs.Length == 0 || trigger != Trigger) return false;

        for (int i = 0; i < _inputs.Length - 1; i++)
        {
            if (!isDown(_inputs[i])) return false;
        }

        return true;
    }

    public bool AllDown(Func<InputCode, bool> isDown)
    {
        if (_inputs.Length == 0) return false;

        foreach (InputCode input in _inputs)
        {
            if (!isDown(input)) return false;
        }

        return true;
    }

    public bool Equals(InputChord? other)
    {
        if (other is null || other._inputs.Length != _inputs.Length) return false;
        if (_inputs.Length == 0) return true;
        if (other.Trigger != Trigger) return false;

        for (int i = 0; i < _inputs.Length - 1; i++)
        {
            if (Array.IndexOf(other._inputs, _inputs[i], 0, other._inputs.Length - 1) < 0) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as InputChord);

    public override int GetHashCode()
    {
        int hash = Trigger.GetHashCode() * 31 + _inputs.Length;
        for (int i = 0; i < _inputs.Length - 1; i++)
        {
            hash ^= _inputs[i].GetHashCode();
        }
        return hash;
    }

    public override string ToString() => string.Join("+", _inputs);

    public static bool TryParse(string? text, out InputChord chord)
    {
        chord = new InputChord();
        if (string.IsNullOrWhiteSpace(text)) return false;

        var inputs = new List<InputCode>();
        foreach (string part in text.Split('+'))
        {
            if (!InputCode.TryParse(part.Trim(), out InputCode input)) return false;
            inputs.Add(input);
        }

        chord = new InputChord(inputs);
        return !chord.IsEmpty;
    }
}

public sealed class InputChordJsonConverter : JsonConverter<InputChord>
{
    public override InputChord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && InputChord.TryParse(reader.GetString(), out InputChord chord))
        {
            return chord;
        }

        reader.Skip();
        return new InputChord();
    }

    public override void Write(Utf8JsonWriter writer, InputChord value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
