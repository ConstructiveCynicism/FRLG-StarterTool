using System.Text.Json.Serialization;

namespace FRLG.StarterTool.Core.Settings;

public sealed class Hotkey
{
    public int Primary { get; set; }

    public int Secondary { get; set; }

    public bool Global { get; set; }

    [JsonIgnore]
    public bool IsBound => Primary != 0 || Secondary != 0;

    public bool Matches(int keyCode) => keyCode != 0 && (Primary == keyCode || Secondary == keyCode);

    public void ClearOne()
    {
        if (Secondary != 0)
        {
            Secondary = 0;
        }
        else
        {
            Primary = 0;
        }
    }
}

public enum KeyMethod
{
    OnPress = 0,
    OnRelease = 1
}
