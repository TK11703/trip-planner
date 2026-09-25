namespace TripPlanner.E2E.Tests.Infrastructure;

/// <summary>
/// Where the browser tests point, and whether they run at all.
///
/// These tests drive a real browser against a running application, which is not something a unit
/// test run can conjure. Rather than failing when no application is available, they skip — so
/// `dotnet test` on the solution stays meaningful for everyone, and the browser suite becomes a
/// deliberate act: set the base URL and they run.
///
/// Locally that is the AppHost's web endpoint; in a pipeline it is the environment that was just
/// deployed and health-checked.
/// </summary>
public static class E2EEnvironment
{
    public const string BaseUrlVariable = "TRIPPLANNER_E2E_BASEURL";

    /// <summary>The application under test, or null when none was supplied.</summary>
    public static string? BaseUrl
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(BaseUrlVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('/');
        }
    }

    public static bool IsConfigured => BaseUrl is not null;

    public static string SkipReason =>
        $"No application under test. Set {BaseUrlVariable} (for example https://localhost:7203) " +
        "with the app running, and install browsers once via `pwsh playwright.ps1 install chromium`.";

    /// <summary>Absolute URL for a path on the application under test.</summary>
    public static string Url(string relativePath) =>
        $"{BaseUrl}/{relativePath.TrimStart('/')}";
}
