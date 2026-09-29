using System.Net;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public sealed record FavoriteDestinationImportProcessingResult(
    HttpStatusCode StatusCode,
    FavoriteDestinationImportResponse Response);

public sealed record FavoriteDestinationImportCompletionResult(
    HttpStatusCode StatusCode,
    FavoriteDestinationImportJobDto? Import,
    ApiError? Error);

public sealed class FavoriteDestinationImportService
{
    public const int MaximumRows = 1000;
    private const int MaximumAddressLength = 1000;

    private readonly FavoriteDestinationImportParser _parser;
    private readonly FavoriteDestinationValidator _validator;
    private readonly IFavoriteDestinationRepository _favorites;
    private readonly IFavoriteDestinationImportRepository _imports;
    private readonly FavoriteDestinationImportSignal _signal;

    public FavoriteDestinationImportService(
        FavoriteDestinationImportParser parser,
        FavoriteDestinationValidator validator,
        IFavoriteDestinationRepository favorites,
        IFavoriteDestinationImportRepository imports,
        FavoriteDestinationImportSignal signal)
    {
        _parser = parser;
        _validator = validator;
        _favorites = favorites;
        _imports = imports;
        _signal = signal;
    }

    // Validates the whole file up front, then queues address resolution for the background worker.
    public async Task<FavoriteDestinationImportProcessingResult> QueueAsync(
        string ownerUserId,
        string fileName,
        Stream content,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var parsed = await _parser.ParseAsync(fileName, content, cancellationToken);
        if (!parsed.IsValid)
        {
            return new FavoriteDestinationImportProcessingResult(
                HttpStatusCode.BadRequest,
                new FavoriteDestinationImportResponse(
                    null,
                    parsed.Errors.Select(error => new FavoriteDestinationImportIssue(error.RowNumber, error.Message)).ToArray()));
        }

        var errors = new List<FavoriteDestinationImportIssue>();
        var rows = new List<FavoriteDestinationImportRowInput>(parsed.Rows.Count);
        foreach (var row in parsed.Rows)
        {
            var validation = _validator.Validate(row.Record.Name, row.Record.Address);
            if (!validation.IsValid)
            {
                foreach (var field in validation.Error!.Details ?? new Dictionary<string, string>())
                {
                    errors.Add(new FavoriteDestinationImportIssue(row.RowNumber, field.Value));
                }
            }

            AddLengthIssue(row.RowNumber, "name", row.Record.Name, 200, errors);
            AddLengthIssue(row.RowNumber, "address", row.Record.Address, MaximumAddressLength, errors);
            AddLengthIssue(row.RowNumber, "notes", row.Record.Notes, 4000, errors);
            if (errors.Any(error => error.RowNumber == row.RowNumber))
            {
                continue;
            }

            rows.Add(new FavoriteDestinationImportRowInput(
                row.RowNumber,
                row.Record.Name!.Trim(),
                row.Record.Address!.Trim(),
                Normalize(row.Record.Notes)));
        }

        if (errors.Count > 0)
        {
            return new FavoriteDestinationImportProcessingResult(
                HttpStatusCode.UnprocessableEntity,
                new FavoriteDestinationImportResponse(null, errors));
        }

        var job = await _imports.CreateAsync(ownerUserId, fileName, rows, nowUtc, cancellationToken);
        _signal.Notify();
        return new FavoriteDestinationImportProcessingResult(
            HttpStatusCode.Accepted,
            new FavoriteDestinationImportResponse(ToDto(job, []), Array.Empty<FavoriteDestinationImportIssue>()));
    }

    public async Task<FavoriteDestinationImportJobDto?> GetAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default)
    {
        var job = await _imports.GetAsync(ownerUserId, importId, cancellationToken);
        return job is null ? null : await ToDtoAsync(job, cancellationToken);
    }

    public async Task<IReadOnlyList<FavoriteDestinationImportJobDto>> GetOpenAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _imports.GetOpenAsync(ownerUserId, cancellationToken);
        var result = new List<FavoriteDestinationImportJobDto>(jobs.Count);
        foreach (var job in jobs)
        {
            result.Add(await ToDtoAsync(job, cancellationToken));
        }
        return result;
    }

    public Task<bool> DiscardAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default)
        => _imports.DeleteAsync(ownerUserId, importId, cancellationToken);

    public async Task<FavoriteDestinationImportCompletionResult> CompleteAsync(
        string ownerUserId,
        Guid importId,
        CompleteFavoriteDestinationImportRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var job = await _imports.GetAsync(ownerUserId, importId, cancellationToken);
        if (job is null)
        {
            return new FavoriteDestinationImportCompletionResult(HttpStatusCode.NotFound, null, ApiError.NotFoundOrDenied());
        }
        if (job.Status != FavoriteDestinationImportStatus.NeedsReview)
        {
            return new FavoriteDestinationImportCompletionResult(HttpStatusCode.Conflict, await ToDtoAsync(job, cancellationToken), null);
        }

        var rows = await _imports.GetRowsAsync(ownerUserId, importId, cancellationToken);
        var selectionErrors = new Dictionary<string, string>();
        var selections = new Dictionary<int, int?>();
        foreach (var selection in request.Selections ?? [])
        {
            if (!selections.TryAdd(selection.RowNumber, selection.CandidateIndex))
            {
                selectionErrors[$"row{selection.RowNumber}"] = $"Row {selection.RowNumber} has more than one selection.";
            }
        }

        var favorites = new List<(int RowNumber, CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Status != FavoriteDestinationImportRowStatus.Ambiguous)
            {
                favorites.Add((row.RowNumber, new CreateFavoriteDestinationRequest(row.Name, row.Address ?? row.SubmittedAddress, row.Notes), row.Location));
                continue;
            }

            if (!selections.TryGetValue(row.RowNumber, out var candidateIndex))
            {
                selectionErrors[$"row{row.RowNumber}"] = $"Row {row.RowNumber}: choose which {row.Name} to import.";
                continue;
            }
            if (candidateIndex is { } index && (index < 0 || index >= row.Candidates.Count))
            {
                selectionErrors[$"row{row.RowNumber}"] = $"Row {row.RowNumber}: the selected place is not one of the options.";
                continue;
            }

            var candidate = candidateIndex is { } chosen ? row.Candidates[chosen] : null;
            var address = candidate is not null
                && row.UsesResolvedAddress
                && !string.IsNullOrWhiteSpace(candidate.Address)
                && candidate.Address.Length <= MaximumAddressLength
                    ? candidate.Address.Trim()
                    : row.SubmittedAddress;
            var location = candidate is null
                ? null
                : new PlaceAddressComponents(candidate.City, candidate.Country, candidate.Latitude, candidate.Longitude, candidate.Address);
            favorites.Add((row.RowNumber, new CreateFavoriteDestinationRequest(row.Name, address, row.Notes), location));
        }

        if (selectionErrors.Count > 0)
        {
            return new FavoriteDestinationImportCompletionResult(
                HttpStatusCode.UnprocessableEntity,
                null,
                new ApiError("favorite_import_selection_invalid", "Choose a place for every flagged row.", selectionErrors));
        }

        var duplicates = await FindPossibleDuplicatesAsync(ownerUserId, favorites.Select(item => (item.RowNumber, item.Request)).ToArray(), cancellationToken);
        if (duplicates.Count > 0 && !request.ConfirmPossibleDuplicates)
        {
            return new FavoriteDestinationImportCompletionResult(
                HttpStatusCode.Conflict,
                ToDto(job, rows) with { PossibleDuplicates = duplicates },
                null);
        }

        var inserted = await _imports.CompleteAsync(
            ownerUserId,
            importId,
            FavoriteDestinationImportStatus.NeedsReview,
            favorites.Select(item => (item.Request, item.Location)).ToArray(),
            nowUtc,
            cancellationToken);
        var current = await _imports.GetAsync(ownerUserId, importId, cancellationToken);
        if (current is null)
        {
            return new FavoriteDestinationImportCompletionResult(HttpStatusCode.NotFound, null, ApiError.NotFoundOrDenied());
        }

        return inserted is null
            ? new FavoriteDestinationImportCompletionResult(HttpStatusCode.Conflict, await ToDtoAsync(current, cancellationToken), null)
            : new FavoriteDestinationImportCompletionResult(HttpStatusCode.OK, ToDto(current, []), null);
    }

    // Called by the background processor once every row has been resolved.
    internal async Task FinishProcessingAsync(
        FavoriteDestinationImportJob job,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var rows = await _imports.GetRowsAsync(job.OwnerUserId, job.ImportId, cancellationToken);
        var resolved = rows
            .Where(row => row.Status == FavoriteDestinationImportRowStatus.Resolved)
            .Select(row => (row.RowNumber, Request: new CreateFavoriteDestinationRequest(row.Name, row.Address ?? row.SubmittedAddress, row.Notes), row.Location))
            .ToArray();
        var duplicates = await FindPossibleDuplicatesAsync(
            job.OwnerUserId,
            resolved.Select(item => (item.RowNumber, item.Request)).ToArray(),
            cancellationToken);

        if (duplicates.Count > 0 || rows.Any(row => row.Status == FavoriteDestinationImportRowStatus.Ambiguous))
        {
            await _imports.MarkNeedsReviewAsync(job.ImportId, duplicates.Select(duplicate => duplicate.RowNumber).ToArray(), nowUtc, cancellationToken);
            return;
        }

        await _imports.CompleteAsync(
            job.OwnerUserId,
            job.ImportId,
            FavoriteDestinationImportStatus.Processing,
            resolved.Select(item => (item.Request, item.Location)).ToArray(),
            nowUtc,
            cancellationToken);
    }

    private async Task<IReadOnlyList<FavoriteDestinationImportDuplicate>> FindPossibleDuplicatesAsync(
        string ownerUserId,
        IReadOnlyList<(int RowNumber, CreateFavoriteDestinationRequest Request)> rows,
        CancellationToken cancellationToken)
    {
        var duplicates = new List<FavoriteDestinationImportDuplicate>();
        var seenInBatch = new HashSet<(string Name, string Address)>(FavoriteDestinationKeyComparer.Instance);
        foreach (var (rowNumber, request) in rows)
        {
            var isDuplicate = !seenInBatch.Add((request.Name!, request.Address!))
                || (await _favorites.FindPossibleDuplicatesAsync(ownerUserId, request.Name, request.Address, null, cancellationToken)).Count > 0;
            if (isDuplicate)
            {
                duplicates.Add(new FavoriteDestinationImportDuplicate(rowNumber, request.Name!, request.Address!));
            }
        }
        return duplicates;
    }

    private async Task<FavoriteDestinationImportJobDto> ToDtoAsync(FavoriteDestinationImportJob job, CancellationToken cancellationToken)
        => ToDto(job, job.Status == FavoriteDestinationImportStatus.NeedsReview
            ? await _imports.GetRowsAsync(job.OwnerUserId, job.ImportId, cancellationToken)
            : []);

    private static FavoriteDestinationImportJobDto ToDto(FavoriteDestinationImportJob job, IReadOnlyList<FavoriteDestinationImportRowState> rows)
        => new(
            job.ImportId,
            job.FileName,
            job.Status,
            job.TotalRows,
            job.ProcessedRows,
            job.ImportedCount,
            job.ErrorMessage,
            rows.Where(row => row.Status == FavoriteDestinationImportRowStatus.Ambiguous)
                .Select(row => new FavoriteDestinationImportAmbiguity(row.RowNumber, row.Name, row.SubmittedAddress, row.Candidates))
                .ToArray(),
            rows.Where(row => row.IsPossibleDuplicate)
                .Select(row => new FavoriteDestinationImportDuplicate(row.RowNumber, row.Name, row.Address ?? row.SubmittedAddress))
                .ToArray(),
            job.CreatedAtUtc,
            job.UpdatedAtUtc);

    private static void AddLengthIssue(int rowNumber, string fieldName, string? value, int maximumLength, ICollection<FavoriteDestinationImportIssue> errors)
    {
        if (value?.Length > maximumLength)
        {
            errors.Add(new FavoriteDestinationImportIssue(rowNumber, $"'{fieldName}' must be {maximumLength} characters or fewer."));
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FavoriteDestinationKeyComparer : IEqualityComparer<(string Name, string Address)>
    {
        public static FavoriteDestinationKeyComparer Instance { get; } = new();

        public bool Equals((string Name, string Address) x, (string Name, string Address) y)
            => string.Equals(x.Name.Trim(), y.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Address.Trim(), y.Address.Trim(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, string Address) value)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name.Trim()),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Address.Trim()));
    }
}