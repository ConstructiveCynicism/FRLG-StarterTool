using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRLG.StarterTool.Core.Settings;

public enum InputDevice
{
    Keyboard = 0,
    Gamepad = 1
}

[JsonConverter(typeof(InputCodeJsonConverter))]
public readonly record struct InputCode(InputDevice Device, int Pad, int Code)
{
    public static readonly InputCode None = default;

    public bool IsNone => Code == 0;

    public bool IsKeyboard => Device == InputDevice.Keyboard;

    public bool IsGamepad => Device == InputDevice.Gamepad;

    public static InputCode Key(int virtualKey) => new(InputDevice.Keyboard, 0, virtualKey);

    public static InputCode Button(int pad, int code) => new(InputDevice.Gamepad, pad, code);

    public override string ToString() => Device == InputDevice.Keyboard
        ? "K" + Code.ToString(CultureInfo.InvariantCulture)
        : "G" + Pad.ToString(CultureInfo.InvariantCulture) + ":" + Code.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out InputCode code)
    {
        code = None;
        if (string.IsNullOrEmpty(text) || text.Length < 2) return false;

        ReadOnlySpan<char> rest = text.AsSpan(1);
        switch (text[0])
        {
            case 'K' or 'k':
                if (!int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out int vk) || vk <= 0)
                {
                    return false;
                }
                code = Key(vk);
                return true;

            case 'G' or 'g':
                int colon = rest.IndexOf(':');
                if (colon <= 0) return false;
                if (!int.TryParse(rest[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out int pad)
                    || !int.TryParse(rest[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int button)
                    || pad < 0 || pad >= GamepadInput.MaxPads || button <= 0)
                {
                    return false;
                }
                code = Button(pad, button);
                return true;

            default:
                return false;
        }
    }
}

public sealed class InputCodeJsonConverter : JsonConverter<InputCode>
{
    public override InputCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && InputCode.TryParse(reader.GetString(), out InputCode code))
        {
            return code;
        }

        reader.Skip();
        return InputCode.None;
    }

    public override void Write(Utf8JsonWriter writer, InputCode value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

public static class GamepadInput
{
    public const int MaxPads = 20;

    public const int FirstJoystickPad = 4;

    public const int A = 1;
    public const int B = 2;
    public const int X = 3;
    public const int Y = 4;
    public const int LeftBumper = 5;
    public const int RightBumper = 6;
    public const int LeftTrigger = 7;
    public const int RightTrigger = 8;
    public const int Back = 9;
    public const int Start = 10;
    public const int LeftStickClick = 11;
    public const int RightStickClick = 12;
    public const int DpadUp = 13;
    public const int DpadDown = 14;
    public const int DpadLeft = 15;
    public const int DpadRight = 16;
    public const int LeftStickUp = 17;
    public const int LeftStickDown = 18;
    public const int LeftStickLeft = 19;
    public const int LeftStickRight = 20;
    public const int RightStickUp = 21;
    public const int RightStickDown = 22;
    public const int RightStickLeft = 23;
    public const int RightStickRight = 24;
    public const int Guide = 25;

    public const int AxisZMinus = 41;
    public const int AxisZPlus = 42;
    public const int AxisRMinus = 43;
    public const int AxisRPlus = 44;
    public const int AxisUMinus = 45;
    public const int AxisUPlus = 46;
    public const int AxisVMinus = 47;
    public const int AxisVPlus = 48;

    public const int FirstButton = 100;

    public const int MaxButtons = 32;

    public const int MaxCode = FirstButton + MaxButtons;

    public static string Name(int code) => code switch
    {
        A => "A",
        B => "B",
        X => "X",
        Y => "Y",
        LeftBumper => "LB",
        RightBumper => "RB",
        LeftTrigger => "LT",
        RightTrigger => "RT",
        Back => "Back",
        Start => "Start",
        LeftStickClick => "LS",
        RightStickClick => "RS",
        DpadUp => "D-Up",
        DpadDown => "D-Down",
        DpadLeft => "D-Left",
        DpadRight => "D-Right",
        LeftStickUp => "LS Up",
        LeftStickDown => "LS Down",
        LeftStickLeft => "LS Left",
        LeftStickRight => "LS Right",
        RightStickUp => "RS Up",
        RightStickDown => "RS Down",
        RightStickLeft => "RS Left",
        RightStickRight => "RS Right",
        Guide => "Guide",
        AxisZMinus => "Z−",
        AxisZPlus => "Z+",
        AxisRMinus => "R−",
        AxisRPlus => "R+",
        AxisUMinus => "U−",
        AxisUPlus => "U+",
        AxisVMinus => "V−",
        AxisVPlus => "V+",
        > FirstButton and <= MaxCode => "Btn " + (code - FirstButton).ToString(CultureInfo.InvariantCulture),
        _ => "#" + code.ToString(CultureInfo.InvariantCulture)
    };

    public static string PadName(int pad) => pad < FirstJoystickPad
        ? "P" + (pad + 1).ToString(CultureInfo.InvariantCulture)
        : "J" + (pad - FirstJoystickPad + 1).ToString(CultureInfo.InvariantCulture);
}
