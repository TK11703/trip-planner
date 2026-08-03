using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>Why a sender address could or could not be attributed to a traveler.</summary>
public enum SenderResolutionOutcome
{
    /// <summary>Exactly one traveler owns the address.</summary>
    Resolved,

    /// <summary>No traveler profile carries the address.</summary>
    NotFound,

    /// <summary>More than one traveler profile carries the address, so ownership is ambiguous.</summary>
    Ambiguous
}

/// <summary>The result of attributing a relayed message to a traveler.</summary>
public sealed record SenderResolution(SenderResolutionOutcome Outcome, string? UserId, string NormalizedSender);

/// <summary>
/// Resolves the traveler that owns a relayed message from the message sender.
///
/// The relay authenticates as an application, so the caller's token can never identify the
/// traveler. Attribution therefore comes from the sender address matched against the traveler's
/// profile email. Anything other than exactly one match is rejected rather than guessed.
/// </summary>
public sealed class EmailSenderResolver
{
    private readonly IUserProfileRepository _profiles;

    public EmailSenderResolver(IUserProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<SenderResolution> ResolveAsync(string sender, CancellationToken ct = default)
    {
        var normalized = Normalize(sender);
        if (normalized.Length == 0)
        {
            return new SenderResolution(SenderResolutionOutcome.NotFound, null, normalized);
        }

        var matches = await _profiles.FindUserIdsByEmailAsync(normalized, ct);
        return matches.Count switch
        {
            1 => new SenderResolution(SenderResolutionOutcome.Resolved, matches[0], normalized),
            0 => new SenderResolution(SenderResolutionOutcome.NotFound, null, normalized),
            _ => new SenderResolution(SenderResolutionOutcome.Ambiguous, null, normalized)
        };
    }

    /// <summary>
    /// Trims, lowercases, and unwraps an RFC 5322 display-name form such as
    /// <c>Ada Lovelace &lt;ada@contoso.com&gt;</c> down to the bare address.
    /// </summary>
    public static string Normalize(string? sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return string.Empty;
        }

        var value = sender.Trim();
        var open = value.LastIndexOf('<');
        var close = value.LastIndexOf('>');
        if (open >= 0 && close > open)
        {
            value = value[(open + 1)..close];
        }

        return value.Trim().ToLowerInvariant();
    }
}
