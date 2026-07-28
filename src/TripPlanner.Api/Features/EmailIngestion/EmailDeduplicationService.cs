using System.Security.Cryptography;
using System.Text;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Produces a deterministic deduplication hash for an incoming email so that the same
/// confirmation forwarded more than once is never stored twice (FR-007 / SC-004).
/// </summary>
public sealed class EmailDeduplicationService
{
    /// <summary>
    /// Returns a hex SHA-256 hash of <c>sender|subject|receivedAt</c> (ISO-8601 date portion).
    /// The date is truncated to the minute so minor clock skew between forwards is tolerated.
    /// </summary>
    public string ComputeHash(string sender, string subject, DateTimeOffset receivedAt)
    {
        var normalized = $"{sender.Trim().ToLowerInvariant()}|{subject.Trim().ToLowerInvariant()}|{receivedAt:yyyy-MM-ddTHH:mm}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
