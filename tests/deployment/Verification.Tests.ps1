<#
.SYNOPSIS
    Contract tests for the post-deployment verification gate.

.DESCRIPTION
    These tests run entirely offline. They assert that the verification report matches its
    schema, that every mandatory category is represented, that a failing run always carries
    a failure category and a recovery action, and that an unverified release exits nonzero.

    Run with: Invoke-Pester -Path tests/deployment
#>

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $script:ScriptPath = Join-Path $RepoRoot 'scripts/deployment-verify.ps1'
    $script:ModulePath = Join-Path $RepoRoot 'scripts/TripPlanner.Deployment.psm1'
    $script:SchemaPath = Join-Path $RepoRoot 'specs/026-azure-deployment-readiness/contracts/deployment-verification.schema.json'
    $script:ReleaseId = '0123456789abcdef0123456789abcdef01234567'

    Import-Module $ModulePath -Force

    # The script calls `exit`, so it runs in a child process to keep the test host alive.
    function Invoke-Verification {
        param(
            [string[]] $ExtraArgument = @()
        )

        $outputPath = Join-Path ([System.IO.Path]::GetTempPath()) "verification-$([guid]::NewGuid()).json"

        try {
            $arguments = @(
                '-NoProfile', '-NonInteractive', '-File', $script:ScriptPath,
                '-ReleaseId', $script:ReleaseId,
                '-EnvironmentName', 'tripplanner-test',
                '-WebUrl', 'https://web.example.invalid',
                '-Offline',
                '-OutputPath', $outputPath
            ) + $ExtraArgument

            $stdout = & pwsh @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            [pscustomobject]@{
                ExitCode = $exitCode
                Output   = $stdout
                Json     = if (Test-Path -LiteralPath $outputPath) { Get-Content -LiteralPath $outputPath -Raw } else { $null }
                Report   = if (Test-Path -LiteralPath $outputPath) { Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json } else { $null }
            }
        }
        finally {
            if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
        }
    }

    function New-PassingCheckSet {
        $categories = (Get-TripPlannerDeploymentContract).VerificationCategories
        @($categories | ForEach-Object {
                New-VerificationCheck -Id $_ -Category $_ -Status 'pass' -Summary 'ok' -DurationMilliseconds 1
            })
    }
}

Describe 'Verification report contract' {
    BeforeAll {
        $script:Result = Invoke-Verification
    }

    It 'writes a report file' {
        $Result.Report | Should -Not -BeNullOrEmpty
    }

    It 'conforms to the verification schema' {
        Test-Json -Json $Result.Json -SchemaFile $script:SchemaPath | Should -BeTrue
    }

    It 'records the supplied release id and environment' {
        $Result.Report.releaseId | Should -Be $script:ReleaseId
        $Result.Report.environmentName | Should -Be 'tripplanner-test'
    }

    It 'covers every mandatory verification category' {
        $expected = (Get-TripPlannerDeploymentContract).VerificationCategories
        $actual = @($Result.Report.checks | ForEach-Object { $_.category } | Sort-Object -Unique)
        foreach ($category in $expected) {
            $actual | Should -Contain $category
        }
    }

    It 'uses only the categories defined in the contract' {
        $allowed = (Get-TripPlannerDeploymentContract).VerificationCategories
        foreach ($check in $Result.Report.checks) {
            $allowed | Should -Contain $check.category
        }
    }

    It 'assigns every check a unique id' {
        $ids = @($Result.Report.checks | ForEach-Object { $_.id })
        ($ids | Sort-Object -Unique).Count | Should -Be $ids.Count
    }

    It 'records a non-negative duration for every check' {
        foreach ($check in $Result.Report.checks) {
            $check.durationMilliseconds | Should -BeGreaterOrEqual 0
        }
    }
}

Describe 'Blocking behaviour' {
    BeforeAll {
        $script:Result = Invoke-Verification
    }

    It 'does not report a pass when the environment could not be reached' {
        $Result.Report.overallStatus | Should -Be 'fail'
    }

    It 'exits nonzero so the release cannot complete unverified' {
        $Result.ExitCode | Should -Not -Be 0
    }

    It 'populates failureCategory and recoveryAction on failure' {
        $Result.Report.failureCategory | Should -Not -BeNullOrEmpty
        $Result.Report.recoveryAction | Should -Not -BeNullOrEmpty
    }

    It 'classifies an unreachable run as incomplete rather than as an app defect' {
        $Result.Report.failureCategory | Should -Be 'verification-incomplete'
    }
}

Describe 'Failure classification' {
    It 'returns no classification when every check passed' {
        Get-VerificationFailureClassification -Check (New-PassingCheckSet) | Should -BeNullOrEmpty
    }

    It 'reports an all-pass run as pass' {
        Get-VerificationOverallStatus -Check (New-PassingCheckSet) | Should -Be 'pass'
    }

    It 'treats a not-run check as a failed run' {
        $checks = @(
            New-VerificationCheck -Id 'a' -Category 'liveness' -Status 'pass' -Summary 'ok'
            New-VerificationCheck -Id 'b' -Category 'sign-in' -Status 'not-run' -Summary 'skipped'
        )
        Get-VerificationOverallStatus -Check $checks | Should -Be 'fail'
    }

    It 'classifies by the first failing check, not the last' {
        $checks = @(
            New-VerificationCheck -Id 'a' -Category 'sign-in' -Status 'fail' -Summary 'no'
            New-VerificationCheck -Id 'b' -Category 'authenticated-api' -Status 'fail' -Summary 'no'
        )
        (Get-VerificationFailureClassification -Check $checks).failureCategory | Should -Be 'authentication'
    }

    It 'supplies a recovery action for every verification category' {
        foreach ($category in (Get-TripPlannerDeploymentContract).VerificationCategories) {
            $check = New-VerificationCheck -Id 'x' -Category $category -Status 'fail' -Summary 'no'
            $classification = Get-VerificationFailureClassification -Check @($check)
            $classification.failureCategory | Should -Not -Be 'unclassified'
            $classification.recoveryAction | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'Secret hygiene' {
    It 'redacts credential-shaped text from check summaries' {
        $check = New-VerificationCheck -Id 'x' -Category 'readiness' -Status 'fail' `
            -Summary 'Host=postgres;Password=hunter2;Database=tripplanner'
        $check.summary | Should -Not -Match 'hunter2'
        $check.summary | Should -Match 'REDACTED'
    }

    It 'writes no bearer token into the report' {
        $result = Invoke-Verification
        $result.Json | Should -Not -Match 'eyJ[A-Za-z0-9._-]{20,}'
    }
}
