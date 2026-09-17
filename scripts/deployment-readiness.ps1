<#
.SYNOPSIS
    Pre-deployment readiness gate for the Trip Planner production release.

.DESCRIPTION
    Evaluates every prerequisite that must hold before provisioning or deploying, and
    writes a sanitized, schema-conformant report to artifacts/deployment-evidence/.

    Exit code 0 means the release may proceed. Exit code 1 means at least one required
    check failed without an accepted-risk waiver and the release must be blocked.

    This script is read-only with respect to Azure. It queries and previews; it never
    creates, updates, or deletes a resource.

.PARAMETER ReleaseId
    Immutable release identifier, normally the full commit SHA.

.PARAMETER EnvironmentName
    The azd environment name (AZURE_ENV_NAME).

.PARAMETER FirstRelease
    Set for the very first deployment into an empty resource group. Checks that inspect
    already-deployed resources report 'not-applicable' instead of failing.

.PARAMETER Offline
    Skips every Azure CLI call. Intended for tests and for validating report shape
    without a subscription. Azure-dependent checks report 'not-applicable'.

.EXAMPLE
    ./scripts/deployment-readiness.ps1 -ReleaseId $env:GITHUB_SHA -EnvironmentName tripplanner-prod
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ReleaseId,

    [string] $EnvironmentName = $env:AZURE_ENV_NAME,

    [string] $SubscriptionId = $env:AZURE_SUBSCRIPTION_ID,

    [string] $ResourceGroup = $env:AZURE_RESOURCE_GROUP,

    [string] $AcceptedRiskPath = (Join-Path $PSScriptRoot '../.azure/accepted-risks.json'),

    [string] $OutputPath,

    [switch] $FirstRelease,

    [switch] $SkipInfrastructurePreview,

    [switch] $Offline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TripPlanner.Deployment.psm1') -Force

$contract = Get-TripPlannerDeploymentContract
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$schemaPath = Join-Path $repoRoot 'specs/026-azure-deployment-readiness/contracts/readiness-report.schema.json'

# CI exports the azd environment before invoking this script; an operator following the
# quickstart has only selected it. Hydrating here means both paths see the same context
# instead of the local run failing on variables azd already knows.
if (-not $Offline -and (Get-Command azd -CommandType Application -ErrorAction SilentlyContinue)) {
    foreach ($line in @(& azd env get-values 2>$null)) {
        if ("$line" -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"?(.*?)"?\s*$') {
            $name = $Matches[1]
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
                Set-Item -Path "Env:$name" -Value $Matches[2]
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($EnvironmentName)) { $EnvironmentName = $env:AZURE_ENV_NAME }
    if ([string]::IsNullOrWhiteSpace($SubscriptionId)) { $SubscriptionId = $env:AZURE_SUBSCRIPTION_ID }
    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) { $ResourceGroup = $env:AZURE_RESOURCE_GROUP }
}

if ([string]::IsNullOrWhiteSpace($EnvironmentName)) {
    $EnvironmentName = 'unknown'
}

# Resources that only exist after a successful first deployment.
$deployedResourcesExpected = -not ($FirstRelease -or $Offline)
$azureAvailable = -not $Offline

$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Category,
        [Parameter(Mandatory = $true)][string] $Status,
        [Parameter(Mandatory = $true)][string] $Summary,
        [bool] $Required = $true,
        [string] $CorrectiveAction,
        [string] $EvidenceReference
    )

    $checks.Add((New-DeploymentCheck -Id $Id -Category $Category -Status $Status -Summary $Summary `
                -Required $Required -CorrectiveAction $CorrectiveAction -EvidenceReference $EvidenceReference))
}

function Add-SkippedCheck {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Category,
        [Parameter(Mandatory = $true)][string] $Reason
    )

    Add-Check -Id $Id -Category $Category -Status 'not-applicable' -Summary $Reason
}

# ---------------------------------------------------------------------------
# Category: azure-context
# ---------------------------------------------------------------------------

function Test-AzureContext {
    $missingVars = @($contract.RequiredEnvironmentVars | Where-Object {
            [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
        })

    if ($missingVars.Count -eq 0) {
        Add-Check -Id 'azure-context-environment-variables' -Category 'azure-context' -Status 'pass' `
            -Summary 'All required azd environment variables are set.'
    }
    else {
        Add-Check -Id 'azure-context-environment-variables' -Category 'azure-context' -Status 'fail' `
            -Summary "Missing required environment variables: $($missingVars -join ', ')." `
            -CorrectiveAction "Set each missing value with 'azd env set <NAME> <value>' for environment '$EnvironmentName', then re-run readiness."
    }

    if (-not $azureAvailable) {
        Add-SkippedCheck -Id 'azure-context-cloud' -Category 'azure-context' -Reason 'Offline mode: active cloud not queried.'
        Add-SkippedCheck -Id 'azure-context-subscription' -Category 'azure-context' -Reason 'Offline mode: subscription context not queried.'
        Add-SkippedCheck -Id 'azure-context-resource-group' -Category 'azure-context' -Reason 'Offline mode: resource group not queried.'
        return
    }

    # A CLI left pointed at a sovereign cloud still authenticates and still answers
    # queries -- it just answers them about the wrong Azure. Every downstream check would
    # then be measuring a cloud this release will never deploy to. az and azd keep
    # separate cloud settings, and azd's is global rather than per-project, so an operator
    # who corrects one can still be silently wrong in the other.
    $cloud = "$(Invoke-AzCommand -Argument @('cloud', 'show', '--query', 'name', '-o', 'tsv'))".Trim()

    $azdCloud = $null
    if (Get-Command azd -CommandType Application -ErrorAction SilentlyContinue) {
        $azdCloudJson = & azd config get cloud 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace("$azdCloudJson")) {
            try { $azdCloud = ($azdCloudJson | ConvertFrom-Json).name } catch { $azdCloud = $null }
        }

        # azd falls back to Commercial when nothing is configured.
        if ([string]::IsNullOrWhiteSpace($azdCloud)) { $azdCloud = 'AzureCloud' }
    }

    $wrongClouds = @()
    if ($cloud -ne 'AzureCloud') {
        $wrongClouds += "Azure CLI is targeting '$(if ($cloud) { $cloud } else { 'an unknown cloud' })'"
    }
    if ($null -ne $azdCloud -and $azdCloud -ne 'AzureCloud') {
        $wrongClouds += "azd is targeting '$azdCloud'"
    }

    if ($wrongClouds.Count -eq 0) {
        Add-Check -Id 'azure-context-cloud' -Category 'azure-context' -Status 'pass' `
            -Summary 'Azure CLI and azd are both targeting Azure Commercial (AzureCloud).' `
            -EvidenceReference 'az cloud show; azd config get cloud'
    }
    else {
        Add-Check -Id 'azure-context-cloud' -Category 'azure-context' -Status 'fail' `
            -Summary "$($wrongClouds -join '; '), but this release deploys to Azure Commercial." `
            -CorrectiveAction "Run 'az cloud set --name AzureCloud' and 'az login', and 'azd config set cloud.name AzureCloud' followed by 'azd auth login --tenant-id <tenant>'. Results from another cloud describe different resources. Note azd's cloud setting is global, so this also affects other azd projects."
    }

    $account = Invoke-AzCommand -Argument @('account', 'show', '-o', 'json')
    if ($null -eq $account) {
        Add-Check -Id 'azure-context-subscription' -Category 'azure-context' -Status 'fail' `
            -Summary 'No authenticated Azure CLI context.' `
            -CorrectiveAction "Run 'az login' (or configure the workflow OIDC federated credential) and re-run readiness."
    }
    elseif ($SubscriptionId -and $account.id -ne $SubscriptionId) {
        Add-Check -Id 'azure-context-subscription' -Category 'azure-context' -Status 'fail' `
            -Summary "Active subscription '$($account.id)' does not match the expected deployment subscription." `
            -CorrectiveAction "Run 'az account set --subscription $SubscriptionId' so the deployment targets the intended subscription."
    }
    else {
        Add-Check -Id 'azure-context-subscription' -Category 'azure-context' -Status 'pass' `
            -Summary "Authenticated against subscription '$($account.id)' in tenant '$($account.tenantId)'." `
            -EvidenceReference "az account show --subscription $($account.id)"
    }

    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
        Add-Check -Id 'azure-context-resource-group' -Category 'azure-context' -Status 'fail' `
            -Summary 'No target resource group supplied.' `
            -CorrectiveAction "Set AZURE_RESOURCE_GROUP with 'azd env set AZURE_RESOURCE_GROUP <name>'. The Bicep deployment is resource-group scoped and cannot infer it."
        return
    }

    $group = Invoke-AzCommand -Argument @('group', 'show', '--name', $ResourceGroup, '-o', 'json')
    if ($null -eq $group) {
        if ($FirstRelease) {
            Add-Check -Id 'azure-context-resource-group' -Category 'azure-context' -Status 'pass' `
                -Summary "Resource group '$ResourceGroup' does not exist yet and will be created by the first release."
        }
        else {
            Add-Check -Id 'azure-context-resource-group' -Category 'azure-context' -Status 'fail' `
                -Summary "Resource group '$ResourceGroup' was not found." `
                -CorrectiveAction "Create it with 'az group create --name $ResourceGroup --location <region>', or re-run with -FirstRelease if this is the initial deployment."
        }
    }
    else {
        Add-Check -Id 'azure-context-resource-group' -Category 'azure-context' -Status 'pass' `
            -Summary "Resource group '$ResourceGroup' exists in '$($group.location)'."
    }
}

# ---------------------------------------------------------------------------
# Category: resource-provider
# ---------------------------------------------------------------------------

function Test-ResourceProviders {
    if (-not $azureAvailable) {
        Add-SkippedCheck -Id 'resource-providers-registered' -Category 'resource-provider' -Reason 'Offline mode: provider registration not queried.'
        return
    }

    $unregistered = [System.Collections.Generic.List[string]]::new()
    foreach ($provider in $contract.RequiredResourceProviders) {
        $state = Invoke-AzCommand -Argument @('provider', 'show', '--namespace', $provider, '--query', 'registrationState', '-o', 'tsv')
        if ("$state" -ne 'Registered') {
            $unregistered.Add($provider)
        }
    }

    if ($unregistered.Count -eq 0) {
        Add-Check -Id 'resource-providers-registered' -Category 'resource-provider' -Status 'pass' `
            -Summary "All $($contract.RequiredResourceProviders.Count) required resource providers are registered."
    }
    else {
        Add-Check -Id 'resource-providers-registered' -Category 'resource-provider' -Status 'fail' `
            -Summary "Unregistered resource providers: $($unregistered -join ', ')." `
            -CorrectiveAction "Register each one with 'az provider register --namespace <provider>' and wait for the state to reach Registered."
    }
}

# ---------------------------------------------------------------------------
# Category: quota-policy
# ---------------------------------------------------------------------------

function Test-QuotaAndPolicy {
    if (-not $azureAvailable) {
        Add-SkippedCheck -Id 'quota-container-apps' -Category 'quota-policy' -Reason 'Offline mode: quota not queried.'
        Add-SkippedCheck -Id 'policy-blocking-assignments' -Category 'quota-policy' -Reason 'Offline mode: policy assignments not queried.'
        return
    }

    $location = $env:AZURE_LOCATION
    if ([string]::IsNullOrWhiteSpace($location)) {
        Add-Check -Id 'quota-container-apps' -Category 'quota-policy' -Status 'fail' `
            -Summary 'Cannot evaluate Container Apps quota without a target region.' `
            -CorrectiveAction "Set the region with 'azd env set AZURE_LOCATION <region>'."
    }
    else {
        # Container Apps Consumption exposes managed-environment availability per region.
        # An unregistered provider reports a misleading (often empty or partial) location
        # list, so registration is checked first to avoid blaming the region.
        $registration = "$(Invoke-AzCommand -Argument @(
                'provider', 'show', '--namespace', 'Microsoft.App',
                '--query', 'registrationState', '-o', 'tsv'))".Trim()

        $available = @(Invoke-AzCommand -Argument @(
                'provider', 'show', '--namespace', 'Microsoft.App',
                '--query', "resourceTypes[?resourceType=='managedEnvironments'].locations[]", '-o', 'json'))

        $normalized = @($available | ForEach-Object { "$_".Replace(' ', '').ToLowerInvariant() })

        if ($registration -ne 'Registered') {
            Add-Check -Id 'quota-container-apps' -Category 'quota-policy' -Status 'fail' `
                -Summary "Container Apps availability in '$location' cannot be confirmed: Microsoft.App is '$(if ($registration) { $registration } else { 'unknown' })' in this subscription." `
                -CorrectiveAction "Run 'az provider register --namespace Microsoft.App', wait for Registered, then re-run readiness."
        }
        elseif ($normalized -contains $location.Replace(' ', '').ToLowerInvariant()) {
            Add-Check -Id 'quota-container-apps' -Category 'quota-policy' -Status 'pass' `
                -Summary "Container Apps managed environments are available in '$location'."
        }
        else {
            Add-Check -Id 'quota-container-apps' -Category 'quota-policy' -Status 'fail' `
                -Summary "Container Apps managed environments are not offered in '$location'." `
                -CorrectiveAction "Choose a supported region with 'azd env set AZURE_LOCATION <region>'."
        }
    }

    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
        Add-SkippedCheck -Id 'policy-blocking-assignments' -Category 'quota-policy' -Reason 'No resource group supplied; policy scope unknown.'
        return
    }

    $assignments = Invoke-AzCommand -Argument @('policy', 'assignment', 'list', '--resource-group', $ResourceGroup, '-o', 'json')
    $denyAssignments = @($assignments | Where-Object { $_.enforcementMode -eq 'Default' })

    # Deny policies do not automatically mean failure, but the operator must know they exist.
    Add-Check -Id 'policy-blocking-assignments' -Category 'quota-policy' -Status 'pass' -Required $false `
        -Summary "$($denyAssignments.Count) enforced policy assignment(s) apply to '$ResourceGroup'. Review them if provisioning is denied." `
        -EvidenceReference "az policy assignment list --resource-group $ResourceGroup"
}

# ---------------------------------------------------------------------------
# Category: configuration
# ---------------------------------------------------------------------------

function Get-ContainerAppEnvVarNames {
    param([string] $AppName)

    $names = Invoke-AzCommand -Argument @(
        'containerapp', 'show', '--name', $AppName, '--resource-group', $ResourceGroup,
        '--query', 'properties.template.containers[0].env[].name', '-o', 'json')

    if ($null -eq $names) { return @() }
    return @($names)
}

function Test-Configuration {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'configuration-web-keys' -Category 'configuration' -Reason 'First release or offline: the web container app does not exist yet.'
        Add-SkippedCheck -Id 'configuration-api-keys' -Category 'configuration' -Reason 'First release or offline: the api container app does not exist yet.'
        return
    }

    foreach ($target in @(
            @{ Id = 'configuration-web-keys'; App = "ca-web-$EnvironmentName"; Keys = $contract.RequiredWebConfigKeys; Label = 'web' }
            @{ Id = 'configuration-api-keys'; App = "ca-api-$EnvironmentName"; Keys = $contract.RequiredApiConfigKeys; Label = 'api' }
        )) {

        # PowerShell unrolls an empty array return to $null, so re-wrap before counting.
        $present = @(Get-ContainerAppEnvVarNames -AppName $target.App)
        if ($present.Count -eq 0) {
            Add-Check -Id $target.Id -Category 'configuration' -Status 'fail' `
                -Summary "Could not read configuration from container app '$($target.App)'." `
                -CorrectiveAction "Confirm the app exists in '$ResourceGroup' and that the deploying identity has reader access, or re-run with -FirstRelease."
            continue
        }

        $missing = @($target.Keys | Where-Object { $present -notcontains $_ })
        if ($missing.Count -eq 0) {
            Add-Check -Id $target.Id -Category 'configuration' -Status 'pass' `
                -Summary "All $($target.Keys.Count) required $($target.Label) configuration keys are present." `
                -EvidenceReference "az containerapp show --name $($target.App) --resource-group $ResourceGroup"
        }
        else {
            Add-Check -Id $target.Id -Category 'configuration' -Status 'fail' `
                -Summary "Missing $($target.Label) configuration keys: $($missing -join ', ')." `
                -CorrectiveAction "Add the missing keys to infra/$($target.Label).bicep and re-provision. Configuration is owned by Bicep, not by portal edits."
        }
    }
}

# ---------------------------------------------------------------------------
# Category: identity
# ---------------------------------------------------------------------------

function Test-Identity {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'identity-user-assigned' -Category 'identity' -Reason 'First release or offline: managed identities are created by this deployment.'
        Add-SkippedCheck -Id 'identity-role-assignments' -Category 'identity' -Reason 'First release or offline: role assignments are created by this deployment.'
        return
    }

    $expected = @('acrpull', 'web', 'api') | ForEach-Object { "id-$EnvironmentName-$_" }
    $actual = Invoke-AzCommand -Argument @('identity', 'list', '--resource-group', $ResourceGroup, '--query', '[].name', '-o', 'json')
    $actualNames = @($actual)

    $missing = @($expected | Where-Object { $actualNames -notcontains $_ })
    if ($missing.Count -eq 0) {
        Add-Check -Id 'identity-user-assigned' -Category 'identity' -Status 'pass' `
            -Summary 'All three user-assigned managed identities exist.'
    }
    else {
        Add-Check -Id 'identity-user-assigned' -Category 'identity' -Status 'fail' `
            -Summary "Missing user-assigned identities: $($missing -join ', ')." `
            -CorrectiveAction 'Re-run provisioning so infra/identity.bicep creates the missing identities.'
    }

    # `--all` is subscription-wide and the CLI rejects it alongside `--resource-group`.
    # Scoping with `--resource-group` alone is no good either: it matches only assignments
    # made at the group scope, and every assignment here sits on a child resource.
    $assignments = Invoke-AzCommand -Argument @(
        'role', 'assignment', 'list', '--all',
        '--query', '[].{role:roleDefinitionName,scope:scope}', '-o', 'json')

    $groupScope = "/resourceGroups/$ResourceGroup/"
    $roleNames = @($assignments |
        Where-Object { "$($_.scope)/" -like "*$groupScope*" } |
        ForEach-Object { $_.role })

    $requiredRoles = @('AcrPull', 'Key Vault Secrets User', 'Storage Blob Data Contributor')
    $missingRoles = @($requiredRoles | Where-Object { $roleNames -notcontains $_ })

    if ($null -eq $assignments) {
        Add-Check -Id 'identity-role-assignments' -Category 'identity' -Status 'fail' `
            -Summary 'Could not read role assignments; the lookup itself failed.' `
            -CorrectiveAction 'Confirm the deploying principal can read role assignments (Reader on the subscription is enough) and re-run. This is a failed query, not a missing assignment.'
    }
    elseif ($missingRoles.Count -eq 0) {
        Add-Check -Id 'identity-role-assignments' -Category 'identity' -Status 'pass' `
            -Summary 'Least-privilege role assignments for registry, Key Vault, and storage are in place.' `
            -EvidenceReference 'az role assignment list --all'
    }
    else {
        Add-Check -Id 'identity-role-assignments' -Category 'identity' -Status 'fail' `
            -Summary "Missing role assignments: $($missingRoles -join ', ')." `
            -CorrectiveAction 'Re-run provisioning so infra/rbac.bicep applies the missing assignments. Role propagation can lag by a few minutes.'
    }
}

# ---------------------------------------------------------------------------
# Category: secret-reference
# ---------------------------------------------------------------------------

function Test-SecretReferences {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'secret-references-present' -Category 'secret-reference' -Reason 'First release or offline: Key Vault secrets are seeded by this deployment.'
        return
    }

    $vaultName = Invoke-AzCommand -Argument @(
        'keyvault', 'list', '--resource-group', $ResourceGroup, '--query', '[0].name', '-o', 'tsv')

    if ([string]::IsNullOrWhiteSpace("$vaultName")) {
        Add-Check -Id 'secret-references-present' -Category 'secret-reference' -Status 'fail' `
            -Summary "No Key Vault found in resource group '$ResourceGroup'." `
            -CorrectiveAction 'Re-run provisioning so infra/key-vault.bicep creates the vault before deploying the applications.'
        return
    }

    # Names only. Secret values are never read by the readiness gate.
    $secretNames = @(Invoke-AzCommand -Argument @('keyvault', 'secret', 'list', '--vault-name', "$vaultName", '--query', '[].name', '-o', 'json'))
    $requiredSecrets = @($contract.SecretReferences | Where-Object { $_.Required } | ForEach-Object { $_.LogicalName })
    $missing = @($requiredSecrets | Where-Object { $secretNames -notcontains $_ })

    if ($missing.Count -eq 0) {
        Add-Check -Id 'secret-references-present' -Category 'secret-reference' -Status 'pass' `
            -Summary "All required secrets exist in '$vaultName'." `
            -EvidenceReference "az keyvault secret list --vault-name $vaultName --query '[].name'"
    }
    else {
        Add-Check -Id 'secret-references-present' -Category 'secret-reference' -Status 'fail' `
            -Summary "Missing Key Vault secrets: $($missing -join ', ')." `
            -CorrectiveAction "Supply the corresponding azd environment values and re-provision. Container Apps secret references fail to resolve when the secret does not exist."
    }
}

# ---------------------------------------------------------------------------
# Category: entra
# ---------------------------------------------------------------------------

function Test-Entra {
    $webClientId = $env:AZURE_ENTRA_WEB_CLIENT_ID
    $apiClientId = $env:AZURE_ENTRA_API_CLIENT_ID

    if ([string]::IsNullOrWhiteSpace($webClientId) -or [string]::IsNullOrWhiteSpace($apiClientId)) {
        Add-Check -Id 'entra-app-registrations' -Category 'entra' -Status 'fail' `
            -Summary 'Entra web and/or API client id is not configured.' `
            -CorrectiveAction "Register both applications and set them with 'azd env set AZURE_ENTRA_WEB_CLIENT_ID <id>' and 'azd env set AZURE_ENTRA_API_CLIENT_ID <id>'. See docs/operations/production-runbook.md."
        return
    }

    if (-not $azureAvailable) {
        Add-SkippedCheck -Id 'entra-app-registrations' -Category 'entra' -Reason 'Offline mode: app registrations not queried.'
        return
    }

    $missingApps = [System.Collections.Generic.List[string]]::new()
    foreach ($clientId in @($webClientId, $apiClientId)) {
        $app = Invoke-AzCommand -Argument @('ad', 'app', 'show', '--id', $clientId, '--query', 'appId', '-o', 'tsv')
        if ([string]::IsNullOrWhiteSpace("$app")) { $missingApps.Add($clientId) }
    }

    if ($missingApps.Count -eq 0) {
        Add-Check -Id 'entra-app-registrations' -Category 'entra' -Status 'pass' `
            -Summary 'Both Entra application registrations resolve in the deployment tenant.'
    }
    else {
        Add-Check -Id 'entra-app-registrations' -Category 'entra' -Status 'fail' `
            -Summary "Entra application registration(s) not found: $($missingApps -join ', ')." `
            -CorrectiveAction 'Confirm the client ids belong to the deployment tenant and that the deploying identity can read directory objects.'
    }
}

# ---------------------------------------------------------------------------
# Category: data-protection
# ---------------------------------------------------------------------------

function Test-DataProtection {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'data-protection-key-ring' -Category 'data-protection' -Reason 'First release or offline: the key-ring container is created by this deployment.'
        return
    }

    $account = Invoke-AzCommand -Argument @('storage', 'account', 'list', '--resource-group', $ResourceGroup, '--query', '[0].name', '-o', 'tsv')
    if ([string]::IsNullOrWhiteSpace("$account")) {
        Add-Check -Id 'data-protection-key-ring' -Category 'data-protection' -Status 'fail' `
            -Summary "No storage account found in '$ResourceGroup' to hold the data-protection key ring." `
            -CorrectiveAction 'Re-run provisioning so infra/storage.bicep creates the storage account and containers.'
        return
    }

    $containers = @(Invoke-AzCommand -Argument @(
            'storage', 'container', 'list', '--account-name', "$account", '--auth-mode', 'login',
            '--query', '[].name', '-o', 'json'))

    if ($containers -contains $contract.DataProtectionContainer) {
        Add-Check -Id 'data-protection-key-ring' -Category 'data-protection' -Status 'pass' `
            -Summary "Data-protection container '$($contract.DataProtectionContainer)' exists, so sign-in cookies survive revision replacement." `
            -EvidenceReference "az storage container list --account-name $account"
    }
    else {
        Add-Check -Id 'data-protection-key-ring' -Category 'data-protection' -Status 'fail' `
            -Summary "Data-protection container '$($contract.DataProtectionContainer)' is missing." `
            -CorrectiveAction 'Re-run provisioning. Without a shared key ring, every scale-to-zero cycle invalidates existing sign-in cookies.'
    }
}

# ---------------------------------------------------------------------------
# Category: artifact
# ---------------------------------------------------------------------------

function Test-Artifacts {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'artifact-images-tagged' -Category 'artifact' -Reason 'First release or offline: images are built and pushed by this release.'
        return
    }

    $registry = Invoke-AzCommand -Argument @('acr', 'list', '--resource-group', $ResourceGroup, '--query', '[0].name', '-o', 'tsv')
    if ([string]::IsNullOrWhiteSpace("$registry")) {
        Add-Check -Id 'artifact-images-tagged' -Category 'artifact' -Status 'fail' `
            -Summary "No container registry found in '$ResourceGroup'." `
            -CorrectiveAction 'Re-run provisioning so infra/registry.bicep creates the registry before images are pushed.'
        return
    }

    $missingTags = [System.Collections.Generic.List[string]]::new()
    foreach ($repository in @('web', 'api')) {
        $tag = Invoke-AzCommand -Argument @(
            'acr', 'repository', 'show-tags', '--name', "$registry", '--repository', $repository,
            '--query', "[?@=='$ReleaseId'] | [0]", '-o', 'tsv')
        if ([string]::IsNullOrWhiteSpace("$tag")) { $missingTags.Add("${repository}:$ReleaseId") }
    }

    if ($missingTags.Count -eq 0) {
        Add-Check -Id 'artifact-images-tagged' -Category 'artifact' -Status 'pass' `
            -Summary "Both images are present in '$registry' tagged with the immutable release id."
    }
    else {
        Add-Check -Id 'artifact-images-tagged' -Category 'artifact' -Status 'fail' `
            -Summary "Images not yet published for this release: $($missingTags -join ', ')." `
            -CorrectiveAction 'Run the build-and-push stage before the readiness gate, so the release deploys the exact commit that was tested.'
    }
}

# ---------------------------------------------------------------------------
# Category: infrastructure-preview
# ---------------------------------------------------------------------------

function Test-InfrastructurePreview {
    if ($SkipInfrastructurePreview -or -not $azureAvailable) {
        Add-SkippedCheck -Id 'infrastructure-preview' -Category 'infrastructure-preview' -Reason 'Infrastructure preview was skipped for this run.'
        return
    }

    $azd = Get-Command azd -ErrorAction SilentlyContinue
    if ($null -eq $azd) {
        Add-Check -Id 'infrastructure-preview' -Category 'infrastructure-preview' -Status 'fail' `
            -Summary 'The Azure Developer CLI (azd) is not installed on this machine.' `
            -CorrectiveAction 'Install azd (https://aka.ms/azd-install) so the deployment preview can run before any change is applied.'
        return
    }

    Push-Location $repoRoot
    try {
        $output = & azd provision --preview --no-prompt 2>&1 | Out-String
        $exit = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $sanitized = Protect-DeploymentSecret -InputText $output

    if ($exit -ne 0) {
        Add-Check -Id 'infrastructure-preview' -Category 'infrastructure-preview' -Status 'fail' `
            -Summary 'azd provision --preview did not complete successfully.' `
            -CorrectiveAction 'Resolve the template or parameter errors reported by the preview, then re-run readiness.' `
            -EvidenceReference 'azd provision --preview'
        Write-Verbose $sanitized
        return
    }

    # A Delete operation against an existing environment means data loss risk.
    $destructive = @([regex]::Matches($sanitized, '(?im)^\s*Delete\s*:\s*(.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() })

    if ($destructive.Count -gt 0 -and $deployedResourcesExpected) {
        Add-Check -Id 'infrastructure-preview' -Category 'infrastructure-preview' -Status 'fail' `
            -Summary "The preview reports $($destructive.Count) resource deletion(s): $($destructive -join '; ')." `
            -CorrectiveAction 'Review the template change. Deleting a stateful resource destroys trip data; note the current restore point, confirm you can reach it, and accept the risk explicitly before proceeding.' `
            -EvidenceReference 'azd provision --preview'
    }
    else {
        Add-Check -Id 'infrastructure-preview' -Category 'infrastructure-preview' -Status 'pass' `
            -Summary 'Deployment preview succeeded with no unexpected resource deletions.' `
            -EvidenceReference 'azd provision --preview'
    }
}

# ---------------------------------------------------------------------------
# Category: database-recovery
# ---------------------------------------------------------------------------

function Test-DatabaseRecovery {
    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'database-recovery-point' -Category 'database-recovery' -Reason 'First release or offline: no database server exists to restore yet.'
        return
    }

    # Flexible Server keeps a continuous restore window instead of discrete dump files.
    # The question is no longer "did last night's job run" but "how far back can we go, and
    # is the window actually open" - a server that was just created reports a restore point
    # in the future until the first base backup completes.
    $server = Invoke-AzCommand -Argument @(
        'postgres', 'flexible-server', 'list', '--resource-group', $ResourceGroup,
        '--query', '[0].{name:name,earliest:backup.earliestRestoreDate,retention:backup.backupRetentionDays}', '-o', 'json')

    if ($null -eq $server -or [string]::IsNullOrWhiteSpace("$($server.name)")) {
        Add-Check -Id 'database-recovery-point' -Category 'database-recovery' -Status 'fail' `
            -Summary 'No PostgreSQL flexible server found in the resource group.' `
            -CorrectiveAction 'Re-run provisioning so the database server exists before deploying a change that touches the database.'
        return
    }

    if ([string]::IsNullOrWhiteSpace("$($server.earliest)")) {
        Add-Check -Id 'database-recovery-point' -Category 'database-recovery' -Status 'fail' `
            -Summary "Server '$($server.name)' reports no earliest restore date, so point-in-time restore is not yet available." `
            -CorrectiveAction 'Wait for the first base backup to finish, then re-run readiness. If it never appears, confirm backups are enabled on the server.'
        return
    }

    $earliest = ([datetime]$server.earliest).ToUniversalTime()
    $windowHours = ([datetime]::UtcNow - $earliest).TotalHours
    $evidence = "az postgres flexible-server show --name $($server.name)"

    if ($windowHours -le 0) {
        Add-Check -Id 'database-recovery-point' -Category 'database-recovery' -Status 'fail' `
            -Summary "Server '$($server.name)' has no usable restore window yet; its earliest restore date is $($earliest.ToString('o'))." `
            -CorrectiveAction 'The first base backup has not completed. Wait for it before deploying a change that touches the database.' `
            -EvidenceReference $evidence
        return
    }

    if ($windowHours -ge $contract.RestorePointMaxAgeHours) {
        Add-Check -Id 'database-recovery-point' -Category 'database-recovery' -Status 'pass' `
            -Summary ("Point-in-time restore covers the last {0:N1} hours ({1}-day retention configured)." -f $windowHours, $server.retention) `
            -EvidenceReference $evidence
    }
    else {
        Add-Check -Id 'database-recovery-point' -Category 'database-recovery' -Status 'fail' `
            -Summary ("Restore window is only {0:N1} hours, short of the {1}-hour objective." -f $windowHours, $contract.RestorePointMaxAgeHours) `
            -CorrectiveAction 'The server is too new or was recently restored. Wait until the window covers the objective before deploying a change that touches the database.' `
            -EvidenceReference $evidence
    }
}

# ---------------------------------------------------------------------------
# Category: security
# ---------------------------------------------------------------------------

function Test-Security {
    # Static check: the repository must not carry committed secret values.
    $parametersPath = Join-Path $repoRoot 'infra/main.parameters.json'
    $parameters = Get-Content -LiteralPath $parametersPath -Raw
    $literalSecret = [regex]::IsMatch($parameters, '(?i)"value"\s*:\s*"(?!\$\{)[^"]{12,}"')

    if ($literalSecret) {
        Add-Check -Id 'security-no-literal-secrets' -Category 'security' -Status 'fail' `
            -Summary 'infra/main.parameters.json contains a literal value where an environment reference is expected.' `
            -CorrectiveAction 'Replace the literal with a ${AZURE_*} environment reference so secrets stay in the azd environment and Key Vault.'
    }
    else {
        Add-Check -Id 'security-no-literal-secrets' -Category 'security' -Status 'pass' `
            -Summary 'Deployment parameters carry only environment references, not literal secret values.' `
            -EvidenceReference 'infra/main.parameters.json'
    }

    if (-not $deployedResourcesExpected) {
        Add-SkippedCheck -Id 'security-public-network-exposure' -Category 'security' -Reason 'First release or offline: deployed ingress not queried.'
        return
    }

    $externalApps = @(Invoke-AzCommand -Argument @(
            'containerapp', 'list', '--resource-group', $ResourceGroup,
            '--query', '[?properties.configuration.ingress.external==`true`].name', '-o', 'json'))

    $unexpected = @($externalApps | Where-Object { -not [string]::IsNullOrWhiteSpace("$_") -and $_ -ne "ca-web-$EnvironmentName" })

    if ($unexpected.Count -eq 0) {
        Add-Check -Id 'security-public-network-exposure' -Category 'security' -Status 'pass' `
            -Summary 'Only the web front end accepts public traffic; the API and database stay on internal ingress.' `
            -EvidenceReference "az containerapp list --resource-group $ResourceGroup"
    }
    else {
        Add-Check -Id 'security-public-network-exposure' -Category 'security' -Status 'fail' `
            -Summary "Unexpected internet-facing container app(s): $($unexpected -join ', ')." `
            -CorrectiveAction "Set 'external: false' on those apps in infra/ and re-provision. Only the web front end should be publicly reachable."
    }
}

# ---------------------------------------------------------------------------
# Accepted risks
# ---------------------------------------------------------------------------

function Get-AcceptedRisk {
    if (-not (Test-Path -LiteralPath $AcceptedRiskPath)) {
        return @()
    }

    $document = Get-Content -LiteralPath $AcceptedRiskPath -Raw | ConvertFrom-Json
    $entries = @($document.acceptedRisks)
    if ($entries.Count -eq 0) { return @() }

    $now = [datetime]::UtcNow
    $valid = [System.Collections.Generic.List[object]]::new()

    foreach ($risk in $entries) {
        $required = @('riskId', 'checkId', 'owner', 'rationale', 'acceptedAtUtc', 'reviewAtUtc')
        $missing = @($required | Where-Object {
                -not $risk.PSObject.Properties.Name.Contains($_) -or [string]::IsNullOrWhiteSpace("$($risk.$_)")
            })

        if ($missing.Count -gt 0) {
            Write-Warning "Ignoring accepted risk '$($risk.riskId)': missing $($missing -join ', '). An incomplete waiver does not suppress a failure."
            continue
        }

        $expires = if ($risk.PSObject.Properties.Name.Contains('expiresAtUtc') -and
            -not [string]::IsNullOrWhiteSpace("$($risk.expiresAtUtc)")) { [datetime]$risk.expiresAtUtc } else { $null }

        if ($null -ne $expires -and $expires.ToUniversalTime() -lt $now) {
            Write-Warning "Accepted risk '$($risk.riskId)' expired on $($risk.expiresAtUtc) and no longer waives '$($risk.checkId)'."
            continue
        }

        $valid.Add([pscustomobject]@{
                riskId        = $risk.riskId
                checkId       = $risk.checkId
                owner         = $risk.owner
                rationale     = $risk.rationale
                acceptedAtUtc = Get-DeploymentUtcNow -Value ([datetime]$risk.acceptedAtUtc)
                reviewAtUtc   = Get-DeploymentUtcNow -Value ([datetime]$risk.reviewAtUtc)
                expiresAtUtc  = if ($null -eq $expires) { $null } else { Get-DeploymentUtcNow -Value $expires }
            })
    }

    return $valid.ToArray()
}

# ---------------------------------------------------------------------------
# Execution
# ---------------------------------------------------------------------------

Test-AzureContext
Test-ResourceProviders
Test-QuotaAndPolicy
Test-Configuration
Test-Identity
Test-SecretReferences
Test-Entra
Test-DataProtection
Test-Artifacts
Test-InfrastructurePreview
Test-DatabaseRecovery
Test-Security

$acceptedRisks = @(Get-AcceptedRisk)
$overallStatus = Get-DeploymentOverallStatus -Check $checks.ToArray() -AcceptedRisk $acceptedRisks

$report = [pscustomobject]@{
    schemaVersion   = $contract.SchemaVersion
    releaseId       = $ReleaseId
    environmentName = $EnvironmentName
    generatedAtUtc  = Get-DeploymentUtcNow
    overallStatus   = $overallStatus
    checks          = $checks.ToArray()
    acceptedRisks   = $acceptedRisks
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Get-DeploymentEvidencePath -Kind 'readiness' -ReleaseId $ReleaseId -Root (Join-Path $repoRoot 'artifacts/deployment-evidence')
}

$written = Write-DeploymentEvidence -Evidence $report -Path $OutputPath -SchemaPath $schemaPath

$failed = @($checks | Where-Object { $_.status -eq 'fail' })
$waived = @($acceptedRisks | ForEach-Object { $_.checkId })

Write-Host "Readiness report: $written"
Write-Host "Release $ReleaseId / environment $EnvironmentName -> $($overallStatus.ToUpperInvariant())"

foreach ($check in $failed) {
    $prefix = if ($waived -contains $check.id) { 'WAIVED' } elseif ($check.required) { 'BLOCKING' } else { 'ADVISORY' }
    Write-Host "  [$prefix] $($check.id): $($check.summary)"
    Write-Host "            -> $($check.correctiveAction)"
}

exit (Get-DeploymentExitCode -OverallStatus $overallStatus)
