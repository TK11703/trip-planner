using Xunit;

namespace TripPlanner.E2E.Tests;

// Feature 020: Printable Trip View — an owner opens a chrome-free printable page for a
// trip and can print it. Mirrors the repo's E2E convention (Playwright, skipped without
// a running AppHost).
public class PrintTripFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void OwnerClicksPrint_OpensChromeFreePrintPageWithLegsAndEvents() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void PrintPage_ShowsNoNavOrFooterChrome() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void BackLink_ReturnsToTripDetails() { }
}
