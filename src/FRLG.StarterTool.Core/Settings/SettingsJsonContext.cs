using System.Text.Json.Serialization;

namespace FRLG.StarterTool.Core.Settings;

[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(FilterPreset))]
[JsonSerializable(typeof(EncounterRoutePreset))]
[JsonSerializable(typeof(TimerSet))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
