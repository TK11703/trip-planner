using Xunit;

namespace TripPlanner.E2E.Tests;

public sealed class TripDataChatFlowTests
{
    [Fact(Skip = "Playwright; requires a running AppHost and an authenticated Entra test account.")]
    public void CitationNavigationRestoresTheEpochScopedTranscriptAndOpenPane() { }

    [Fact(Skip = "Playwright; requires a running AppHost and an authenticated Entra test account.")]
    public void CitationLinksUseAuthorizedTripRoutesAtDesktopAndNarrowViewports() { }

    [Fact(Skip = "Playwright; requires a running AppHost and an authenticated Entra test account.")]
    public void RevokedCitationAccessIsDeniedWithoutLeakingTripDetails() { }
}