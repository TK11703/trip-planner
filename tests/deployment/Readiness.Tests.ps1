<#
.SYNOPSIS
    Contract tests for the deployment readiness gate.

.DESCRIPTION
    These tests run entirely offline. They assert the behaviours a release depends on:
    the report matches its schema, every failure is actionable, secrets never reach the
    artifact, and a blocking failure produces a nonzero exit code.

    Run with: Invoke-Pester -Path tests/deployment
#>

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $script:ScriptPath = Join-Path $RepoRoot 'scripts/deployment-readiness.ps1'
    $script:ModulePath = Join-Path $RepoRoot 'scripts/TripPlanner.Deployment.psm1'
    $script:SchemaPath = Join-Path $RepoRoot 'specs/026-azure-deployment-readiness/contracts/readiness-report.schema.json'
    $script:ReleaseId = '0123456789abcdef0123456789abcdef01234567'

    Import-Module $ModulePath -Force

    # The script calls `exit`, so it runs in a child process to keep the test host alive.
    function Invoke-Readiness {
        param(
            [hashtable] $Environment = @{},
            [string[]] $ExtraArgument = @()
        )

        $outputPath = Join-Path ([System.IO.Path]::GetTempPath()) "readiness-$([guid]::NewGuid()).json"
        $saved = @{}

        foreach ($key in $Environment.Keys) {
            $saved[$key] = [Environment]::GetEnvironmentVariable($key)
            [Environment]::SetEnvironmentVariable($key, $Environment[$key])
        }

        try {
            $arguments = @(
                '-NoProfile', '-NonInteractive', '-File', $script:ScriptPath,
                '-ReleaseId', $script:ReleaseId,
                '-Offline',
                '-FirstRelease',
                '-OutputPath', $outputPath
            ) + $ExtraArgument

            $stdout = & pwsh @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            [pscustomobject]@{
                ExitCode = $exitCode
                Output   = $stdout
                Path     = $outputPath
                Json     = if (Test-Path -LiteralPath $outputPath) { Get-Content -LiteralPath $outputPath -Raw } else { $null }
                Report   = if (Test-Path -LiteralPath $outputPath) { Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json } else { $null }
            }
        }
        finally {
            foreach ($key in $saved.Keys) {
                [Environment]::SetEnvironmentVariable($key, $saved[$key])
            }
            if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
        }
    }

    $script:CompleteEnvironment = @{
        AZURE_ENV_NAME             = 'tripplanner-test'
        AZURE_LOCATION             = 'eastus2'
        AZURE_SUBSCRIPTION_ID      = '00000000-0000-0000-0000-000000000001'
        AZURE_RESOURCE_GROUP       = 'rg-tripplanner-test'
        AZURE_TENANT_ID            = '00000000-0000-0000-0000-000000000002'
        AZURE_ENTRA_WEB_CLIENT_ID  = '00000000-0000-0000-0000-000000000003'
        AZURE_ENTRA_API_CLIENT_ID  = '00000000-0000-0000-0000-000000000004'
    }
}

Describe 'Readiness report contract' {
    BeforeAll {
        $script:Result = Invoke-Readiness -Environment $script:CompleteEnvironment
    }

    It 'writes a report file' {
        $Result.Report | Should -Not -BeNullOrEmpty
    }

    It 'conforms to the readiness report schema' {
        Test-Json -Json $Result.Json -SchemaFile $script:SchemaPath | Should -BeTrue
    }

    It 'records the supplied release id' {
        $Result.Report.releaseId | Should -Be $script:ReleaseId
    }

    It 'uses only the categories defined in the contract' {
        $allowed = (Get-TripPlannerDeploymentContract).ReadinessCategories
        foreach ($check in $Result.Report.checks) {
            $allowed | Should -Contain $check.category
        }
    }

    It 'assigns every check a unique id' {
        $ids = @($Result.Report.checks | ForEach-Object { $_.id })
        ($ids | Sort-Object -Unique).Count | Should -Be $ids.Count
    }

    It 'gives every failing check a corrective action' {
        foreach ($check in @($Result.Report.checks | Where-Object { $_.status -eq 'fail' })) {
            $check.correctiveAction | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'Blocking behaviour' {
    It 'passes when every required prerequisite is satisfied' {
        $result = Invoke-Readiness -Environment $script:CompleteEnvironment
        $result.Report.overallStatus | Should -Be 'pass'
        $result.ExitCode | Should -Be 0
    }

    It 'fails with a nonzero exit code when a required setting is missing' {
        $incomplete = $script:CompleteEnvironment.Clone()
        $incomplete['AZURE_ENTRA_WEB_CLIENT_ID'] = ''

        $result = Invoke-Readiness -Environment $incomplete

        $result.Report.overallStatus | Should -Be 'fail'
        $result.ExitCode | Should -Be 1
    }

    It 'names the specific missing prerequisite and its corrective action' {
        $incomplete = $script:CompleteEnvironment.Clone()
        $incomplete['AZURE_ENTRA_WEB_CLIENT_ID'] = ''

        $result = Invoke-Readiness -Environment $incomplete
        $failed = @($result.Report.checks | Where-Object { $_.status -eq 'fail' })

        $failed.Count | Should -BeGreaterThan 0
        ($failed | Where-Object { $_.summary -match 'AZURE_ENTRA_WEB_CLIENT_ID' -or $_.summary -match 'client id' }).Count |
            Should -BeGreaterThan 0
        foreach ($check in $failed) {
            $check.correctiveAction | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'Secret handling' {
    It 'never writes a secret value into the report' {
        $result = Invoke-Readiness -Environment $script:CompleteEnvironment

        $result.Json | Should -Not -Match '(?i)password\s*=\s*[^;"\s*]+'
        $result.Json | Should -Not -Match 'eyJ[A-Za-z0-9._-]{20,}'
        $result.Json | Should -Not -Match '(?i)accountkey\s*='
    }

    It 'redacts connection string credentials' {
        $text = 'Host=db;Username=postgres;Password=Sup3rS3cret!;SSL Mode=Disable'
        Protect-DeploymentSecret -InputText $text | Should -Not -Match 'Sup3rS3cret'
    }

    It 'redacts bearer tokens and JWTs' {
        $token = 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payloadpayloadpayload.signature'
        $redacted = Protect-DeploymentSecret -InputText $token
        $redacted | Should -Not -Match 'payloadpayload'
    }

    It 'masks explicitly supplied known secret values anywhere they appear' {
        $redacted = Protect-DeploymentSecret -InputText 'value is hunter2hunter2' -KnownSecret @('hunter2hunter2')
        $redacted | Should -Not -Match 'hunter2'
    }

    It 'redacts nested string values in an object graph' {
        $graph = [pscustomobject]@{
            outer = [pscustomobject]@{ connection = 'Password=TopSecretValue;Host=db' }
        }

        $sanitized = Protect-DeploymentObject -InputObject $graph
        ($sanitized | ConvertTo-Json -Depth 5) | Should -Not -Match 'TopSecretValue'
    }
}

Describe 'Check construction rules' {
    It 'refuses to create a failing check without a corrective action' {
        { New-DeploymentCheck -Id 'x' -Category 'security' -Status 'fail' -Summary 'broken' } |
            Should -Throw -ExpectedMessage '*CorrectiveAction*'
    }

    It 'allows a passing check with no corrective action' {
        $check = New-DeploymentCheck -Id 'x' -Category 'security' -Status 'pass' -Summary 'fine'
        $check.correctiveAction | Should -BeNullOrEmpty
    }
}

Describe 'Overall status resolution' {
    BeforeAll {
        $script:FailingCheck = New-DeploymentCheck -Id 'database-recovery-point' -Category 'database-recovery' `
            -Status 'fail' -Summary 'stale' -CorrectiveAction 'run the backup job'
        $script:PassingCheck = New-DeploymentCheck -Id 'security-public-network-exposure' -Category 'security' `
            -Status 'pass' -Summary 'fine'
    }

    It 'fails when a required check fails' {
        Get-DeploymentOverallStatus -Check @($script:PassingCheck, $script:FailingCheck) | Should -Be 'fail'
    }

    It 'passes when an accepted risk waives the failing check' {
        $risk = [pscustomobject]@{ checkId = 'database-recovery-point' }
        Get-DeploymentOverallStatus -Check @($script:FailingCheck) -AcceptedRisk @($risk) | Should -Be 'pass'
    }

    It 'still fails when the waiver targets a different check' {
        $risk = [pscustomobject]@{ checkId = 'something-else' }
        Get-DeploymentOverallStatus -Check @($script:FailingCheck) -AcceptedRisk @($risk) | Should -Be 'fail'
    }

    It 'does not fail the release for an optional check' {
        $optional = New-DeploymentCheck -Id 'policy-blocking-assignments' -Category 'quota-policy' `
            -Status 'fail' -Summary 'advisory' -CorrectiveAction 'review policy' -Required $false
        Get-DeploymentOverallStatus -Check @($optional) | Should -Be 'pass'
    }

    It 'maps pass to exit code 0 and fail to exit code 1' {
        Get-DeploymentExitCode -OverallStatus 'pass' | Should -Be 0
        Get-DeploymentExitCode -OverallStatus 'fail' | Should -Be 1
    }
}

Describe 'Accepted risk register' {
    It 'ships a register that matches the contract shape' {
        $registerPath = Join-Path $script:RepoRoot '.azure/accepted-risks.json'
        Test-Path -LiteralPath $registerPath | Should -BeTrue

        $register = Get-Content -LiteralPath $registerPath -Raw | ConvertFrom-Json
        $register.PSObject.Properties.Name | Should -Contain 'acceptedRisks'
    }
}
