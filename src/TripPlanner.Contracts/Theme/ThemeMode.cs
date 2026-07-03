using System.Text.Json.Serialization;

namespace TripPlanner.Contracts.Theme;

[JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode
{
    [JsonStringEnumMemberName("light")]
    Light,

    [JsonStringEnumMemberName("dark")]
    Dark
}

[JsonConverter(typeof(JsonStringEnumConverter<ThemePreferenceSource>))]
public enum ThemePreferenceSource
{
    [JsonStringEnumMemberName("accountPreference")]
    AccountPreference,

    [JsonStringEnumMemberName("deviceBrowser")]
    DeviceBrowser
}
