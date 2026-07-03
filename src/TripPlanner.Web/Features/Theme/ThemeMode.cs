namespace TripPlanner.Web.Features.Theme;

public enum ThemeMode
{
    Light,
    Dark
}

public enum ThemeModeSource
{
    DeviceBrowser,
    AccountPreference,
    TemporaryVisitorChoice
}

public static class ThemeModeNames
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string AccountPreference = "accountPreference";
    public const string DeviceBrowser = "deviceBrowser";
    public const string TemporaryVisitorChoice = "temporaryVisitorChoice";

    public static string ToCssValue(this ThemeMode mode) => mode == ThemeMode.Dark ? Dark : Light;
    public static string ToSourceValue(this ThemeModeSource source) => source switch
    {
        ThemeModeSource.AccountPreference => AccountPreference,
        ThemeModeSource.TemporaryVisitorChoice => TemporaryVisitorChoice,
        _ => DeviceBrowser
    };

    public static ThemeMode FromCssValue(string? value) =>
        string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase) ? ThemeMode.Dark : ThemeMode.Light;
}
