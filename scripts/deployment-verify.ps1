<#
.SYNOPSIS
    Post-deployment verification gate for the Trip Planner production release.

.DESCRIPTION
    Exercises the deployed release end to end and writes a sanitized, schema-conformant
    report to artifacts/deployment-evidence/verification-<releaseId>.json.

    Exit code 0 means the release is verified. Any other exit code means the release is
    unproven and must not be marked complete. `not-run` is deliberately treated as a
    failure: an unverified release is not a verified one.

    The script is read-mostly. It creates and then deletes a single throwaway trip through
    the public API to prove the core workflow and persistence; it never mutates
    infrastructure and never touches another user's data.

.PARAMETER ReleaseId
    Immutable release identifier, normally the full commit SHA.

.PARAMETER EnvironmentName
    The azd environment name (AZURE_ENV_NAME).

.PARAMETER WebUrl
    Public HTTPS base URL of the web app, for example https://web.<domain>.

.PARAMETER AccessToken
    Bearer token for a verification test user, scoped to the API. When omitted, the
    authenticated checks report `not-run` and the release is reported unverified.

.PARAMETER SecondaryAccessToken
    Bearer token for a second, unrelated verification user. Used to prove cross-user
    isolation. When omitted, the isolation check reports `not-run`.

.PARAMETER Offline
    Skips every network call. Intended for contract tests and for validating report shape
    without a deployed environment. Every check reports `not-run`.

.EXAMPLE
    ./scripts/deployment-verify.ps1 -ReleaseId $env:GITHUB_SHA -WebUrl (azd env get-value SERVICE_WEB_URI)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ReleaseId,

    [string] $EnvironmentName = $env:AZURE_ENV_NAME,

    [string] $WebUrl = $env:SERVICE_WEB_URI,

    [string] $AccessToken = $env:VERIFICATION_ACCESS_TOKEN,

    [string] $SecondaryAccessToken = $env:VERIFICATION_SECONDARY_ACCESS_TOKEN,

    [string] $ResourceGroup = $env:AZURE_RESOURCE_GROUP,

    [string] $OutputPath,

    [int] $TimeoutSeconds = 30,

    [switch] $Offline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TripPlanner.Deployment.psm1') -Force

$contract = Get-TripPlannerDeploymentContract
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$schemaPath = Join-Path $repoRoot 'specs/026-azure-deployment-readiness/contracts/deployment-verification.schema.json'

if ([string]::IsNullOrWhiteSpace($EnvironmentName)) { $EnvironmentName = 'unknown' }

$startedAt = Get-DeploymentUtcNow
$online = -not $Offline
$checks = [System.Collections.Generic.List[object]]::new()

# Populated by the sign-in/authenticated-api checks and reused by later checks so the
# workflow checks do not repeat work that has already failed.
$script:ApiReachable = $false
$script:CreatedTripId = $null

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Add-Result {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Category,
        [Parameter(Mandatory = $true)][string] $Status,
        [Parameter(Mandatory = $true)][string] $Summary,
        [int] $DurationMilliseconds = 0,
        [string] $EvidenceReference
    )

    $checks.Add((New-VerificationCheck -Id $Id -Category $Category -Status $Status `
                -Summary $Summary -DurationMilliseconds $DurationMilliseconds -EvidenceReference $EvidenceReference))
}

function Add-NotRun {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Category,
        [Parameter(Mandatory = $true)][string] $Reason
    )

    Add-Result -Id $Id -Category $Category -Status 'not-run' -Summary $Reason
}

function Invoke-VerificationRequest {
    <#
    .SYNOPSIS
        Issues an HTTP request and returns status, body, and elapsed time without throwing.

    .DESCRIPTION
        A non-2xx response is data, not an error: every check needs to record the status it
        observed rather than crash the run.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $Uri,
        [string] $Method = 'GET',
        [string] $Token,
        $Body,
        [switch] $AllowRedirect
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers['Authorization'] = "Bearer $Token" }

    $parameters = @{
        Uri                = $Uri
        Method             = $Method
        Headers            = $headers
        TimeoutSec         = $TimeoutSeconds
        SkipHttpErrorCheck = $true
        ErrorAction        = 'Stop'
    }
    if (-not $AllowRedirect) { $parameters['MaximumRedirection'] = 0 }
    if ($null -ne $Body) {
        $parameters['Body'] = ($Body | ConvertTo-Json -Depth 8)
        $parameters['ContentType'] = 'application/json'
    }

    try {
        $response = Invoke-WebRequest @parameters
        $stopwatch.Stop()
        [pscustomobject]@{
            Succeeded  = $true
            StatusCode = [int]$response.StatusCode
            Content    = [string]$response.Content
            Headers    = $response.Headers
            Elapsed    = [int]$stopwatch.ElapsedMilliseconds
            Error      = $null
        }
    }
    catch {
        $stopwatch.Stop()
        [pscustomobject]@{
            Succeeded  = $false
            StatusCode = 0
            Content    = ''
            Headers    = @{}
            Elapsed    = [int]$stopwatch.ElapsedMilliseconds
            Error      = Protect-DeploymentSecret -InputText $_.Exception.Message
        }
    }
}

# ---------------------------------------------------------------------------
# Category: secure-reachability
# ---------------------------------------------------------------------------

function Test-SecureReachability {
    if (-not $online) {
        Add-NotRun -Id 'secure-reachability-https' -Category 'secure-reachability' -Reason 'Offline mode: the public endpoint was not contacted.'
        Add-NotRun -Id 'secure-reachability-http-rejected' -Category 'secure-reachability' -Reason 'Offline mode: plaintext rejection was not exercised.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($WebUrl)) {
        Add-Result -Id 'secure-reachability-https' -Category 'secure-reachability' -Status 'fail' `
            -Summary 'No web URL was supplied, so the release could not be reached.'
        Add-NotRun -Id 'secure-reachability-http-rejected' -Category 'secure-reachability' -Reason 'Skipped: no web URL to test.'
        return
    }

    if (-not $WebUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
        Add-Result -Id 'secure-reachability-https' -Category 'secure-reachability' -Status 'fail' `
            -Summary "The supplied web URL is not HTTPS: '$WebUrl'."
        Add-NotRun -Id 'secure-reachability-http-rejected' -Category 'secure-reachability' -Reason 'Skipped: the base URL is not HTTPS.'
        return
    }

    # A sign-in redirect (302) is a healthy response for an authenticated app.
    $response = Invoke-VerificationRequest -Uri $WebUrl
    if ($response.Succeeded -and @(200, 302) -contains $response.StatusCode) {
        Add-Result -Id 'secure-reachability-https' -Category 'secure-reachability' -Status 'pass' `
            -Summary "The web app answered over HTTPS with status $($response.StatusCode)." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $WebUrl"
        $script:ApiReachable = $true
    }
    else {
        Add-Result -Id 'secure-reachability-https' -Category 'secure-reachability' -Status 'fail' `
            -Summary "The web app did not answer over HTTPS (status $($response.StatusCode); $($response.Error))." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $WebUrl"
    }

    # Container Apps ingress is configured with allowInsecure=false, so plaintext must be
    # redirected to HTTPS rather than served.
    $plaintextUri = 'http://' + $WebUrl.Substring('https://'.Length)
    $plain = Invoke-VerificationRequest -Uri $plaintextUri
    $redirected = $plain.Succeeded -and @(301, 302, 307, 308) -contains $plain.StatusCode
    if ($redirected -or -not $plain.Succeeded) {
        Add-Result -Id 'secure-reachability-http-rejected' -Category 'secure-reachability' -Status 'pass' `
            -Summary 'Plaintext HTTP is redirected or refused; content is served over HTTPS only.' `
            -DurationMilliseconds $plain.Elapsed -EvidenceReference "GET $plaintextUri"
    }
    else {
        Add-Result -Id 'secure-reachability-http-rejected' -Category 'secure-reachability' -Status 'fail' `
            -Summary "Plaintext HTTP returned status $($plain.StatusCode) instead of redirecting to HTTPS." `
            -DurationMilliseconds $plain.Elapsed -EvidenceReference "GET $plaintextUri"
    }
}

# ---------------------------------------------------------------------------
# Category: liveness
# ---------------------------------------------------------------------------

function Test-Liveness {
    if (-not $online) {
        Add-NotRun -Id 'liveness-web' -Category 'liveness' -Reason 'Offline mode: the liveness endpoint was not contacted.'
        return
    }

    if (-not $script:ApiReachable) {
        Add-NotRun -Id 'liveness-web' -Category 'liveness' -Reason 'Skipped: the web app was not reachable.'
        return
    }

    $uri = "$($WebUrl.TrimEnd('/'))/alive"
    $response = Invoke-VerificationRequest -Uri $uri
    if ($response.Succeeded -and $response.StatusCode -eq 200) {
        Add-Result -Id 'liveness-web' -Category 'liveness' -Status 'pass' `
            -Summary 'The web app reports itself alive.' `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
    else {
        Add-Result -Id 'liveness-web' -Category 'liveness' -Status 'fail' `
            -Summary "The liveness endpoint returned status $($response.StatusCode)." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
}

# ---------------------------------------------------------------------------
# Category: readiness
# ---------------------------------------------------------------------------

function Test-Readiness {
    if (-not $online) {
        Add-NotRun -Id 'readiness-web' -Category 'readiness' -Reason 'Offline mode: the readiness endpoint was not contacted.'
        return
    }

    if (-not $script:ApiReachable) {
        Add-NotRun -Id 'readiness-web' -Category 'readiness' -Reason 'Skipped: the web app was not reachable.'
        return
    }

    # /health aggregates the API reachability, authentication configuration, and
    # data-protection key ring; the API's own /health covers PostgreSQL and migrations.
    $uri = "$($WebUrl.TrimEnd('/'))/health"
    $response = Invoke-VerificationRequest -Uri $uri
    if ($response.Succeeded -and $response.StatusCode -eq 200 -and $response.Content -match 'Healthy') {
        Add-Result -Id 'readiness-web' -Category 'readiness' -Status 'pass' `
            -Summary 'All web dependencies report healthy.' `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
    else {
        Add-Result -Id 'readiness-web' -Category 'readiness' -Status 'fail' `
            -Summary "Readiness returned status $($response.StatusCode). At least one dependency is unhealthy." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
}

# ---------------------------------------------------------------------------
# Category: sign-in
# ---------------------------------------------------------------------------

function Test-SignIn {
    if (-not $online) {
        Add-NotRun -Id 'sign-in-challenge' -Category 'sign-in' -Reason 'Offline mode: the sign-in challenge was not exercised.'
        return
    }

    if (-not $script:ApiReachable) {
        Add-NotRun -Id 'sign-in-challenge' -Category 'sign-in' -Reason 'Skipped: the web app was not reachable.'
        return
    }

    # An anonymous request to a protected page must redirect to the configured Entra
    # authority. A 200 here would mean authentication is not being enforced.
    $uri = "$($WebUrl.TrimEnd('/'))/trips"
    $response = Invoke-VerificationRequest -Uri $uri
    $location = if ($response.Headers.ContainsKey('Location')) { [string]($response.Headers['Location'] | Select-Object -First 1) } else { '' }

    if ($response.StatusCode -eq 302 -and $location -match 'login\.microsoftonline\.com|oauth2/v2\.0/authorize') {
        Add-Result -Id 'sign-in-challenge' -Category 'sign-in' -Status 'pass' `
            -Summary 'An anonymous request to a protected page is challenged by Entra ID.' `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
    elseif ($response.StatusCode -eq 200) {
        Add-Result -Id 'sign-in-challenge' -Category 'sign-in' -Status 'fail' `
            -Summary 'A protected page was served to an anonymous caller; authentication is not being enforced.' `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
    else {
        Add-Result -Id 'sign-in-challenge' -Category 'sign-in' -Status 'fail' `
            -Summary "The sign-in challenge did not redirect to Entra ID (status $($response.StatusCode))." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
}

# ---------------------------------------------------------------------------
# Category: authenticated-api
# ---------------------------------------------------------------------------

function Test-AuthenticatedApi {
    if (-not $online) {
        Add-NotRun -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Reason 'Offline mode: the API was not contacted.'
        Add-NotRun -Id 'authenticated-api-accepts-token' -Category 'authenticated-api' -Reason 'Offline mode: the API was not contacted.'
        return
    }

    if (-not $script:ApiReachable) {
        Add-NotRun -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Reason 'Skipped: the web app was not reachable.'
        Add-NotRun -Id 'authenticated-api-accepts-token' -Category 'authenticated-api' -Reason 'Skipped: the web app was not reachable.'
        return
    }

    $uri = "$($WebUrl.TrimEnd('/'))/api/trips"

    $anonymous = Invoke-VerificationRequest -Uri $uri
    if (@(401, 403, 302) -contains $anonymous.StatusCode) {
        Add-Result -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Status 'pass' `
            -Summary "The API rejected an anonymous call with status $($anonymous.StatusCode)." `
            -DurationMilliseconds $anonymous.Elapsed -EvidenceReference "GET $uri (anonymous)"
    }
    else {
        Add-Result -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Status 'fail' `
            -Summary "The API answered an anonymous call with status $($anonymous.StatusCode); authorization is not being enforced." `
            -DurationMilliseconds $anonymous.Elapsed -EvidenceReference "GET $uri (anonymous)"
    }

    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        Add-NotRun -Id 'authenticated-api-accepts-token' -Category 'authenticated-api' `
            -Reason 'No verification access token was supplied, so the authenticated path is unproven.'
        return
    }

    $authenticated = Invoke-VerificationRequest -Uri $uri -Token $AccessToken
    if ($authenticated.StatusCode -eq 200) {
        Add-Result -Id 'authenticated-api-accepts-token' -Category 'authenticated-api' -Status 'pass' `
            -Summary 'The API accepted a correctly scoped bearer token.' `
            -DurationMilliseconds $authenticated.Elapsed -EvidenceReference "GET $uri (authenticated)"
    }
    else {
        Add-Result -Id 'authenticated-api-accepts-token' -Category 'authenticated-api' -Status 'fail' `
            -Summary "The API rejected a correctly scoped token with status $($authenticated.StatusCode)." `
            -DurationMilliseconds $authenticated.Elapsed -EvidenceReference "GET $uri (authenticated)"
    }
}

# ---------------------------------------------------------------------------
# Category: data-access
# ---------------------------------------------------------------------------

function Test-DataAccess {
    if (-not $online) {
        Add-NotRun -Id 'data-access-read' -Category 'data-access' -Reason 'Offline mode: no data was read.'
        Add-NotRun -Id 'data-access-cross-user-isolation' -Category 'data-access' -Reason 'Offline mode: isolation was not exercised.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($AccessToken) -or -not $script:ApiReachable) {
        Add-NotRun -Id 'data-access-read' -Category 'data-access' -Reason 'Skipped: no authenticated session was available.'
        Add-NotRun -Id 'data-access-cross-user-isolation' -Category 'data-access' -Reason 'Skipped: no authenticated session was available.'
        return
    }

    $uri = "$($WebUrl.TrimEnd('/'))/api/trips"
    $read = Invoke-VerificationRequest -Uri $uri -Token $AccessToken
    if ($read.StatusCode -eq 200) {
        Add-Result -Id 'data-access-read' -Category 'data-access' -Status 'pass' `
            -Summary 'The API read through to PostgreSQL and returned the caller''s trips.' `
            -DurationMilliseconds $read.Elapsed -EvidenceReference "GET $uri"
    }
    else {
        Add-Result -Id 'data-access-read' -Category 'data-access' -Status 'fail' `
            -Summary "Reading trips failed with status $($read.StatusCode); the database path is broken." `
            -DurationMilliseconds $read.Elapsed -EvidenceReference "GET $uri"
        return
    }

    if ([string]::IsNullOrWhiteSpace($SecondaryAccessToken)) {
        Add-NotRun -Id 'data-access-cross-user-isolation' -Category 'data-access' `
            -Reason 'No secondary verification token was supplied, so cross-user isolation is unproven.'
        return
    }

    # The second user must not see the first user's trips. Compare returned identifiers:
    # any overlap means the ownership filter is not being applied.
    $secondary = Invoke-VerificationRequest -Uri $uri -Token $SecondaryAccessToken
    if ($secondary.StatusCode -ne 200) {
        Add-Result -Id 'data-access-cross-user-isolation' -Category 'data-access' -Status 'fail' `
            -Summary "The secondary verification user could not read their own trips (status $($secondary.StatusCode))." `
            -DurationMilliseconds $secondary.Elapsed -EvidenceReference "GET $uri (secondary user)"
        return
    }

    $primaryIds = @()
    $secondaryIds = @()
    try {
        $primaryIds = @(($read.Content | ConvertFrom-Json) | ForEach-Object { $_.id })
        $secondaryIds = @(($secondary.Content | ConvertFrom-Json) | ForEach-Object { $_.id })
    }
    catch {
        Add-Result -Id 'data-access-cross-user-isolation' -Category 'data-access' -Status 'fail' `
            -Summary 'The trips response could not be parsed, so cross-user isolation could not be confirmed.' `
            -DurationMilliseconds $secondary.Elapsed -EvidenceReference "GET $uri (secondary user)"
        return
    }

    $overlap = @($primaryIds | Where-Object { $secondaryIds -contains $_ })
    if ($overlap.Count -eq 0) {
        Add-Result -Id 'data-access-cross-user-isolation' -Category 'data-access' -Status 'pass' `
            -Summary 'Two verification users see disjoint trip sets; ownership filtering holds.' `
            -DurationMilliseconds $secondary.Elapsed -EvidenceReference "GET $uri (two users)"
    }
    else {
        Add-Result -Id 'data-access-cross-user-isolation' -Category 'data-access' -Status 'fail' `
            -Summary "$($overlap.Count) trip(s) were visible to both verification users; ownership filtering is not being applied." `
            -DurationMilliseconds $secondary.Elapsed -EvidenceReference "GET $uri (two users)"
    }
}

# ---------------------------------------------------------------------------
# Category: core-trip-workflow
# ---------------------------------------------------------------------------

function Test-CoreTripWorkflow {
    if (-not $online) {
        Add-NotRun -Id 'core-trip-workflow-create' -Category 'core-trip-workflow' -Reason 'Offline mode: no trip was created.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($AccessToken) -or -not $script:ApiReachable) {
        Add-NotRun -Id 'core-trip-workflow-create' -Category 'core-trip-workflow' -Reason 'Skipped: no authenticated session was available.'
        return
    }

    $uri = "$($WebUrl.TrimEnd('/'))/api/trips"
    $payload = @{
        name      = "verification-$ReleaseId"
        startDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
        endDate   = (Get-Date).ToUniversalTime().AddDays(1).ToString('yyyy-MM-dd')
    }

    $created = Invoke-VerificationRequest -Uri $uri -Method 'POST' -Token $AccessToken -Body $payload
    if (@(200, 201) -contains $created.StatusCode) {
        try { $script:CreatedTripId = ($created.Content | ConvertFrom-Json).id } catch { $script:CreatedTripId = $null }
        Add-Result -Id 'core-trip-workflow-create' -Category 'core-trip-workflow' -Status 'pass' `
            -Summary 'A trip was created through the public API.' `
            -DurationMilliseconds $created.Elapsed -EvidenceReference "POST $uri"
    }
    else {
        Add-Result -Id 'core-trip-workflow-create' -Category 'core-trip-workflow' -Status 'fail' `
            -Summary "Creating a trip failed with status $($created.StatusCode); the core workflow is broken." `
            -DurationMilliseconds $created.Elapsed -EvidenceReference "POST $uri"
    }
}

# ---------------------------------------------------------------------------
# Category: persistence-after-restart
# ---------------------------------------------------------------------------

function Test-PersistenceAfterRestart {
    if (-not $online) {
        Add-NotRun -Id 'persistence-after-restart-trip' -Category 'persistence-after-restart' -Reason 'Offline mode: persistence was not exercised.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:CreatedTripId)) {
        Add-NotRun -Id 'persistence-after-restart-trip' -Category 'persistence-after-restart' `
            -Reason 'Skipped: no verification trip was created, so persistence could not be observed.'
        return
    }

    # The API scales to zero, so a second request after the replica has been recycled reads
    # from PostgreSQL rather than from in-process state. Restarting the API revision is the
    # cheapest way to force that without waiting out the scale-to-zero window.
    if (-not [string]::IsNullOrWhiteSpace($ResourceGroup)) {
        $apiApp = "ca-api-$EnvironmentName"
        $revision = Invoke-AzCommand -Argument @(
            'containerapp', 'revision', 'list', '-n', $apiApp, '-g', $ResourceGroup,
            '--query', '[?properties.active].name | [0]', '-o', 'tsv')
        if (-not [string]::IsNullOrWhiteSpace("$revision")) {
            Invoke-AzCommand -Argument @('containerapp', 'revision', 'restart', '-n', $apiApp, '-g', $ResourceGroup, '--revision', "$revision") | Out-Null
        }
    }

    $uri = "$($WebUrl.TrimEnd('/'))/api/trips/$($script:CreatedTripId)"
    $response = Invoke-VerificationRequest -Uri $uri -Token $AccessToken
    if ($response.StatusCode -eq 200) {
        Add-Result -Id 'persistence-after-restart-trip' -Category 'persistence-after-restart' -Status 'pass' `
            -Summary 'The verification trip survived an API replica restart; the Azure Files volume is durable.' `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }
    else {
        Add-Result -Id 'persistence-after-restart-trip' -Category 'persistence-after-restart' -Status 'fail' `
            -Summary "The verification trip was not readable after a restart (status $($response.StatusCode)); data is not durable." `
            -DurationMilliseconds $response.Elapsed -EvidenceReference "GET $uri"
    }

    # Clean up so verification never accumulates data in production.
    Invoke-VerificationRequest -Uri $uri -Method 'DELETE' -Token $AccessToken | Out-Null
}

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------

# Order matters: cheap reachability first, so an unreachable environment short-circuits the
# expensive workflow checks instead of producing a wall of unrelated failures.
Test-SecureReachability
Test-Liveness
Test-Readiness
Test-SignIn
Test-AuthenticatedApi
Test-DataAccess
Test-CoreTripWorkflow
Test-PersistenceAfterRestart

$checkArray = $checks.ToArray()
$overallStatus = Get-VerificationOverallStatus -Check $checkArray
$classification = Get-VerificationFailureClassification -Check $checkArray

$report = [pscustomobject]@{
    schemaVersion   = $contract.SchemaVersion
    releaseId       = $ReleaseId
    environmentName = $EnvironmentName
    startedAtUtc    = $startedAt
    completedAtUtc  = Get-DeploymentUtcNow
    overallStatus   = $overallStatus
    checks          = $checkArray
    failureCategory = if ($null -eq $classification) { $null } else { $classification.failureCategory }
    recoveryAction  = if ($null -eq $classification) { $null } else { $classification.recoveryAction }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Get-DeploymentEvidencePath -Kind 'verification' -ReleaseId $ReleaseId -Root (Join-Path $repoRoot 'artifacts/deployment-evidence')
}

$written = Write-DeploymentEvidence -Evidence $report -Path $OutputPath -SchemaPath $schemaPath `
    -KnownSecret @($AccessToken, $SecondaryAccessToken)

Write-Host ""
Write-Host "Deployment verification: $overallStatus" -ForegroundColor $(if ($overallStatus -eq 'pass') { 'Green' } else { 'Red' })
foreach ($check in $checkArray) {
    $colour = switch ($check.status) { 'pass' { 'Green' } 'fail' { 'Red' } default { 'Yellow' } }
    Write-Host ("  [{0,-7}] {1,-36} {2}" -f $check.status, $check.id, $check.summary) -ForegroundColor $colour
}
if ($null -ne $classification) {
    Write-Host ""
    Write-Host "Failure category: $($classification.failureCategory)" -ForegroundColor Red
    Write-Host "Recovery action : $($classification.recoveryAction)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Evidence: $written"

exit (Get-DeploymentExitCode -OverallStatus $overallStatus)
