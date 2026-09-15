using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace TripPlanner.Database.Initialization;

/// <summary>
/// Reads and writes the <c>schema_migrations</c> ledger and owns the advisory lock that
/// serializes migration across concurrently starting replicas.
/// </summary>
public sealed class MigrationLedger
{
    /// <summary>
    /// Application-defined advisory lock key. Any value works as long as every replica
    /// agrees on it; this one is derived from "tripplanner.migrations".
    /// </summary>
    public const long AdvisoryLockKey = 7_246_193_845_110_027L;

    private readonly DbConnection _connection;

    public MigrationLedger(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Computes the checksum recorded for a script body.
    /// </summary>
    /// <remarks>
    /// Line endings are normalized first so a file checked out with CRLF on Windows and
    /// LF on Linux produces the same checksum and does not look like an edited migration.
    /// </remarks>
    public static string ComputeChecksum(string scriptBody)
    {
        ArgumentNullException.ThrowIfNull(scriptBody);

        var normalized = scriptBody.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Blocks until this session owns the migration lock. Released automatically when the
    /// connection closes, so a crashed replica cannot leave the lock held forever.
    /// </summary>
    public Task AcquireLockAsync(CancellationToken cancellationToken = default) =>
        _connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_lock(@key)",
            new { key = AdvisoryLockKey },
            cancellationToken: cancellationToken));

    /// <summary>
    /// Attempts to take the migration lock without waiting.
    /// </summary>
    /// <returns><c>true</c> when the lock was acquired by this session.</returns>
    public Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken = default) =>
        _connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT pg_try_advisory_lock(@key)",
            new { key = AdvisoryLockKey },
            cancellationToken: cancellationToken));

    public Task ReleaseLockAsync(CancellationToken cancellationToken = default) =>
        _connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_unlock(@key)",
            new { key = AdvisoryLockKey },
            cancellationToken: cancellationToken));

    /// <summary>
    /// Creates the ledger table when it does not exist.
    /// </summary>
    public Task EnsureCreatedAsync(string bootstrapSql, CancellationToken cancellationToken = default) =>
        _connection.ExecuteAsync(new CommandDefinition(bootstrapSql, cancellationToken: cancellationToken));

    /// <summary>
    /// Returns every applied migration keyed by migration id.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, MigrationRecord>> GetAppliedAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _connection.QueryAsync<MigrationRow>(new CommandDefinition(
            """
            SELECT migration_id, checksum, applied_at_utc, release_id, duration_milliseconds
            FROM schema_migrations
            """,
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new MigrationRecord(
                row.migration_id,
                row.checksum,
                row.applied_at_utc,
                row.release_id,
                row.duration_milliseconds))
            .ToDictionary(record => record.MigrationId, StringComparer.Ordinal);
    }

    /// <summary>
    /// Records a successfully applied migration. Runs inside the migration's transaction so
    /// the ledger entry and the schema change commit or roll back together.
    /// </summary>
    public Task RecordAsync(
        MigrationRecord record,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        _connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO schema_migrations
                (migration_id, checksum, release_id, duration_milliseconds)
            VALUES
                (@MigrationId, @Checksum, @ReleaseId, @DurationMilliseconds)
            """,
            record,
            transaction,
            cancellationToken: cancellationToken));

    // Dapper maps snake_case columns onto this shape without global configuration changes.
    private sealed record MigrationRow(
        string migration_id,
        string checksum,
        DateTimeOffset applied_at_utc,
        string release_id,
        long duration_milliseconds);
}
