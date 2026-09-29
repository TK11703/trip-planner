using System.Net;
using TripPlanner.Api.Features.Places;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public sealed record FavoriteDestinationImportProcessingResult(
    HttpStatusCode StatusCode,
    FavoriteDestinationImportResponse Response);

public sealed class FavoriteDestinationImportService
{
    public const int MaximumRows = 1000;

    private readonly FavoriteDestinationImportParser _parser;
    private readonly FavoriteDestinationValidator _validator;
    private readonly IFavoriteDestinationRepository _repository;
    private readonly IPlaceSuggestionLookup _placeLookup;
    private readonly ILogger<FavoriteDestinationImportService> _logger;

    public FavoriteDestinationImportService(
        FavoriteDestinationImportParser parser,
        FavoriteDestinationValidator validator,
        IFavoriteDestinationRepository repository,
        IPlaceSuggestionLookup placeLookup,
        ILogger<FavoriteDestinationImportService> logger)
    {
        _parser = parser;
        _validator = validator;
        _repository = repository;
        _placeLookup = placeLookup;
        _logger = logger;
    }

    public async Task<FavoriteDestinationImportProcessingResult> ImportAsync(
        string ownerUserId,
        string fileName,
        Stream content,
        bool confirmPossibleDuplicates,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var parsed = await _parser.ParseAsync(fileName, content, cancellationToken);
        if (!parsed.IsValid)
        {
            return new FavoriteDestinationImportProcessingResult(
                HttpStatusCode.BadRequest,
                new FavoriteDestinationImportResponse(
                    Array.Empty<FavoriteDestinationDto>(),
                    parsed.Errors.Select(error => new FavoriteDestinationImportIssue(error.RowNumber, error.Message)).ToArray(),
                    Array.Empty<FavoriteDestinationImportDuplicate>(),
                    false));
        }

        var errors = new List<FavoriteDestinationImportIssue>();
        var possibleDuplicates = new List<FavoriteDestinationImportDuplicate>();
        var validRows = new List<(int RowNumber, CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)>();
        var seenInBatch = new HashSet<(string Name, string Address)>(FavoriteDestinationKeyComparer.Instance);

        foreach (var row in parsed.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = _validator.Validate(row.Record.Name, row.Record.Address);
            if (!validation.IsValid)
            {
                foreach (var field in validation.Error!.Details ?? new Dictionary<string, string>())
                {
                    errors.Add(new FavoriteDestinationImportIssue(row.RowNumber, field.Value));
                }
            }

            AddLengthIssue(row.RowNumber, "name", row.Record.Name, 200, errors);
            AddLengthIssue(row.RowNumber, "address", row.Record.Address, 1000, errors);
            AddLengthIssue(row.RowNumber, "notes", row.Record.Notes, 4000, errors);
            if (errors.Any(error => error.RowNumber == row.RowNumber))
            {
                continue;
            }

            PlaceAddressComponents? components = null;
            try
            {
                components = await _placeLookup.ResolveAddressAsync(row.Record.Address!, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Favorite import address resolution failed; preserving the submitted address. Error type: {ErrorType}", exception.GetType().Name);
            }

            var request = new CreateFavoriteDestinationRequest(
                row.Record.Name!.Trim(),
                row.Record.Address!.Trim(),
                Normalize(row.Record.Notes));
            validRows.Add((row.RowNumber, request, components));

            var key = (request.Name!, request.Address!);
            if (!seenInBatch.Add(key))
            {
                possibleDuplicates.Add(new FavoriteDestinationImportDuplicate(row.RowNumber, request.Name!, request.Address!));
            }

            var existing = await _repository.FindPossibleDuplicatesAsync(ownerUserId, request.Name, request.Address, null, cancellationToken);
            if (existing.Count > 0 && possibleDuplicates.All(duplicate => duplicate.RowNumber != row.RowNumber))
            {
                possibleDuplicates.Add(new FavoriteDestinationImportDuplicate(row.RowNumber, request.Name!, request.Address!));
            }
        }

        if (errors.Count > 0)
        {
            return new FavoriteDestinationImportProcessingResult(
                HttpStatusCode.UnprocessableEntity,
                new FavoriteDestinationImportResponse(Array.Empty<FavoriteDestinationDto>(), errors, possibleDuplicates, false));
        }

        if (possibleDuplicates.Count > 0 && !confirmPossibleDuplicates)
        {
            return new FavoriteDestinationImportProcessingResult(
                HttpStatusCode.Conflict,
                new FavoriteDestinationImportResponse(Array.Empty<FavoriteDestinationDto>(), Array.Empty<FavoriteDestinationImportIssue>(), possibleDuplicates, true));
        }

        var inserted = await _repository.CreateManyAsync(ownerUserId, validRows.Select(row => (row.Request, row.Location)).ToArray(), nowUtc, cancellationToken);
        return new FavoriteDestinationImportProcessingResult(
            HttpStatusCode.OK,
            new FavoriteDestinationImportResponse(inserted, Array.Empty<FavoriteDestinationImportIssue>(), Array.Empty<FavoriteDestinationImportDuplicate>(), false));
    }

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