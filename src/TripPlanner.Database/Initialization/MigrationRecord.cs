namespace TripPlanner.Database.Initialization;

/// <summary>
/// One row of the <c>schema_migrations</c> ledger: the record that a specific schema
/// script was applied, by which release, and with what content.
/// </summary>
/// <param name="MigrationId">Stable ordered script identifier (the script file name).</param>
/// <param name="Checksum">Hash of the script body when it was applied.</param>
/// <param name="AppliedAtUtc">Database-generated UTC timestamp of application.</param>
/// <param name="ReleaseId">Release that applied the migration.</param>
/// <param name="DurationMilliseconds">Wall-clock time the migration took.</param>
public sealed record MigrationRecord(
    string MigrationId,
    string Checksum,
    DateTimeOffset AppliedAtUtc,
    string ReleaseId,
    long DurationMilliseconds);
