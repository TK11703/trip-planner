# Contract: Map Output Behavior (Provider-Aware Globe)

Governs how the location (globe) button in `TrackedItemForm` opens the entered address, based on the profile's `MapProvider`.

## Behavior

- The globe remains an anchor that opens in a new browser context: `target="_blank" rel="noopener noreferrer"` (unchanged from feature 018).
- The destination URL base is chosen from the current user's `MapProvider`:

| Provider | URL |
|----------|-----|
| `Bing` (default) | `https://www.bing.com/maps?q={query}` |
| `Google` | `https://www.google.com/maps/search/?api=1&query={query}` |

- `{query}` = `Uri.EscapeDataString(location.Trim())` — the free-text address as entered (unchanged rule).
- The button is disabled/enabled by the existing `IsLocationMappable` rule; only the URL base changes.

## Supplying the provider — `MapPreferenceProvider` (`TripPlanner.Web/Features/Maps/`)

A scoped service that resolves the provider once per circuit and caches it:

```csharp
public interface IMapPreferenceProvider
{
    /// <summary>Returns the user's map provider ("Bing"/"Google"), caching after first read.
    /// Falls back to Bing when the profile cannot be read.</summary>
    ValueTask<string> GetProviderAsync(CancellationToken ct = default);

    /// <summary>Clears the cache so the next read reloads (call after the profile is saved).</summary>
    void Invalidate();
}
```

- Backed by the existing `IProfileApiClient.GetAsync()`.
- `TrackedItemForm` reads the provider (e.g., in `OnInitializedAsync`) and builds `MapUrl` accordingly; on any failure it uses `MapProviders.Bing`.
- `Profile.razor` calls `Invalidate()` after a successful save so a changed default takes effect without a full reload.

## Accessibility & safety (unchanged)

- Existing `title`/`aria-label` ("Open location in a map") are retained for both providers.
- New-context opening keeps `rel="noopener noreferrer"`.

## Tests (bUnit)

- With profile `Bing` (or unreadable profile), the globe href starts with `https://www.bing.com/maps?q=`.
- With profile `Google`, the globe href starts with `https://www.google.com/maps/search/?api=1&query=`.
- The query is the escaped, trimmed location text in both cases.
- Disabled state (no/invalid location) is unchanged.
