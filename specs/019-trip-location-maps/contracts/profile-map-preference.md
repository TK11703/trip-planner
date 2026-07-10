# Contract: Profile Map Preference

Extends the existing profile surface (`GET/PUT /api/profile`) with a persisted default mapping tool.

## Contract types (`TripPlanner.Contracts/Profile/UserProfileContracts.cs`)

Add a `MapProvider` scalar to both the read and write records and a small constants helper.

```csharp
public sealed record UserProfileResponse(
    string UserId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    string MapProvider,                 // NEW: "Bing" | "Google" (default "Bing")
    bool IsComplete,
    NotificationPreferences NotificationPreferences,
    PersonalizationPreferences PersonalizationPreferences,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    string MapProvider,                 // NEW
    NotificationPreferences NotificationPreferences,
    PersonalizationPreferences PersonalizationPreferences);

public static class MapProviders
{
    public const string Bing = "Bing";
    public const string Google = "Google";
    public const string Default = Bing;

    /// <summary>Normalizes any input to a known provider, defaulting to Bing.</summary>
    public static string Normalize(string? value) =>
        string.Equals(value, Google, StringComparison.OrdinalIgnoreCase) ? Google : Bing;
}
```

> Note: `UpdateUserProfileRequest` currently has a positional shape. Adding `MapProvider` is a breaking positional change; update all call sites (Web `Profile.razor` mapping and API tests) accordingly.

## Endpoint behavior

### `GET /api/profile`

- Returns the persisted `MapProvider` (canonical `Bing`/`Google`). New/ensured profiles return `Bing`.

### `PUT /api/profile`

- Persists `MapProviders.Normalize(request.MapProvider)` — an unrecognized/empty value is stored as `Bing` rather than causing a validation error.
- Leaves all other profile fields governed by their existing rules.

## Validation rules

| Rule | Behavior |
|------|----------|
| Unknown/empty/whitespace `MapProvider` | Coerced to `Bing` (never rejected). |
| `Google` (any casing) | Stored as canonical `Google`. |
| `Bing` (any casing) | Stored as canonical `Bing`. |

## Persistence

- Column: `users.map_provider text NOT NULL DEFAULT 'Bing'` (migration `009_user_profile_map_provider.sql`).
- `GetUserProfile.sql` selects `map_provider AS MapProvider`.
- `UpdateUserProfile.sql` sets `map_provider = @MapProvider`.
- `EnsureUserProfileFromClaims.sql` returns `map_provider AS MapProvider`.

## Tests

- API: GET returns `Bing` for a new profile; PUT `Google` round-trips; PUT of an unknown value stores `Bing`; updating unrelated fields preserves `MapProvider`.
- Database: `map_provider` persists and reads back canonically.
