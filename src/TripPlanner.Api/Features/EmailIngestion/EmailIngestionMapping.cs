using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

internal static class EmailIngestionMapping
{
    public static InboxEmailDto ToDto(this InboxEmailRecord record) => new(
        record.InboxEmailId,
        record.Sender,
        record.Subject,
        record.ReceivedAt,
        ParseParseStatus(record.ParseStatus));

    public static ParsedEventDraftDto ToDto(this ParsedEventDraftRecord record) => new(
        record.ParsedEventDraftId,
        record.InboxEmailId,
        record.TripId,
        record.TripLegId,
        record.EventType,
        record.Title,
        record.Location,
        record.StartLocal,
        record.StartTimeZoneId,
        record.EndLocal,
        record.EndTimeZoneId,
        record.ConfirmationCode,
        record.Notes,
        record.Confidence,
        ParseReviewStatus(record.ReviewStatus),
        record.CreatedAtUtc);

    private static ParseStatus ParseParseStatus(string value) => value switch
    {
        "parsed" => ParseStatus.Parsed,
        "failed" => ParseStatus.Failed,
        "unsupported" => ParseStatus.Unsupported,
        _ => ParseStatus.Pending
    };

    private static ReviewStatus ParseReviewStatus(string value) => value switch
    {
        "confirmed" => ReviewStatus.Confirmed,
        "discarded" => ReviewStatus.Discarded,
        _ => ReviewStatus.PendingReview
    };
}
