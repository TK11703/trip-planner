<#
.SYNOPSIS
    Ensures the email relay's managed identity holds the EmailIngestion.Relay app role.

.DESCRIPTION
    Runs as the azd `postprovision` hook. It performs two idempotent directory operations:

      1. Exposes the `EmailIngestion.Relay` application role on the API app registration.
      2. Assigns that role to the relay's user-assigned managed identity.

    Neither is expressible in Bicep — they are Microsoft Graph directory objects rather
    than ARM resources — and the second has no Azure portal equivalent at all, so this
    script is the only way to keep the relay's authorization reproducible.

    Without the role the relay's token carries no `roles` claim. Microsoft.Identity.Web
    rejects a token bearing neither `scp` nor `roles` during validation (IDW10201), so the
    API answers 401 from the JWT challenge rather than 403 from the authorization policy.

.PARAMETER ApiClientId
    Application (client) ID of the API registration. Defaults to AZURE_ENTRA_API_CLIENT_ID.

.PARAMETER RelayPrincipalId
    Object ID of the relay managed identity's service principal. Defaults to the
    EMAIL_RELAY_IDENTITY_PRINCIPAL_ID output emitted by infra/main.bicep.

.PARAMETER Strict
    Exit non-zero when the grant is required but could not be applied. The default warns
    and returns success, so a directory-permission gap cannot fail an otherwise healthy
    infrastructure deployment.

.EXAMPLE
    ./scripts/grant-relay-app-role.ps1

.EXAMPLE
    ./scripts/grant-relay-app-role.ps1 -Strict
#>
[CmdletBinding()]
param(
    [string] $ApiClientId = $env:AZURE_ENTRA_API_CLIENT_ID,

    [string] $RelayPrincipalId = $env:EMAIL_RELAY_IDENTITY_PRINCIPAL_ID,

    [string] $RelayEnabled = $env:EMAIL_RELAY_ENABLED,

    [switch] $Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TripPlanner.Deployment.psm1') -Force
$contract = Get-TripPlannerDeploymentContract
$roleValue = $contract.EmailIngestionRelayRole

function Write-Skip {
    param([string] $Message)
    Write-Host "relay app role: skipped - $Message"
    exit 0
}

function Write-Unresolved {
    param([string] $Message, [string] $CorrectiveAction)

    if ($Strict) {
        Write-Error "relay app role: $Message`n$CorrectiveAction"
        exit 1
    }

    Write-Warning "relay app role: $Message"
    Write-Warning $CorrectiveAction
    Write-Warning 'The relay will receive 401 (IDW10201) from the API until this grant exists.'
    exit 0
}

function Invoke-AzJson {
    <#
    .SYNOPSIS
        Runs an Azure CLI command and parses its JSON output.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromRemainingArguments)] [string[]] $Arguments)

    $output = & az @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output | Out-String).Trim()
    }
    $text = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

function Invoke-Graph {
    <#
    .SYNOPSIS
        Calls Microsoft Graph through the Azure CLI, passing the body as a temp file.
    .DESCRIPTION
        `az rest --body` is unreliable with inline JSON on Windows because the shell eats
        the quoting, so every mutating call is routed through a file instead.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body
    )

    $arguments = @('rest', '--method', $Method, '--url', $Url)
    $bodyFile = $null

    if ($null -ne $Body) {
        $bodyFile = Join-Path ([System.IO.Path]::GetTempPath()) "relay-grant-$([guid]::NewGuid()).json"
        Set-Content -LiteralPath $bodyFile -Value ($Body | ConvertTo-Json -Depth 10 -Compress) -Encoding utf8 -NoNewline
        $arguments += @('--headers', 'Content-Type=application/json', '--body', "@$bodyFile")
    }

    try {
        $output = & az @arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ($output | Out-String).Trim()
        }
        $text = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json
    }
    finally {
        if ($bodyFile -and (Test-Path -LiteralPath $bodyFile)) {
            Remove-Item -LiteralPath $bodyFile -Force -ErrorAction SilentlyContinue
        }
    }
}

# CI exports the azd environment before invoking hooks, but an operator running this by
# hand has only selected it. Hydrating here means both paths resolve the same values.
if (Get-Command azd -CommandType Application -ErrorAction SilentlyContinue) {
    foreach ($line in @(& azd env get-values 2>$null)) {
        if ("$line" -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"?(.*?)"?\s*$') {
            $name = $Matches[1]
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
                Set-Item -Path "Env:$name" -Value $Matches[2]
            }
        }
    }
    if (-not $ApiClientId) { $ApiClientId = $env:AZURE_ENTRA_API_CLIENT_ID }
    if (-not $RelayPrincipalId) { $RelayPrincipalId = $env:EMAIL_RELAY_IDENTITY_PRINCIPAL_ID }
    if (-not $RelayEnabled) { $RelayEnabled = $env:EMAIL_RELAY_ENABLED }
}

if ($RelayEnabled -ne 'true') {
    Write-Skip "EMAIL_RELAY_ENABLED is '$RelayEnabled'; the relay is not deployed."
}

if ([string]::IsNullOrWhiteSpace($ApiClientId)) {
    Write-Unresolved 'AZURE_ENTRA_API_CLIENT_ID is not set.' `
        "Set it with 'azd env set AZURE_ENTRA_API_CLIENT_ID <client-id>'."
}

if ([string]::IsNullOrWhiteSpace($RelayPrincipalId)) {
    Write-Unresolved 'EMAIL_RELAY_IDENTITY_PRINCIPAL_ID is not set.' `
        'It is an output of infra/main.bicep; re-run provisioning with the relay enabled.'
}

# --------------------------------------------------------------------------
# 1. Expose the role on the API registration
# --------------------------------------------------------------------------

try {
    # `az ad app show` rather than a Graph alternate-key URL: the `(appId='...')` segment
    # does not survive the az.cmd batch wrapper on Windows.
    $app = Invoke-AzJson 'ad' 'app' 'show' '--id' $ApiClientId
}
catch {
    Write-Unresolved "could not read the API app registration ($ApiClientId): $_" `
        'The signed-in principal needs Application.Read.All (Cloud Application Administrator covers it).'
}

$existingRoles = @($app.appRoles)
$role = $existingRoles | Where-Object { $_.value -eq $roleValue } | Select-Object -First 1

if ($role -and $role.isEnabled) {
    $roleId = $role.id
    Write-Host "relay app role: '$roleValue' already exposed ($roleId)."
}
else {
    # PATCH replaces the whole appRoles collection, so every existing role is carried
    # forward. Dropping one here would silently revoke it for every assignee.
    if ($role) {
        $roleId = $role.id
        $role.isEnabled = $true
        $desiredRoles = $existingRoles
        $action = "re-enabled '$roleValue'"
    }
    else {
        $roleId = [guid]::NewGuid().ToString()
        $desiredRoles = $existingRoles + @([pscustomobject]@{
                id                 = $roleId
                allowedMemberTypes = @('Application')
                displayName        = 'Email ingestion relay'
                description        = 'Allows the email relay to submit messages for ingestion.'
                value              = $roleValue
                isEnabled          = $true
            })
        $action = "exposed '$roleValue'"
    }

    try {
        Invoke-Graph -Method patch `
            -Url "https://graph.microsoft.com/v1.0/applications/$($app.id)" `
            -Body @{ appRoles = @($desiredRoles) } | Out-Null
    }
    catch {
        Write-Unresolved "could not update the API app registration: $_" `
            'The signed-in principal needs Application.ReadWrite.All (Cloud Application Administrator).'
    }

    Write-Host "relay app role: $action ($roleId), preserving $($existingRoles.Count) existing role(s)."
}

# --------------------------------------------------------------------------
# 2. Assign the role to the relay identity
# --------------------------------------------------------------------------

try {
    $apiSp = Invoke-AzJson 'ad' 'sp' 'show' '--id' $ApiClientId
}
catch {
    Write-Unresolved "could not resolve the API service principal for ${ApiClientId}: $_" `
        'Confirm the app registration has an enterprise application in this tenant.'
}

$assignmentUrl = "https://graph.microsoft.com/v1.0/servicePrincipals/$RelayPrincipalId/appRoleAssignments"

try {
    $assignments = Invoke-Graph -Method get -Url $assignmentUrl
}
catch {
    Write-Unresolved "could not read app role assignments for the relay identity: $_" `
        'Confirm EMAIL_RELAY_IDENTITY_PRINCIPAL_ID is the service principal object ID of the managed identity.'
}

$already = @($assignments.value) | Where-Object {
    $_.resourceId -eq $apiSp.id -and $_.appRoleId -eq $roleId
}

if ($already) {
    Write-Host 'relay app role: assignment already present; nothing to do.'
    exit 0
}

# A role created moments ago is not always visible to the assignment endpoint yet, so a
# 400/404 here is usually replication lag rather than a real rejection.
$assignmentBody = @{
    principalId = $RelayPrincipalId
    resourceId  = $apiSp.id
    appRoleId   = $roleId
}

$maxAttempts = 5
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        Invoke-Graph -Method post -Url $assignmentUrl -Body $assignmentBody | Out-Null
        Write-Host "relay app role: assigned '$roleValue' to $RelayPrincipalId."
        break
    }
    catch {
        $message = "$_"

        if ($message -match 'Permission being assigned already exists') {
            Write-Host 'relay app role: assignment already present; nothing to do.'
            break
        }

        if ($attempt -eq $maxAttempts) {
            Write-Unresolved "could not assign the role after $maxAttempts attempts: $message" `
                'The signed-in principal needs AppRoleAssignment.ReadWrite.All (Cloud Application Administrator).'
        }

        Write-Host "relay app role: assignment attempt $attempt failed, retrying in 5s (directory replication)."
        Start-Sleep -Seconds 5
    }
}

Write-Host ''
Write-Host 'Note: the managed identity token cache holds the previous role-less token for up'
Write-Host '      to ~24h. Until it expires the relay may still receive 401 (IDW10201).'
