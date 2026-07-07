using Dapper;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.TripSharing;

/// <summary>A caller's resolved access to a trip: the trip's owner id and the caller's access level.</summary>
public sealed record TripAccess(string OwnerUserId, TripAccessLevel AccessLevel);

/// <summary>Maps trip access levels to and from their stored text representation.</summary>
public static class TripAccessLevels
{
    public static TripAccessLevel Parse(string? value) => value switch
    {
        "owner" => TripAccessLevel.Owner,
        "collaborator" => TripAccessLevel.Collaborator,
        "viewer" => TripAccessLevel.Viewer,
        _ => TripAccessLevel.Viewer
    };

    /// <summary>Only viewer or collaborator can be persisted as a share; owner is implicit on the trip.</summary>
    public static string ToDbString(TripAccessLevel level) => level switch
    {
        TripAccessLevel.Collaborator => "collaborator",
        TripAccessLevel.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Only collaborator or viewer can be stored as a share.")
    };
}

public interface ITripSharingRepository
{
    Task<TripAccess?> GetAccessAsync(string userId, string? email, Guid tripId, CancellationToken ct);
    Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct);
    Task<TripShareMember?> UpsertShareAsync(string ownerUserId, Guid tripId, UpsertTripShareRequest request, DateTimeOffset nowUtc, CancellationToken ct);
    Task<TripShareMember?> UpdateAccessAsync(string ownerUserId, Guid tripId, string memberUserId, TripAccessLevel accessLevel, DateTimeOffset nowUtc, CancellationToken ct);
    Task<int> DeleteShareAsync(string ownerUserId, Guid tripId, string memberUserId, CancellationToken ct);
}

public sealed class TripSharingRepository : ITripSharingRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TripSharingRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    { _factory = factory; _sql = sql; }

    public async Task<TripAccess?> GetAccessAsync(string userId, string? email, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/TripSharing/GetTripAccess.sql");
        var row = await conn.QuerySingleOrDefaultAsync<AccessRow>(new CommandDefinition(query, new { OwnerUserId = userId, CallerEmail = email, TripId = tripId }, cancellationToken: ct));
        return row is null ? null : new TripAccess(row.OwnerUserId, TripAccessLevels.Parse(row.AccessLevel));
    }

    public async Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/TripSharing/GetTripShares.sql");
        var rows = await conn.QueryAsync<ShareRow>(new CommandDefinition(query, new { TripId = tripId }, cancellationToken: ct));
        return rows.Select(Map).ToArray();
    }

    public async Task<TripShareMember?> UpsertShareAsync(string ownerUserId, Guid tripId, UpsertTripShareRequest request, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var cmd = _sql.Get("Commands/TripSharing/UpsertTripShare.sql");
        var row = await conn.QuerySingleOrDefaultAsync<ShareRow>(new CommandDefinition(cmd, new
        {
            TripShareId = Guid.NewGuid(),
            TripId = tripId,
            MemberUserId = request.UserId,
            MemberDisplayName = request.DisplayName,
            MemberEmail = request.Email,
            AccessLevel = TripAccessLevels.ToDbString(request.AccessLevel),
            OwnerUserId = ownerUserId,
            NowUtc = nowUtc
        }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<TripShareMember?> UpdateAccessAsync(string ownerUserId, Guid tripId, string memberUserId, TripAccessLevel accessLevel, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var cmd = _sql.Get("Commands/TripSharing/UpdateTripShareAccess.sql");
        var row = await conn.QuerySingleOrDefaultAsync<ShareRow>(new CommandDefinition(cmd, new
        {
            TripId = tripId,
            MemberUserId = memberUserId,
            AccessLevel = TripAccessLevels.ToDbString(accessLevel),
            OwnerUserId = ownerUserId,
            NowUtc = nowUtc
        }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<int> DeleteShareAsync(string ownerUserId, Guid tripId, string memberUserId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var cmd = _sql.Get("Commands/TripSharing/DeleteTripShare.sql");
        return await conn.ExecuteAsync(new CommandDefinition(cmd, new { TripId = tripId, MemberUserId = memberUserId, OwnerUserId = ownerUserId }, cancellationToken: ct));
    }

    private static TripShareMember Map(ShareRow row)
        => new(row.UserId, row.DisplayName, row.Email, TripAccessLevels.Parse(row.AccessLevel), row.UpdatedAtUtc);

    private sealed record AccessRow(string OwnerUserId, string AccessLevel);
    private sealed record ShareRow(string UserId, string? DisplayName, string? Email, string AccessLevel, DateTimeOffset UpdatedAtUtc);
}
