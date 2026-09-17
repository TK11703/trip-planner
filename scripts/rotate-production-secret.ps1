<#
.SYNOPSIS
    Rotates a production secret in Key Vault with verification before the old version is retired.

.DESCRIPTION
    Implements the five-step rotation the runbook prescribes:

      1. add    — write a new version of the secret
      2. verify — confirm the new version is the current one and is readable
      3. restart — restart the consuming container apps so they pick up the new version
      4. validate — confirm the apps are healthy on the new value
      5. disable — disable the previous version only after validation succeeds

    If validation fails, the previous version is left enabled so an operator can restart
    back onto it. The script never deletes a secret version.

.PARAMETER SecretName
    Key Vault secret to rotate, for example postgres-connection-string.

.PARAMETER KeyVaultName
    Key Vault holding the secret.

.PARAMETER NewValue
    The replacement value. Prompted for securely when omitted.

.PARAMETER ResourceGroup
    Resource group holding the container apps that consume the secret.

.PARAMETER EnvironmentName
    Naming seed used to resolve the consuming container app names. Defaults to AZURE_ENV_NAME.

.PARAMETER ConsumingApp
    Container apps to restart. Defaults are derived from the secret name.

.PARAMETER WebUrl
    Public HTTPS base URL used for the post-rotation validation probe.

.PARAMETER SkipDisableOldVersion
    Leaves the previous version enabled. Use when an external system still needs it.

.EXAMPLE
    ./scripts/rotate-production-secret.ps1 -SecretName postgres-connection-string `
        -KeyVaultName kv-tripplanner-prod -ResourceGroup rg-trip-planner `
        -WebUrl https://ca-web-trip-planner.centralus.azurecontainerapps.io
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('postgres-password', 'postgres-connection-string')]
    [string] $SecretName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $KeyVaultName,

    [securestring] $NewValue,

    [string] $ResourceGroup = $env:AZURE_RESOURCE_GROUP,

    [string] $EnvironmentName = $env:AZURE_ENV_NAME,

    [string[]] $ConsumingApp,

    [string] $WebUrl = $env:SERVICE_WEB_URI,

    [switch] $SkipDisableOldVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TripPlanner.Deployment.psm1') -Force

# Which apps read which secret. Restarting an app that does not consume the secret would
# cause an avoidable cold start, so the map is explicit. The admin password has no runtime
# consumer at all: the API authenticates to Flexible Server with its managed identity.
$SecretConsumers = @{
    'postgres-password'          = @()
    'postgres-connection-string' = @('api')
}

if ($null -eq $ConsumingApp -or $ConsumingApp.Count -eq 0) {
    if ([string]::IsNullOrWhiteSpace($EnvironmentName)) {
        throw 'Set AZURE_ENV_NAME or pass -EnvironmentName so the consuming container app names can be resolved.'
    }

    $ConsumingApp = @($SecretConsumers[$SecretName] | ForEach-Object { "ca-$_-$EnvironmentName" })
}

if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    throw 'A resource group is required so the consuming container apps can be restarted.'
}

if ($SecretName -eq 'postgres-password') {
    Write-Warning "postgres-password is the Flexible Server administrator login, used for schema bootstrap and break-glass only. Change it on the server first with 'az postgres flexible-server update --admin-password', then rotate here, then update the POSTGRES_PASSWORD GitHub secret so the next provision does not reset it."
}

# ---------------------------------------------------------------------------
# 1. Add the new version
# ---------------------------------------------------------------------------

if ($null -eq $NewValue) {
    $NewValue = Read-Host -Prompt "New value for '$SecretName'" -AsSecureString
}

$plainValue = [System.Net.NetworkCredential]::new('', $NewValue).Password
if ([string]::IsNullOrWhiteSpace($plainValue)) {
    throw 'The replacement value is empty. Rotation aborted.'
}

$previousVersion = Invoke-AzCommand -Argument @(
    'keyvault', 'secret', 'show',
    '--vault-name', $KeyVaultName, '--name', $SecretName,
    '--query', 'id', '-o', 'tsv')
$previousVersion = "$previousVersion".Trim()

if ([string]::IsNullOrWhiteSpace($previousVersion)) {
    throw "Secret '$SecretName' does not exist in '$KeyVaultName'. Rotation requires an existing secret."
}

Write-Host "Current version: $previousVersion"

if (-not $PSCmdlet.ShouldProcess("$KeyVaultName/$SecretName", 'add new secret version')) {
    return
}

Write-Host "Adding a new version of '$SecretName'"
$newVersion = Invoke-AzCommand -Argument @(
    'keyvault', 'secret', 'set',
    '--vault-name', $KeyVaultName, '--name', $SecretName,
    '--value', $plainValue,
    '--query', 'id', '-o', 'tsv')
$newVersion = "$newVersion".Trim()
$plainValue = $null

if ([string]::IsNullOrWhiteSpace($newVersion) -or $newVersion -eq $previousVersion) {
    throw 'Key Vault did not report a new secret version. Rotation aborted before any app was restarted.'
}

# ---------------------------------------------------------------------------
# 2. Verify the new version is current and readable
# ---------------------------------------------------------------------------

Write-Host 'Verifying the new version is current'
$currentVersion = "$(Invoke-AzCommand -Argument @(
    'keyvault', 'secret', 'show',
    '--vault-name', $KeyVaultName, '--name', $SecretName,
    '--query', 'id', '-o', 'tsv'))".Trim()

if ($currentVersion -ne $newVersion) {
    throw "The current version ($currentVersion) is not the version just written ($newVersion). Rotation aborted."
}

Write-Host "New version: $newVersion"

# ---------------------------------------------------------------------------
# 3. Restart consumers so they resolve the new version
# ---------------------------------------------------------------------------

foreach ($app in $ConsumingApp) {
    Write-Host "Restarting container app '$app'"
    $revision = "$(Invoke-AzCommand -Argument @(
        'containerapp', 'revision', 'list', '-n', $app, '-g', $ResourceGroup,
        '--query', '[?properties.active].name | [0]', '-o', 'tsv'))".Trim()

    if ([string]::IsNullOrWhiteSpace($revision)) {
        throw "No active revision found for container app '$app'. Rotation stopped; the previous secret version is still enabled."
    }

    Invoke-AzCommand -Argument @(
        'containerapp', 'revision', 'restart',
        '-n', $app, '-g', $ResourceGroup, '--revision', $revision) | Out-Null
}

# ---------------------------------------------------------------------------
# 4. Validate the apps are healthy on the new value
# ---------------------------------------------------------------------------

$validated = $false

if ([string]::IsNullOrWhiteSpace($WebUrl)) {
    Write-Warning 'No web URL was supplied, so post-rotation health could not be validated automatically.'
}
else {
    $healthUri = "$($WebUrl.TrimEnd('/'))/health"
    Write-Host "Validating $healthUri"

    # Scale-from-zero plus a cold start after the restart: retry rather than fail fast.
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $healthUri -TimeoutSec 30 -SkipHttpErrorCheck -ErrorAction Stop
            if ([int]$response.StatusCode -eq 200) {
                $validated = $true
                break
            }
            Write-Host "  attempt ${attempt}: HTTP $([int]$response.StatusCode)"
        }
        catch {
            Write-Host "  attempt ${attempt}: $(Protect-DeploymentSecret -InputText $_.Exception.Message)"
        }
        Start-Sleep -Seconds 15
    }
}

# ---------------------------------------------------------------------------
# 5. Disable the previous version, only after validation
# ---------------------------------------------------------------------------

if (-not $validated) {
    Write-Warning "Post-rotation validation did not succeed. The previous version is left ENABLED so you can restart back onto it:"
    Write-Warning "  $previousVersion"
    throw 'Rotation completed the write but failed validation. Investigate before retiring the previous version.'
}

if ($SkipDisableOldVersion) {
    Write-Host 'Validation passed. Leaving the previous version enabled as requested.'
}
else {
    Write-Host 'Validation passed. Disabling the previous version.'
    Invoke-AzCommand -Argument @(
        'keyvault', 'secret', 'set-attributes',
        '--id', $previousVersion, '--enabled', 'false', '-o', 'none') | Out-Null
}

Write-Host ''
Write-Host "Rotated '$SecretName' in '$KeyVaultName'." -ForegroundColor Green
Write-Host "  previous: $previousVersion$(if ($SkipDisableOldVersion) { ' (still enabled)' } else { ' (disabled)' })"
Write-Host "  current : $newVersion"
Write-Host 'Record the rotation date in docs/operations/production-runbook.md.'
