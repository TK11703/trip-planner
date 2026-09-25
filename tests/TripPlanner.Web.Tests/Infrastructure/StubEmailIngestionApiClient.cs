using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;
using TripPlanner.Web.Features.EmailIngestion;

namespace TripPlanner.Web.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for the ingestion API, shared by the review-queue and draft-editor tests.
///
/// Extracted from <c>InboxReviewPageTests</c> so more than one test class can drive the same
/// behaviour, following the <c>StubTripApiClient</c> convention.
/// </summary>
internal sealed class StubEmailIngestionApiClient(IReadOnlyList<ParsedItemDraftDto> drafts) : IEmailIngestionApiClient
{
    private readonly List<ParsedItemDraftDto> _drafts = [.. drafts];

    public List<Guid> Discarded { get; } = [];

    public List<(Guid DraftId, UpdateParsedItemDraftRequest Request)> Updated { get; } = [];

    /// <summary>Set to make the next save or confirm fail the way the API would.</summary>
    public ApiError? NextError { get; set; }

    public int Fetches { get; private set; }

    /// <summary>The outcome the next confirm reports back. Tests set this to mimic a leg.</summary>
    public DraftOutcome NextConfirmOutcome { get; set; } = DraftOutcome.Item;

    public Task<IReadOnlyList<ParsedItemDraftDto>> GetDraftsAsync(CancellationToken ct = default)
    {
        Fetches++;
        return Task.FromResult<IReadOnlyList<ParsedItemDraftDto>>([.. _drafts]);
    }

    public Task<DraftMutationResult<ParsedItemDraftDto>> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default)
    {
        Updated.Add((draftId, request));
        if (NextError is { } error)
        {
            NextError = null;
            return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(null, error));
        }

        var index = _drafts.FindIndex(d => d.ParsedItemDraftId == draftId);
        if (index < 0) return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(null, ApiError.NotFoundOrDenied()));

        var updated = _drafts[index] with
        {
            TripId = request.TripId,
            TripLegId = request.TripLegId,
            ItemType = request.ItemType,
            Title = request.Title,
            Location = request.Location,
            StartLocal = request.StartLocal,
            StartTimeZoneId = request.StartTimeZoneId,
            EndLocal = request.EndLocal,
            EndTimeZoneId = request.EndTimeZoneId,
            ConfirmationCode = request.ConfirmationCode,
            Notes = request.Notes,
            ProposedOutcome = request.ProposedOutcome,
            Origin = request.Origin,
            Destination = request.Destination,
            TransportationMode = request.TransportationMode,
            TravelCost = request.TravelCost
        };
        _drafts[index] = updated;
        return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(updated, null));
    }

    public Task<DraftMutationResult<ConfirmParsedItemDraftResponse>> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        if (NextError is { } error)
        {
            NextError = null;
            return Task.FromResult(new DraftMutationResult<ConfirmParsedItemDraftResponse>(null, error));
        }

        _drafts.RemoveAll(d => d.ParsedItemDraftId == draftId);

        var response = NextConfirmOutcome == DraftOutcome.Leg
            ? new ConfirmParsedItemDraftResponse(DraftOutcome.Leg, Guid.NewGuid(), null, null, Guid.NewGuid())
            : new ConfirmParsedItemDraftResponse(DraftOutcome.Item, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

        return Task.FromResult(new DraftMutationResult<ConfirmParsedItemDraftResponse>(response, null));
    }

    public Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        Discarded.Add(draftId);
        _drafts.RemoveAll(d => d.ParsedItemDraftId == draftId);
        return Task.FromResult(true);
    }

    /// <summary>How many times re-recognition was asked for, so a test can assert "exactly once".</summary>
    public int ReRecognizeCalls { get; private set; }

    /// <summary>What re-recognition returns. Null models the provider being unavailable.</summary>
    public ParsedItemDraftDto? ReRecognizeResult { get; set; }

    /// <summary>Set to make re-recognition throw rather than return, modelling a hard failure.</summary>
    public Exception? ReRecognizeThrows { get; set; }

    public Task<ParsedItemDraftDto?> ReRecognizeDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        ReRecognizeCalls++;
        if (ReRecognizeThrows is { } ex) throw ex;
        return Task.FromResult(ReRecognizeResult);
    }
}
