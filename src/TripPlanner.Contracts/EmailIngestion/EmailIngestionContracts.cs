namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>A raw email received by the trip inbox, as returned to the API consumer.</summary>
public sealed record InboxEmailDto(
    Guid InboxEmailId,
    string Sender,
    string Subject,
    DateTimeOffset ReceivedAt,
    ParseStatus ParseStatus);

/// <summary>A structured event extracted from an inbox email, awaiting user review.</summary>
public sealed record ParsedEventDraftDto(
    Guid ParsedEventDraftId,
    Guid InboxEmailId,
    Guid? TripId,
    Guid? TripLegId,
    string? EventType,
    string? Title,
    string? Location,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    string? ConfirmationCode,
    string? Notes,
    double Confidence,
    ReviewStatus ReviewStatus,
    DateTimeOffset CreatedAt);

/// <summary>Request to update editable fields of a parsed event draft.</summary>
public sealed record UpdateParsedEventDraftRequest(
    Guid? TripId,
    Guid? TripLegId,
    string? EventType,
    string? Title,
    string? Location,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    string? ConfirmationCode,
    string? Notes);

/// <summary>Response returned after confirming a draft (the promoted event id).</summary>
public sealed record ConfirmParsedEventDraftResponse(Guid TrackedItemId, Guid TripId, Guid TripLegId);

/// <summary>A page of inbox emails.</summary>
public sealed record InboxEmailListResponse(IReadOnlyList<InboxEmailDto> Items);

/// <summary>A page of pending drafts.</summary>
public sealed record ParsedEventDraftListResponse(IReadOnlyList<ParsedEventDraftDto> Items);

/// <summary>Request payload accepted by the development-only inject endpoint.</summary>
public sealed record DevInjectEmailRequest(string Sender, string Subject, string BodyText, string? BodyHtml = null);
