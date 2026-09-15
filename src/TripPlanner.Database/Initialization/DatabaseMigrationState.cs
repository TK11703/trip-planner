namespace TripPlanner.Database.Initialization;

public enum DatabaseMigrationStatus
{
    /// <summary>Startup has not finished applying migrations.</summary>
    Pending,

    /// <summary>All migrations for this release are applied.</summary>
    Completed,

    /// <summary>Migration failed; the process must not accept traffic.</summary>
    Failed
}

/// <summary>
/// Shared, process-wide migration status so readiness can report unhealthy until startup
/// initialization has finished. Registered as a singleton and written once during startup.
/// </summary>
public sealed class DatabaseMigrationState
{
    private int _status = (int)DatabaseMigrationStatus.Pending;

    public DatabaseMigrationStatus Status => (DatabaseMigrationStatus)Volatile.Read(ref _status);

    public void MarkCompleted() => Volatile.Write(ref _status, (int)DatabaseMigrationStatus.Completed);

    public void MarkFailed() => Volatile.Write(ref _status, (int)DatabaseMigrationStatus.Failed);
}
