<#
.SYNOPSIS
    Shared helpers and configuration constants for Trip Planner deployment automation.

.DESCRIPTION
    Single source of truth for production configuration key names, Key Vault secret
    logical names, and evidence-file handling used by both deployment-readiness.ps1
    and deployment-verify.ps1. Keeping these here prevents the readiness gate and the
    runtime wiring from drifting apart.

    No function in this module ever emits a secret value. All evidence passes through
    Protect-DeploymentSecret before being written or logged.
#>

Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Contract constants
# ---------------------------------------------------------------------------

$script:ReadinessCategories = @(
    'azure-context'
    'resource-provider'
    'quota-policy'
    'configuration'
    'identity'
    'secret-reference'
    'entra'
    'data-protection'
    'artifact'
    'infrastructure-preview'
    'database-recovery'
    'security'
)

$script:VerificationCategories = @(
    'secure-reachability'
    'liveness'
    'readiness'
    'sign-in'
    'authenticated-api'
)

# Maps a failing verification category to the cause category and the first recovery step.
# Keeping this beside the category list guarantees every category is classifiable, so a
# failing release can never produce a report without an actionable next step.
$script:VerificationFailureClassification = [ordered]@{
    'secure-reachability'       = @{
        FailureCategory = 'ingress'
        RecoveryAction  = 'Confirm the web container app has external ingress with allowInsecure=false and that the revision is provisioned. If the revision is unhealthy, roll back to the previous commit SHA.'
    }
    'liveness'                  = @{
        FailureCategory = 'application-startup'
        RecoveryAction  = 'Inspect the container app revision logs in Log Analytics for startup exceptions, then roll back to the previous commit SHA if the new image cannot start.'
    }
    'readiness'                 = @{
        FailureCategory = 'dependency'
        RecoveryAction  = 'A dependency reported unhealthy. Check PostgreSQL replica status, the migration ledger, and Azure OpenAI configuration before retrying the release.'
    }
    'sign-in'                   = @{
        FailureCategory = 'authentication'
        RecoveryAction  = 'Verify the Entra web registration redirect URI matches the live ingress FQDN and that the client secret in Key Vault is current. See docs/operations/production-runbook.md section 1.'
    }
    'authenticated-api'         = @{
        FailureCategory = 'authorization'
        RecoveryAction  = 'A protected route did not challenge an anonymous caller. Verify the API audience, the exposed access_as_user scope, and that admin consent has been granted for the web registration.'
    }
}

# Used when the run could not complete rather than when a specific check failed.
$script:VerificationIncompleteClassification = @{
    FailureCategory = 'verification-incomplete'
    RecoveryAction  = 'Verification could not reach the deployed environment, so the release is unproven. Re-run scripts/deployment-verify.ps1 with connectivity, and treat the release as unverified until it passes.'
}

# Non-secret runtime configuration keys, in Container Apps environment-variable form.
$script:RequiredWebConfigKeys = @(
    'AzureEntra__Instance'
    'AzureEntra__TenantId'
    'AzureEntra__ClientId'
    'AzureEntra__ApiScopes__0'
    # Losing these silently downgrades the app to having no client credential at all, which
    # breaks every sign-in. Nothing else fails first, so the gate has to catch it.
    'AzureEntra__ClientCredentials__0__SourceType'
    'AzureEntra__ClientCredentials__0__ManagedIdentityClientId'
    'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
    'DataProtection__BlobUri'
    'DataProtection__KeyVaultKeyUri'
    'services__api__https__0'
)

$script:RequiredApiConfigKeys = @(
    'AzureEntra__Instance'
    'AzureEntra__TenantId'
    'AzureEntra__ClientId'
    'AzureEntra__Audience'
    'AzureOpenAI__Endpoint'
    'AzureOpenAI__DeploymentName'
    'RunDatabaseMigrations'
)

# Logical Key Vault secret names. Container Apps resolves these as versionless references.
# The Postgres admin password is no longer an application credential: the API authenticates
# to Flexible Server with its managed identity. It survives only for schema bootstrap and
# break-glass access, so nothing consumes it at runtime. The web app likewise holds no
# client secret -- its managed identity is federated onto the app registration instead.
$script:SecretReferences = @(
    [pscustomobject]@{ LogicalName = 'postgres-password'; Consumers = @(); Required = $true }
)

# Azure resource providers the deployment depends on.
$script:RequiredResourceProviders = @(
    'Microsoft.App'
    'Microsoft.ContainerRegistry'
    'Microsoft.KeyVault'
    'Microsoft.ManagedIdentity'
    'Microsoft.OperationalInsights'
    'Microsoft.Storage'
    'Microsoft.CognitiveServices'
    'Microsoft.DBforPostgreSQL'
)

# azd environment variables that must be present before any provisioning call.
$script:RequiredEnvironmentVariables = @(
    'AZURE_ENV_NAME'
    'AZURE_LOCATION'
    'AZURE_SUBSCRIPTION_ID'
    'AZURE_RESOURCE_GROUP'
    'AZURE_TENANT_ID'
    'AZURE_ENTRA_WEB_CLIENT_ID'
    'AZURE_ENTRA_API_CLIENT_ID'
)

function Get-TripPlannerDeploymentContract {
    <#
    .SYNOPSIS
        Returns the shared configuration contract consumed by readiness and verification.
    #>
    [CmdletBinding()]
    param()

    [pscustomobject]@{
        ReadinessCategories       = $script:ReadinessCategories
        VerificationCategories    = $script:VerificationCategories
        RequiredWebConfigKeys     = $script:RequiredWebConfigKeys
        RequiredApiConfigKeys     = $script:RequiredApiConfigKeys
        SecretReferences          = $script:SecretReferences
        RequiredResourceProviders = $script:RequiredResourceProviders
        RequiredEnvironmentVars   = $script:RequiredEnvironmentVariables
        SchemaVersion             = '1.0'
        DataProtectionContainer   = 'dataprotection'
        # Flexible Server keeps a continuous restore window rather than discrete dumps, so
        # recovery is expressed as retention days and a freshness bound on the restore point.
        BackupRetentionDays       = 7
        RestorePointMaxAgeHours   = 24
    }
}

# ---------------------------------------------------------------------------
# Time
# ---------------------------------------------------------------------------

function Get-DeploymentUtcNow {
    <#
    .SYNOPSIS
        Returns the current UTC time formatted as the ISO-8601 string the evidence schemas require.
    #>
    [CmdletBinding()]
    param(
        [datetime] $Value = [datetime]::UtcNow
    )

    $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

# ---------------------------------------------------------------------------
# Redaction
# ---------------------------------------------------------------------------

$script:SecretPatterns = @(
    # Connection-string credential segments.
    @{ Pattern = '(?i)(password|pwd)\s*=\s*[^;"\r\n]+'; Replacement = 'Password=***REDACTED***' }
    @{ Pattern = '(?i)(accountkey)\s*=\s*[^;"\r\n]+'; Replacement = 'AccountKey=***REDACTED***' }
    @{ Pattern = '(?i)(sharedaccesssignature|sig)\s*=\s*[^;&"\r\n]+'; Replacement = 'Signature=***REDACTED***' }
    # Bearer tokens and JWTs.
    @{ Pattern = '(?i)bearer\s+[A-Za-z0-9._\-]+'; Replacement = 'Bearer ***REDACTED***' }
    @{ Pattern = 'eyJ[A-Za-z0-9._\-]{20,}'; Replacement = '***REDACTED-JWT***' }
    # Key Vault secret values are never URIs; a `?` query on a storage URL usually is a SAS.
    @{ Pattern = '(?i)(\?|&)(sv|sig|se|sp)=[^"\s&]+'; Replacement = '?***REDACTED-SAS***' }
)

function Protect-DeploymentSecret {
    <#
    .SYNOPSIS
        Redacts credential-shaped substrings from text destined for logs or evidence files.

    .DESCRIPTION
        Applies structural patterns (connection-string segments, tokens, SAS query strings)
        and additionally masks any explicitly supplied known secret values.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string] $InputText,

        [string[]] $KnownSecret = @()
    )

    process {
        if ([string]::IsNullOrEmpty($InputText)) {
            return $InputText
        }

        $result = $InputText

        foreach ($secret in $KnownSecret) {
            if (-not [string]::IsNullOrWhiteSpace($secret) -and $secret.Length -ge 4) {
                $result = $result.Replace($secret, '***REDACTED***')
            }
        }

        foreach ($rule in $script:SecretPatterns) {
            $result = [regex]::Replace($result, $rule.Pattern, $rule.Replacement)
        }

        $result
    }
}

function Protect-DeploymentObject {
    <#
    .SYNOPSIS
        Recursively redacts every string value in an evidence object graph.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $InputObject,

        [string[]] $KnownSecret = @()
    )

    if ($null -eq $InputObject) { return $null }

    if ($InputObject -is [string]) {
        return Protect-DeploymentSecret -InputText $InputObject -KnownSecret $KnownSecret
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $copy = @{}
        foreach ($key in $InputObject.Keys) {
            $copy[$key] = Protect-DeploymentObject -InputObject $InputObject[$key] -KnownSecret $KnownSecret
        }
        return $copy
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $items = @($InputObject | ForEach-Object { Protect-DeploymentObject -InputObject $_ -KnownSecret $KnownSecret })
        # Comma operator keeps an empty or single-element array an array instead of
        # collapsing to $null / a scalar, which would break schema validation.
        return , $items
    }

    if ($InputObject -is [psobject] -and @($InputObject.PSObject.Properties).Count -gt 0 -and
        $InputObject -isnot [ValueType]) {
        $copy = [ordered]@{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $copy[$property.Name] = Protect-DeploymentObject -InputObject $property.Value -KnownSecret $KnownSecret
        }
        return [pscustomobject]$copy
    }

    return $InputObject
}

# ---------------------------------------------------------------------------
# Check construction
# ---------------------------------------------------------------------------

function New-DeploymentCheck {
    <#
    .SYNOPSIS
        Builds a schema-shaped readiness or verification check result.

    .DESCRIPTION
        A `fail` status must always carry a corrective action so the operator knows the
        next step. This function enforces that rather than leaving it to each call site.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Category,
        [Parameter(Mandatory = $true)][ValidateSet('pass', 'fail', 'not-applicable')][string] $Status,
        [Parameter(Mandatory = $true)][string] $Summary,
        [bool] $Required = $true,
        [string] $CorrectiveAction,
        [string] $EvidenceReference
    )

    if ($Status -eq 'fail' -and [string]::IsNullOrWhiteSpace($CorrectiveAction)) {
        throw "Check '$Id' failed but supplied no CorrectiveAction. Every failure must be actionable."
    }

    [pscustomobject]@{
        id                = $Id
        category          = $Category
        required          = $Required
        status            = $Status
        summary           = $Summary
        correctiveAction  = if ([string]::IsNullOrWhiteSpace($CorrectiveAction)) { $null } else { $CorrectiveAction }
        evidenceReference = if ([string]::IsNullOrWhiteSpace($EvidenceReference)) { $null } else { $EvidenceReference }
    }
}

function New-VerificationCheck {
    <#
    .SYNOPSIS
        Builds a schema-shaped post-deployment verification check result.

    .DESCRIPTION
        The verification schema differs from readiness: every check is mandatory (there is
        no `required` flag), timings are recorded, and `not-run` is a distinct outcome from
        `fail` so an unreachable environment is never mistaken for a healthy one.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][ValidateSet(
            'secure-reachability', 'liveness', 'readiness', 'sign-in',
            'authenticated-api')]
        [string] $Category,
        [Parameter(Mandatory = $true)][ValidateSet('pass', 'fail', 'not-run')][string] $Status,
        [Parameter(Mandatory = $true)][string] $Summary,
        [int] $DurationMilliseconds = 0,
        [string] $EvidenceReference
    )

    if ($DurationMilliseconds -lt 0) { $DurationMilliseconds = 0 }

    [pscustomobject]@{
        id                   = $Id
        category             = $Category
        status               = $Status
        durationMilliseconds = $DurationMilliseconds
        summary              = Protect-DeploymentSecret -InputText $Summary
        evidenceReference    = if ([string]::IsNullOrWhiteSpace($EvidenceReference)) { $null } else { $EvidenceReference }
    }
}

function Get-VerificationFailureClassification {
    <#
    .SYNOPSIS
        Resolves the failure category and recovery action for a verification run.

    .DESCRIPTION
        Returns $null when every check passed. Otherwise the first failing check (in
        execution order) determines the classification, because later failures are usually
        consequences of the first one. A run with no failures but with `not-run` checks is
        classified as incomplete, not as a pass.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Check
    )

    foreach ($item in $Check) {
        if ($item.status -eq 'fail') {
            $classification = $script:VerificationFailureClassification[$item.category]
            if ($null -eq $classification) {
                # Defensive: an unmapped category must still yield an actionable report.
                return [pscustomobject]@{
                    failureCategory = 'unclassified'
                    recoveryAction  = "Check '$($item.id)' failed in category '$($item.category)'. Investigate the check output, then add a classification for this category."
                }
            }
            return [pscustomobject]@{
                failureCategory = $classification.FailureCategory
                recoveryAction  = $classification.RecoveryAction
            }
        }
    }

    if (@($Check | Where-Object { $_.status -eq 'not-run' }).Count -gt 0) {
        return [pscustomobject]@{
            failureCategory = $script:VerificationIncompleteClassification.FailureCategory
            recoveryAction  = $script:VerificationIncompleteClassification.RecoveryAction
        }
    }

    $null
}

function Get-VerificationOverallStatus {
    <#
    .SYNOPSIS
        Resolves the overall verification status. Only an all-pass run passes.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Check
    )

    if ($Check.Count -eq 0) { return 'fail' }

    foreach ($item in $Check) {
        # `not-run` is a failure: an unverified release is not a verified one.
        if ($item.status -ne 'pass') { return 'fail' }
    }

    'pass'
}

function Get-DeploymentOverallStatus {

    <#
    .SYNOPSIS
        Resolves overall pass/fail from a check collection, honouring accepted risks.

    .DESCRIPTION
        A required check that fails makes the whole report fail unless an accepted risk
        waives that specific check id. Optional checks never fail the report.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Check,
        [object[]] $AcceptedRisk = @()
    )

    $waivedIds = @($AcceptedRisk | ForEach-Object { $_.checkId })

    foreach ($item in $Check) {
        if ($item.status -eq 'fail' -and $item.required -and $waivedIds -notcontains $item.id) {
            return 'fail'
        }
    }

    'pass'
}

function Get-DeploymentExitCode {
    <#
    .SYNOPSIS
        Maps an overall status to a process exit code (0 = pass, 1 = fail).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory = $true)][string] $OverallStatus
    )

    if ($OverallStatus -eq 'pass') { 0 } else { 1 }
}

# ---------------------------------------------------------------------------
# Evidence files
# ---------------------------------------------------------------------------

function Get-DeploymentEvidencePath {
    <#
    .SYNOPSIS
        Resolves (and creates) the evidence output path for a release artifact.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][string] $Kind,
        [Parameter(Mandatory = $true)][string] $ReleaseId,
        [string] $Root = (Join-Path (Get-Location) 'artifacts/deployment-evidence')
    )

    if (-not (Test-Path -LiteralPath $Root)) {
        New-Item -ItemType Directory -Path $Root -Force | Out-Null
    }

    $safeRelease = ($ReleaseId -replace '[^A-Za-z0-9._-]', '-')
    Join-Path $Root "$Kind-$safeRelease.json"
}

function Test-DeploymentEvidence {
    <#
    .SYNOPSIS
        Validates an evidence document against its JSON Schema contract.

    .OUTPUTS
        Boolean. Writes schema errors to the error stream when validation fails.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)][string] $Json,
        [Parameter(Mandatory = $true)][string] $SchemaPath
    )

    if (-not (Test-Path -LiteralPath $SchemaPath)) {
        Write-Error "Schema not found at '$SchemaPath'."
        return $false
    }

    try {
        # -ErrorAction Stop so schema violations surface as terminating errors we can report.
        return [bool](Test-Json -Json $Json -SchemaFile $SchemaPath -ErrorAction Stop)
    }
    catch {
        Write-Error "Evidence failed schema validation: $($_.Exception.Message)"
        return $false
    }
}

function Write-DeploymentEvidence {
    <#
    .SYNOPSIS
        Redacts, serializes, schema-validates, and writes an evidence document.

    .DESCRIPTION
        Validation happens before the file is written so an invalid document never
        becomes a release artifact.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)] $Evidence,
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $SchemaPath,
        [string[]] $KnownSecret = @()
    )

    $sanitized = Protect-DeploymentObject -InputObject $Evidence -KnownSecret $KnownSecret
    $json = $sanitized | ConvertTo-Json -Depth 12

    if (-not (Test-DeploymentEvidence -Json $json -SchemaPath $SchemaPath)) {
        throw "Refusing to write '$Path': evidence does not conform to $SchemaPath."
    }

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
    $Path
}

# ---------------------------------------------------------------------------
# Azure CLI helper
# ---------------------------------------------------------------------------

function Invoke-AzCommand {
    <#
    .SYNOPSIS
        Runs an `az` CLI command and returns parsed JSON, or $null when the call fails.

    .DESCRIPTION
        Readiness checks treat a failed lookup as a check failure rather than a script
        crash, so this swallows the non-zero exit and returns $null.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string[]] $Argument
    )

    $output = & az @Argument 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Verbose "az $($Argument -join ' ') failed: $(Protect-DeploymentSecret -InputText ([string]$output))"
        return $null
    }

    $text = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }

    try { return $text | ConvertFrom-Json }
    catch { return $text }
}

Export-ModuleMember -Function @(
    'Get-TripPlannerDeploymentContract'
    'Get-DeploymentUtcNow'
    'Protect-DeploymentSecret'
    'Protect-DeploymentObject'
    'New-DeploymentCheck'
    'New-VerificationCheck'
    'Get-VerificationFailureClassification'
    'Get-VerificationOverallStatus'
    'Get-DeploymentOverallStatus'
    'Get-DeploymentExitCode'
    'Get-DeploymentEvidencePath'
    'Test-DeploymentEvidence'
    'Write-DeploymentEvidence'
    'Invoke-AzCommand'
)
