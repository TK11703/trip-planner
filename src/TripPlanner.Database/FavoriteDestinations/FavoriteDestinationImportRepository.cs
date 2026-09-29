using System.Text.Json;
using Dapper;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.FavoriteDestinations;

public sealed class FavoriteDestinationImportRepository : IFavoriteDestinationImportRepository
{
    private const string QueryFolder = "Queries/FavoriteDestinationImports/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public FavoriteDestinationImportRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<FavoriteDestinationImportJob> CreateAsync(
        string ownerUserId,
        string fileName,
        IReadOnlyList<FavoriteDestinationImportRowInput> rows,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var importId = Guid.NewGuid();
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get(QueryFolder + "insert-import.sql"),
            new
            {
                ImportId = importId,
                OwnerUserId = ownerUserId,
                FileName = fileName,
                RowNumbers = rows.Select(row => row.RowNumber).ToArray(),
                Names = rows.Select(row => row.Name).ToArray(),
                SubmittedAddresses = rows.Select(row => row.SubmittedAddress).ToArray(),
                NotesValues = rows.Select(row => row.Notes).ToArray(),
                NowUtc = nowUtc
            },
            cancellationToken: cancellationToken));

        return new FavoriteDestinationImportJob(
            importId, ownerUserId, fileName, FavoriteDestinationImportStatus.Queued, rows.Count, 0, 0, null, 0, nowUtc, nowUtc);
    }

    public async Task<FavoriteDestinationImportJob?> GetAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default)
        => (await QueryJobsAsync(ownerUserId, importId, openOnly: false, cancellationToken)).SingleOrDefault();

    public Task<IReadOnlyList<FavoriteDestinationImportJob>> GetOpenAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
        => QueryJobsAsync(ownerUserId, importId: null, openOnly: true, cancellationToken);

    public async Task<IReadOnlyList<FavoriteDestinationImportRowState>> GetRowsAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RowRecord>(new CommandDefinition(
            _sql.Get(QueryFolder + "get-import-rows.sql"),
            new { OwnerUserId = ownerUserId, ImportId = importId },
            cancellationToken: cancellationToken));
        return rows.Select(ToRowState).ToArray();
    }

    public async Task<FavoriteDestinationImportJob?> ClaimNextAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var job = await connection.QuerySingleOrDefaultAsync<JobRecord>(new CommandDefinition(
            _sql.Get(QueryFolder + "claim-next-import.sql"),
            new { NowUtc = nowUtc, LeaseExpiresAtUtc = leaseExpiresAtUtc },
            cancellationToken: cancellationToken));
        return job is null ? null : ToJob(job);
    }

    public async Task<bool> SaveRowResolutionAsync(
        Guid importId,
        FavoriteDestinationImportRowResolution resolution,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get(QueryFolder + "save-import-row-resolution.sql"),
            new
            {
                ImportId = importId,
                resolution.RowNumber,
                Status = resolution.Status.ToString(),
                resolution.UsesResolvedAddress,
                Address = Normalize(resolution.Address),
                City = Normalize(resolution.Location?.City),
                Country = Normalize(resolution.Location?.Country),
                Latitude = resolution.Location?.Latitude,
                Longitude = resolution.Location?.Longitude,
                CandidatesJson = resolution.Candidates.Count == 0
                    ? null
                    : JsonSerializer.Serialize(resolution.Candidates, JsonOptions),
                NowUtc = nowUtc,
                LeaseExpiresAtUtc = leaseExpiresAtUtc
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> MarkNeedsReviewAsync(
        Guid importId,
        IReadOnlyCollection<int> duplicateRowNumbers,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get(QueryFolder + "mark-import-needs-review.sql"),
            new { ImportId = importId, DuplicateRowNumbers = duplicateRowNumbers.ToArray(), NowUtc = nowUtc },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task MarkFailedAsync(
        Guid importId,
        string errorMessage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get(QueryFolder + "mark-import-failed.sql"),
            new { ImportId = importId, ErrorMessage = errorMessage, NowUtc = nowUtc },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FavoriteDestinationDto>?> CompleteAsync(
        string ownerUserId,
        Guid importId,
        FavoriteDestinationImportStatus expectedStatus,
        IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favorites,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var transitioned = await connection.ExecuteAsync(new CommandDefinition(
                _sql.Get(QueryFolder + "complete-import.sql"),
                new
                {
                    OwnerUserId = ownerUserId,
                    ImportId = importId,
                    ExpectedStatus = expectedStatus.ToString(),
                    ImportedCount = favorites.Count,
                    NowUtc = nowUtc
                },
                transaction,
                cancellationToken: cancellationToken));
            if (transitioned == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            IReadOnlyList<FavoriteDestinationDto> inserted = Array.Empty<FavoriteDestinationDto>();
            if (favorites.Count > 0)
            {
                inserted = (await connection.QueryAsync<FavoriteDestinationDto>(new CommandDefinition(
                    _sql.Get(FavoriteDestinationRepository.InsertManySqlPath),
                    FavoriteDestinationRepository.InsertManyParameters(ownerUserId, favorites, nowUtc),
                    transaction,
                    cancellationToken: cancellationToken))).ToArray();
            }

            // The staged rows hold personal data that is no longer needed once the favorites exist.
            await connection.ExecuteAsync(new CommandDefinition(
                _sql.Get(QueryFolder + "delete-import-rows.sql"),
                new { ImportId = importId },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return inserted;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get(QueryFolder + "delete-import.sql"),
            new { OwnerUserId = ownerUserId, ImportId = importId },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    private async Task<IReadOnlyList<FavoriteDestinationImportJob>> QueryJobsAsync(
        string ownerUserId,
        Guid? importId,
        bool openOnly,
        CancellationToken cancellationToken)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var jobs = await connection.QueryAsync<JobRecord>(new CommandDefinition(
            _sql.Get(QueryFolder + "get-imports.sql"),
            new { OwnerUserId = ownerUserId, ImportId = importId, OpenOnly = openOnly },
            cancellationToken: cancellationToken));
        return jobs.Select(ToJob).ToArray();
    }

    private static FavoriteDestinationImportJob ToJob(JobRecord record)
        => new(
            record.ImportId,
            record.OwnerUserId,
            record.FileName,
            Enum.Parse<FavoriteDestinationImportStatus>(record.Status),
            record.TotalRows,
            record.ProcessedRows,
            record.ImportedCount,
            record.ErrorMessage,
            record.AttemptCount,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);

    private static FavoriteDestinationImportRowState ToRowState(RowRecord record)
        => new(
            record.RowNumber,
            record.Name,
            record.SubmittedAddress,
            record.Notes,
            Enum.Parse<FavoriteDestinationImportRowStatus>(record.Status),
            record.UsesResolvedAddress,
            record.Address,
            record.City is null && record.Country is null && record.Latitude is null
                ? null
                : new PlaceAddressComponents(record.City, record.Country, record.Latitude, record.Longitude, record.Address),
            string.IsNullOrWhiteSpace(record.CandidatesJson)
                ? Array.Empty<FavoriteDestinationPlaceCandidate>()
                : JsonSerializer.Deserialize<FavoriteDestinationPlaceCandidate[]>(record.CandidatesJson, JsonOptions)
                    ?? Array.Empty<FavoriteDestinationPlaceCandidate>(),
            record.IsPossibleDuplicate);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class JobRecord
    {
        public Guid ImportId { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int ImportedCount { get; set; }
        public string? ErrorMessage { get; set; }
        public int AttemptCount { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class RowRecord
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SubmittedAddress { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool UsesResolvedAddress { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? CandidatesJson { get; set; }
        public bool IsPossibleDuplicate { get; set; }
    }
}
