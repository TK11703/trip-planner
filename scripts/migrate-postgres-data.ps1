<#
.SYNOPSIS
    Copies the tripplanner database from one Flexible Server to another.

.DESCRIPTION
    Re-runnable: everything the API role owns in the target database is dropped and recopied,
    so run it once as a rehearsal and again at cutover with the old API scaled to zero.
    Restored objects are owned by the API role, because the API runs DDL migrations.

    Prerequisites (one-time, see docs/operations/production-runbook.md section 2.0):
      - signed-in user is an Entra administrator on both servers;
      - this machine's public IP is allowed through both firewalls;
      - target has azure.extensions = pgcrypto,vector and a tripplanner database;
      - the API role exists on the target (pgaadauth_create_principal_with_oid).

    PostgreSQL client tools run in a postgres:16 container, so only Docker is required.

.EXAMPLE
    ./scripts/migrate-postgres-data.ps1
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $SourceServer = 'psql-trip-planner-ggg3cumf6h2cs',
    [string] $SourceResourceGroup = 'rg-trip-planner',
    [string] $TargetServer = 'accpsqlshared',
    [string] $TargetResourceGroup = 'rg-platform',
    [string] $Database = 'tripplanner',
    [string] $ApiRoleName = 'id-trip-planner-api',
    [string] $ClientImage = 'postgres:16'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-EntraAdminName([string] $ResourceGroup, [string] $Server, [string] $ObjectId) {
    $name = az postgres flexible-server microsoft-entra-admin list -g $ResourceGroup -s $Server `
        --query "[?objectId=='$ObjectId'].principalName | [0]" -o tsv 2>$null
    if (-not $name) { throw "Signed-in user is not an Entra administrator of '$Server'." }
    return $name
}

function Invoke-Psql([string] $HostName, [string] $User, [string] $Db, [string] $Sql) {
    $Sql | docker run --rm -i -e PGPASSWORD $ClientImage `
        psql "host=$HostName port=5432 dbname=$Db user=$User sslmode=require" -v ON_ERROR_STOP=1 -X -q -t -A
    if ($LASTEXITCODE -ne 0) { throw "psql failed against $HostName/$Db." }
}

$rowCountSql = @"
SELECT table_name || '=' || (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
ORDER BY table_name;
"@

$me = az ad signed-in-user show --query id -o tsv
$sourceHost = az postgres flexible-server show -g $SourceResourceGroup -n $SourceServer --query fullyQualifiedDomainName -o tsv
$targetHost = az postgres flexible-server show -g $TargetResourceGroup -n $TargetServer --query fullyQualifiedDomainName -o tsv
$sourceUser = Get-EntraAdminName $SourceResourceGroup $SourceServer $me
$targetUser = Get-EntraAdminName $TargetResourceGroup $TargetServer $me

# One token is valid for both servers (same tenant) for about an hour.
$env:PGPASSWORD = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv

if (-not (Invoke-Psql $targetHost $targetUser 'postgres' "SELECT 1 FROM pg_roles WHERE rolname = '$ApiRoleName';")) {
    throw "Role '$ApiRoleName' does not exist on '$TargetServer'. Create it with pgaadauth_create_principal_with_oid first."
}

if (-not $PSCmdlet.ShouldProcess("$TargetServer/$Database", "Replace all objects owned by '$ApiRoleName' with a copy from $SourceServer")) {
    return
}

# Admin membership in the API role is what lets the dump read its tables and the restore
# create objects as that role.
$grantMembership = "GRANT `"$ApiRoleName`" TO `"{0}`";"
Invoke-Psql $sourceHost $sourceUser $Database ($grantMembership -f $sourceUser) | Out-Null
Invoke-Psql $targetHost $targetUser $Database ($grantMembership -f $targetUser) | Out-Null

# DROP OWNED also revokes the role's privileges in this database, so they are re-granted.
Invoke-Psql $targetHost $targetUser $Database @"
DROP OWNED BY "$ApiRoleName";
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS vector;
GRANT CONNECT ON DATABASE $Database TO "$ApiRoleName";
GRANT USAGE, CREATE ON SCHEMA public TO "$ApiRoleName";
"@ | Out-Null

Write-Host "Copying $SourceServer/$Database -> $TargetServer/$Database ..."
# pg_dump --schema=public emits CREATE SCHEMA public and its comment; the schema already
# exists on the target and is not ours, so both entries are removed from the restore list.
$pipeline = "pg_dump --format=custom --schema=public --no-owner --no-acl --file=/tmp/db.dump ""host=$sourceHost port=5432 dbname=$Database user=$sourceUser sslmode=require"" " +
    "&& pg_restore --list /tmp/db.dump | grep -Ev ' (SCHEMA|COMMENT) - (SCHEMA )?public ' > /tmp/db.list " +
    "&& pg_restore --no-owner --no-acl --exit-on-error --single-transaction --role=""$ApiRoleName"" --use-list=/tmp/db.list --dbname=""host=$targetHost port=5432 dbname=$Database user=$targetUser sslmode=require"" /tmp/db.dump"
docker run --rm -e PGPASSWORD $ClientImage sh -c $pipeline
if ($LASTEXITCODE -ne 0) { throw 'pg_dump | pg_restore failed; the target transaction was rolled back.' }

$sourceCounts = @(Invoke-Psql $sourceHost $sourceUser $Database $rowCountSql)
$targetCounts = @(Invoke-Psql $targetHost $targetUser $Database $rowCountSql)
$diff = Compare-Object $sourceCounts $targetCounts
if ($diff) {
    $diff | Format-Table | Out-String | Write-Host
    throw 'Row counts differ between source and target.'
}
Write-Host "Verified $($sourceCounts.Count) tables:"
$sourceCounts | ForEach-Object { Write-Host "  $_" }
