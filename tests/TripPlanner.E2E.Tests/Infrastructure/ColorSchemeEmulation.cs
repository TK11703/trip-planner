namespace TripPlanner.E2E.Tests.Infrastructure;

public enum EmulatedColorScheme
{
    Light,
    Dark
}

public static class ColorSchemeEmulation
{
    public static string ToCssMediaFeatureValue(this EmulatedColorScheme scheme)
        => scheme == EmulatedColorScheme.Dark ? "dark" : "light";
}
