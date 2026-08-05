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

    public static InboxEmailDto ToDto(this InboxEmailSummary summary) => new(
        summary.InboxEmailId,
        summary.Sender,
        summary.Subject,
        summary.ReceivedAt,
        ParseParseStatus(summary.ParseStatus));

    public static ParsedItemDraftDto ToDto(this ParsedItemDraftRecord record, DraftPlacement? placement = null) => new(
        record.ParsedItemDraftId,
        record.InboxEmailId,
        record.TripId,
        record.TripLegId,
        record.ItemType,
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
        record.CreatedAtUtc,
        placement);

    private static ParseStatus ParseParseStatus(string value) => value switch
    {
        "parsed" => ParseStatus.Parsed,
        "unsupported" => ParseStatus.Unsupported,
        _ => ParseStatus.Failed
    };

    private static ReviewStatus ParseReviewStatus(string value) => value switch
    {
        "confirmed" => ReviewStatus.Confirmed,
        "discarded" => ReviewStatus.Discarded,
        _ => ReviewStatus.PendingReview
    };
}
