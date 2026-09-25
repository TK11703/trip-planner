using Microsoft.Playwright;

namespace TripPlanner.E2E.Tests.Infrastructure;

/// <summary>
/// One browser for the whole suite, with a fresh context per scenario.
///
/// Launching Chromium is the expensive part, so it happens once; a context is cheap and gives
/// each test its own cookies, storage, and viewport. That separation is what lets a signed-in
/// flow and an anonymous one live in the same run without leaking state between them.
///
/// The browser is only started when an application under test was supplied, so the fixture costs
/// nothing in a normal unit-test run.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        if (!E2EEnvironment.IsConfigured)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    /// <summary>
    /// A page sized for the given scenario. HTTPS errors are ignored because the local AppHost
    /// serves a development certificate the browser has no reason to trust.
    /// </summary>
    public async Task<(IBrowserContext Context, IPage Page)> NewPageAsync(ViewportScenario viewport)
    {
        if (_browser is null)
        {
            throw new InvalidOperationException(E2EEnvironment.SkipReason);
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = viewport.Width, Height = viewport.Height },
            IsMobile = false, // Chromium rejects IsMobile without touch emulation on some platforms.
            IgnoreHTTPSErrors = true
        });

        return (context, await context.NewPageAsync());
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    public const string CollectionName = "Browser";
}

/// <summary>
/// Shares the browser across every flow class. Named without a <c>Collection</c> suffix so it
/// does not trip CA1711.
/// </summary>
[CollectionDefinition(BrowserFixture.CollectionName)]
public sealed class SharedBrowser : ICollectionFixture<BrowserFixture>;
