using Dapper;
using TripPlanner.Contracts.Audit;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Audit;

public interface IAuditRepository
{
    Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);
}

public sealed class AuditRepository : IAuditRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public AuditRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    { _factory = factory; _sql = sql; }

    public async Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = _sql.Get("Commands/Audit/InsertAuditEvent.sql");
        await conn.ExecuteAsync(new CommandDefinition(cmd, new
        {
            AuditEventId = Guid.NewGuid(),
            UserId = userId,
            Operation = operation,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Result = result,
            OccurredAtUtc = occurredAtUtc
        }, cancellationToken: cancellationToken));
    }
}
