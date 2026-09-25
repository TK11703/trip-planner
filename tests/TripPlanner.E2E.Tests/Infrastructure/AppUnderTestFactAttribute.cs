using Xunit;
using Xunit.Sdk;

namespace TripPlanner.E2E.Tests.Infrastructure;

/// <summary>
/// A fact that only runs when an application under test was supplied.
///
/// xUnit v2 has no runtime skip, but <see cref="FactAttribute.Skip"/> is read at discovery — so
/// setting it in the constructor reports these as skipped rather than failed when no URL is
/// configured. That keeps `dotnet test` on the solution honest for everyone: the browser suite
/// announces itself as not-run instead of either failing the build or silently passing.
/// </summary>
public sealed class AppUnderTestFactAttribute : FactAttribute
{
    public AppUnderTestFactAttribute()
    {
        if (!E2EEnvironment.IsConfigured)
        {
            Skip = E2EEnvironment.SkipReason;
        }
    }
}

/// <inheritdoc cref="AppUnderTestFactAttribute"/>
public sealed class AppUnderTestTheoryAttribute : TheoryAttribute
{
    public AppUnderTestTheoryAttribute()
    {
        if (!E2EEnvironment.IsConfigured)
        {
            Skip = E2EEnvironment.SkipReason;
        }
    }
}
