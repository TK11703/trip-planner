<#
.SYNOPSIS
    Post-deployment verification gate for the Trip Planner production release.

.DESCRIPTION
    Exercises the deployed release end to end and writes a sanitized, schema-conformant
    report to artifacts/deployment-evidence/verification-<releaseId>.json.

    Exit code 0 means the release is verified. Any other exit code means the release is
    unproven and must not be marked complete. `not-run` is deliberately treated as a
    failure: an unverified release is not a verified one.

    The script is strictly read-only and unauthenticated. It verifies what an anonymous
    caller on the public internet can observe: that the site serves over HTTPS, reports
    itself live and ready, and challenges anonymous callers on protected routes.

    It deliberately does not exercise authenticated data access. The API has internal-only
    ingress and the web app is Blazor Server, so sessions are cookie-based rather than
    bearer; no external caller can obtain one. Proving authenticated behaviour from here
    would require either exposing the API publicly or adding a privileged endpoint that
    acts on a user's behalf, and both trade away more than the check is worth. Authenticated
    behaviour is covered by tests/TripPlanner.E2E.Tests instead.

.PARAMETER ReleaseId
    Immutable release identifier, normally the full commit SHA.

.PARAMETER EnvironmentName
    The azd environment name (AZURE_ENV_NAME).

.PARAMETER WebUrl
    Public HTTPS base URL of the web app, for example https://web.<domain>.

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

# Populated by the reachability check and reused by later checks so they do not repeat
# work that has already failed.
$script:ApiReachable = $false

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

    # Invoke-WebRequest is not usable here. `-MaximumRedirection 0` throws on PowerShell
    # 7.6 ("Operation is not valid due to the current state of the object") instead of
    # handing back the 3xx, and dropping it lets the client follow the redirect so a
    # sign-in challenge arrives as a 200 from login.microsoftonline.com -- which reads as
    # "authentication is not being enforced". HttpClient gives us the unfollowed response.
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = [bool]$AllowRedirect
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)

    try {
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method), $Uri)

        if (-not [string]::IsNullOrWhiteSpace($Token)) {
            $request.Headers.Authorization =
                [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
        }

        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(
                ($Body | ConvertTo-Json -Depth 8), [System.Text.Encoding]::UTF8, 'application/json')
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $stopwatch.Stop()

        $headers = @{}
        foreach ($header in $response.Headers) { $headers[$header.Key] = @($header.Value) }
        foreach ($header in $response.Content.Headers) { $headers[$header.Key] = @($header.Value) }

        [pscustomobject]@{
            Succeeded  = $true
            StatusCode = [int]$response.StatusCode
            Content    = [string]$content
            Headers    = $headers
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
    finally {
        $client.Dispose()
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
        return
    }

    if (-not $script:ApiReachable) {
        Add-NotRun -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Reason 'Skipped: the web app was not reachable.'
        return
    }

    # `/profile` is an API-backed page carrying [Authorize]: rendering it forces a call to
    # the API for the caller's own record. Probing it anonymously therefore exercises the
    # authorization boundary in front of the API without needing a public API surface.
    $uri = "$($WebUrl.TrimEnd('/'))/profile"

    $anonymous = Invoke-VerificationRequest -Uri $uri
    if (@(401, 403, 302) -contains $anonymous.StatusCode) {
        Add-Result -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Status 'pass' `
            -Summary "An API-backed page rejected an anonymous caller with status $($anonymous.StatusCode)." `
            -DurationMilliseconds $anonymous.Elapsed -EvidenceReference "GET $uri (anonymous)"
    }
    else {
        Add-Result -Id 'authenticated-api-rejects-anonymous' -Category 'authenticated-api' -Status 'fail' `
            -Summary "An API-backed page answered an anonymous caller with status $($anonymous.StatusCode); authorization is not being enforced." `
            -DurationMilliseconds $anonymous.Elapsed -EvidenceReference "GET $uri (anonymous)"
    }
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

$written = Write-DeploymentEvidence -Evidence $report -Path $OutputPath -SchemaPath $schemaPath

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
