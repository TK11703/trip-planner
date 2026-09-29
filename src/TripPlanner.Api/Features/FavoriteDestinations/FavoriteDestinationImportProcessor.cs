using TripPlanner.Api.Features.Places;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

/// <summary>
/// Resolves queued import rows through Google Maps links and Azure Maps, one row at a time, so a
/// large file never has to finish inside a single HTTP request.
/// </summary>
public sealed partial class FavoriteDestinationImportProcessor
{
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    public const int MaximumAttempts = 3;
    private const int MaximumCandidates = 10;
    private const int MaximumAddressLength = 1000;
    private const string FailedMessage = "The import could not be processed. Discard it and try again.";

    private readonly IFavoriteDestinationImportRepository _imports;
    private readonly FavoriteDestinationImportService _importService;
    private readonly IPlaceSuggestionLookup _placeLookup;
    private readonly GoogleMapsUrlResolver _googleMapsUrlResolver;
    private readonly IClock _clock;
    private readonly ILogger<FavoriteDestinationImportProcessor> _logger;

    public FavoriteDestinationImportProcessor(
        IFavoriteDestinationImportRepository imports,
        FavoriteDestinationImportService importService,
        IPlaceSuggestionLookup placeLookup,
        GoogleMapsUrlResolver googleMapsUrlResolver,
        IClock clock,
        ILogger<FavoriteDestinationImportProcessor> logger)
    {
        _imports = imports;
        _importService = importService;
        _placeLookup = placeLookup;
        _googleMapsUrlResolver = googleMapsUrlResolver;
        _clock = clock;
        _logger = logger;
    }

    // Returns true when an import was claimed, so the caller should look for more work right away.
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var claimedAt = _clock.UtcNow;
        var job = await _imports.ClaimNextAsync(claimedAt, claimedAt + LeaseDuration, cancellationToken);
        if (job is null)
        {
            return false;
        }

        if (job.AttemptCount > MaximumAttempts)
        {
            await _imports.MarkFailedAsync(job.ImportId, FailedMessage, _clock.UtcNow, cancellationToken);
            return true;
        }

        try
        {
            var rows = await _imports.GetRowsAsync(job.OwnerUserId, job.ImportId, cancellationToken);
            foreach (var row in rows.Where(row => row.Status == FavoriteDestinationImportRowStatus.Pending))
            {
                var resolution = await ResolveAsync(row, cancellationToken);
                var nowUtc = _clock.UtcNow;
                if (!await _imports.SaveRowResolutionAsync(job.ImportId, resolution, nowUtc, nowUtc + LeaseDuration, cancellationToken))
                {
                    return true;
                }
            }

            await _importService.FinishProcessingAsync(job, _clock.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogImportFailed(job.AttemptCount, exception.GetType().Name);
            // Earlier attempts are retried from the first unresolved row once the lease expires.
            if (job.AttemptCount >= MaximumAttempts)
            {
                await _imports.MarkFailedAsync(job.ImportId, FailedMessage, _clock.UtcNow, cancellationToken);
            }
        }

        return true;
    }

    private async Task<FavoriteDestinationImportRowResolution> ResolveAsync(
        FavoriteDestinationImportRowState row,
        CancellationToken cancellationToken)
    {
        var googlePlace = await _googleMapsUrlResolver.ResolveAsync(row.SubmittedAddress, cancellationToken);
        var lookupQuery = googlePlace.IsGoogleMapsUrl
            ? googlePlace.SearchText ?? row.Name
            : row.SubmittedAddress;

        IReadOnlyList<PlaceMatch> matches;
        try
        {
            matches = await _placeLookup.FindPlacesAsync(lookupQuery, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogLookupFailed(exception.GetType().Name);
            matches = [];
        }

        var candidates = FindSameNameCandidates(matches, lookupQuery);
        if (candidates.Count > 1)
        {
            return new FavoriteDestinationImportRowResolution(
                row.RowNumber,
                FavoriteDestinationImportRowStatus.Ambiguous,
                googlePlace.IsGoogleMapsUrl,
                null,
                null,
                candidates);
        }

        // The top fuzzy result can be a street or a similarly named place, so an exact name match wins.
        var location = candidates.Count == 1
            ? new PlaceAddressComponents(candidates[0].City, candidates[0].Country, candidates[0].Latitude, candidates[0].Longitude, candidates[0].Address)
            : matches.Count > 0 ? matches[0].Location : null;
        var address = googlePlace.IsGoogleMapsUrl
            && !string.IsNullOrWhiteSpace(location?.FreeformAddress)
            && location.FreeformAddress.Length <= MaximumAddressLength
                ? location.FreeformAddress.Trim()
                : row.SubmittedAddress;
        return new FavoriteDestinationImportRowResolution(
            row.RowNumber,
            FavoriteDestinationImportRowStatus.Resolved,
            googlePlace.IsGoogleMapsUrl,
            address,
            location,
            []);
    }

    // Several distinct places named exactly like the searched place means we cannot tell which one was meant.
    public static IReadOnlyList<FavoriteDestinationPlaceCandidate> FindSameNameCandidates(IReadOnlyList<PlaceMatch> matches, string searchedName)
    {
        var name = searchedName.Trim();
        if (name.Length == 0)
        {
            return [];
        }

        return matches
            .Where(match => string.Equals(match.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)
                && match.Location.Latitude is not null
                && match.Location.Longitude is not null)
            .DistinctBy(match => string.IsNullOrWhiteSpace(match.Location.FreeformAddress)
                ? FormattableString.Invariant($"{match.Location.Latitude:F5},{match.Location.Longitude:F5}")
                : match.Location.FreeformAddress.Trim().ToUpperInvariant())
            .Take(MaximumCandidates)
            .Select(match => new FavoriteDestinationPlaceCandidate(
                match.Name!.Trim(),
                match.Location.FreeformAddress,
                match.Location.City,
                match.Location.Country,
                match.Location.Latitude!.Value,
                match.Location.Longitude!.Value))
            .ToArray();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Favorite import processing attempt {Attempt} failed. Error type: {ErrorType}")]
    private partial void LogImportFailed(int attempt, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Favorite import place lookup failed; keeping the submitted address. Error type: {ErrorType}")]
    private partial void LogLookupFailed(string errorType);
}
