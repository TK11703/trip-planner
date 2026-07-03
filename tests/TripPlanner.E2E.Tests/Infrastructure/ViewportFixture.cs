namespace TripPlanner.E2E.Tests.Infrastructure;

public sealed record ViewportScenario(string Name, int Width, int Height, bool IsMobile = false)
{
    public static readonly ViewportScenario Desktop = new("desktop", 1440, 1000);
    public static readonly ViewportScenario TabletPortrait = new("tablet-portrait", 820, 1180, IsMobile: true);
    public static readonly ViewportScenario TabletLandscape = new("tablet-landscape", 1180, 820, IsMobile: true);
    public static readonly ViewportScenario Phone = new("phone", 390, 844, IsMobile: true);
}

public static class ViewportFixture
{
    public static IReadOnlyList<ViewportScenario> PrimaryScenarios { get; } =
        [ViewportScenario.Desktop, ViewportScenario.TabletPortrait, ViewportScenario.TabletLandscape, ViewportScenario.Phone];
}
