using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Initialization;

/// <summary>
/// Applies pending schema scripts exactly once per database.
/// </summary>
/// <remarks>
/// Several replicas can start at the same time, so initialization holds a PostgreSQL
/// advisory lock for the whole check-and-apply pass. Each script runs in its own
/// transaction alongside its ledger entry, so a failure leaves the database on the last
/// successfully applied migration rather than half-way through one.
/// </remarks>
public sealed class DatabaseInitializer
{
    private readonly ILogger<DatabaseInitializer>? _logger;

    public DatabaseInitializer(ILogger<DatabaseInitializer>? logger = null) => _logger = logger;

    /// <summary>
    /// Raised when an already-applied script has been edited. Startup must not continue:
    /// the database no longer matches the migration history the release assumes.
    /// </summary>
    public sealed class MigrationChecksumMismatchException(string migrationId)
        : InvalidOperationException(
            $"Migration '{migrationId}' was already applied with different content. " +
            "Editing an applied migration is not supported; add a new migration script instead.")
    {
        public string MigrationId { get; } = migrationId;
    }

    public async Task InitializeAsync(
        IServiceProvider services,
        string releaseId = "unknown",
        CancellationToken cancellationToken = default)
    {
        var factory = services.GetRequiredService<IPostgresConnectionFactory>();
        var sql = services.GetRequiredService<ISqlFileProvider>();

        var scripts = sql.GetAllInDirectory("Schema");
        if (scripts.Count == 0)
        {
            _logger?.LogInformation("No schema scripts located; skipping database initialization.");
            return;
        }

        await using var connection = await factory.CreateOpenConnectionAsync(cancellationToken);
        var ledger = new MigrationLedger(connection);

        // Held for the whole pass, and released when the connection closes even on crash.
        await ledger.AcquireLockAsync(cancellationToken);
        try
        {
            await ledger.EnsureCreatedAsync(sql.Get("Initialization/migration_ledger.sql"), cancellationToken);
            var applied = await ledger.GetAppliedAsync(cancellationToken);

            var appliedNow = 0;
            foreach (var (name, body) in scripts)
            {
                var checksum = MigrationLedger.ComputeChecksum(body);

                if (applied.TryGetValue(name, out var existing))
                {
                    if (!string.Equals(existing.Checksum, checksum, StringComparison.Ordinal))
                    {
                        throw new MigrationChecksumMismatchException(name);
                    }

                    _logger?.LogDebug("Migration {Migration} already applied; skipping.", name);
                    continue;
                }

                await ApplyAsync(connection, ledger, name, body, checksum, releaseId, cancellationToken);
                appliedNow++;
            }

            _logger?.LogInformation(
                "Database initialization complete: {Applied} migration(s) applied, {Total} total.",
                appliedNow,
                scripts.Count);
        }
        finally
        {
            await ledger.ReleaseLockAsync(CancellationToken.None);
        }
    }

    private async Task ApplyAsync(
        System.Data.Common.DbConnection connection,
        MigrationLedger ledger,
        string name,
        string body,
        string checksum,
        string releaseId,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Applying migration {Migration}", name);

        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                body,
                transaction: transaction,
                cancellationToken: cancellationToken));

            stopwatch.Stop();

            await ledger.RecordAsync(
                new MigrationRecord(name, checksum, DateTimeOffset.UtcNow, releaseId, stopwatch.ElapsedMilliseconds),
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
