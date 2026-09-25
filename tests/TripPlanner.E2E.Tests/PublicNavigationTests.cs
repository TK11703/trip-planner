using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TripPlanner.E2E.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.E2E.Tests;

/// <summary>
/// The pages a visitor can reach before signing in: the landing page, the FAQ, and About.
///
/// This is the one browser flow in the suite that needs no identity at all, which makes it the
/// right place to prove the harness works — browser launch, navigation, and assertions — without
/// also depending on a tenant, test accounts, or seeded data. Everything else in this project
/// builds on what this establishes.
///
/// It runs at two viewports because the navigation is the thing being exercised, and a header
/// that works on a desktop can easily be unreachable on a phone.
/// </summary>
[Trait("Category", "E2E")]
[Collection(BrowserFixture.CollectionName)]
public class PublicNavigationTests
{
    private readonly BrowserFixture _browser;

    public PublicNavigationTests(BrowserFixture browser) => _browser = browser;

    public static TheoryData<string> Viewports() => new("desktop", "phone");

    private static ViewportScenario Resolve(string name) => name switch
    {
        "phone" => ViewportScenario.Phone,
        _ => ViewportScenario.Desktop
    };

    private const string LandingHeading = "Plan your next great trip before you pack.";

    [AppUnderTestTheory]
    [MemberData(nameof(Viewports))]
    public async Task Landing_FAQ_About_Navigate_OnMobileAndDesktop(string viewportName)
    {
        var (context, page) = await _browser.NewPageAsync(Resolve(viewportName));
        await using var _ = context;

        await page.GotoAsync(E2EEnvironment.Url("/"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        // The landing page is public: arriving must not bounce the visitor to a sign-in screen.
        Assert.StartsWith(E2EEnvironment.BaseUrl!, page.Url, StringComparison.Ordinal);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = LandingHeading }))
            .ToBeVisibleAsync();

        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" });
        await Assertions.Expect(nav).ToBeVisibleAsync();

        // FAQ
        await nav.GetByRole(AriaRole.Link, new() { Name = "FAQ", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/faq$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Frequently asked questions" }))
            .ToBeVisibleAsync();

        // About
        await nav.GetByRole(AriaRole.Link, new() { Name = "About", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/about$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "A private home for your travel plans" }))
            .ToBeVisibleAsync();

        // ...and back to where we started.
        await nav.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = LandingHeading }))
            .ToBeVisibleAsync();

        // A signed-out visitor is never offered a signed-in surface.
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Trips", Exact = true }))
            .ToHaveCountAsync(0);
    }

    /// <summary>
    /// The FAQ states plainly that it shows no personal data. Worth asserting, because it is the
    /// kind of promise a later change could quietly break.
    /// </summary>
    [AppUnderTestFact]
    public async Task PublicPagesCarryNoPersonalTripData()
    {
        var (context, page) = await _browser.NewPageAsync(ViewportScenario.Desktop);
        await using var _ = context;

        await page.GotoAsync(E2EEnvironment.Url("/faq"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        await Assertions.Expect(page.GetByText("This page does not load or display personal trip data."))
            .ToBeVisibleAsync();
    }
}
