using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Produces a deterministic deduplication hash for a relayed message so that the same message
/// delivered more than once never yields a second set of drafts (FR-019, FR-020, SC-005).
/// </summary>
public sealed class EmailDeduplicationService
{
    /// <summary>
    /// Hashes the originating message identifier when the relay supplied one — that is the only
    /// value guaranteed stable across a redelivery or a retry after a partial failure. When it is
    /// absent, falls back to the message's own characteristics: sender, subject, receipt time
    /// (truncated to the minute to tolerate clock skew), and the length and content of the body,
    /// so two genuinely different messages that share a sender and subject stay distinct.
    /// </summary>
    public string ComputeHash(string? messageId, string sender, string subject, DateTimeOffset receivedAt, string? bodyText)
    {
        var normalized = string.IsNullOrWhiteSpace(messageId)
            ? string.Join('|',
                "content",
                sender.Trim().ToLowerInvariant(),
                subject.Trim().ToLowerInvariant(),
                receivedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm"),
                (bodyText ?? string.Empty).Length.ToString(),
                Hash(bodyText ?? string.Empty))
            : $"message-id|{messageId.Trim()}";

        return Hash(normalized);
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
