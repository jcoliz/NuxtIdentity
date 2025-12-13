<#
.SYNOPSIS
Runs all tests and collects code coverage metrics.

.DESCRIPTION
This script executes all test projects with code coverage collection and generates a consolidated
HTML report. It uses the XPlat Code Coverage collector and ReportGenerator to create a detailed
coverage report that opens automatically in your browser.

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs all tests and generates a consolidated code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The coverage report will be generated in .\TestResults\CoverageReport\index.html and opened automatically.

.LINK
https://github.com/danielpalme/ReportGenerator
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $RootPath = "$PSScriptRoot/.."
    $TestProjects = @(
        "tests/NuxtIdentity.Core.Tests/NuxtIdentity.Core.Tests.csproj",
        "tests/NuxtIdentity.EntityFrameworkCore.Tests/NuxtIdentity.EntityFrameworkCore.Tests.csproj"
    )

    Push-Location $RootPath

    Write-Host "Cleaning up previous test results..." -ForegroundColor Cyan
    Remove-Item TestResults -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "`nRunning all tests with code coverage..." -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    foreach ($project in $TestProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        Write-Host "Testing: $projectName" -ForegroundColor Yellow

        dotnet test $project `
            --collect:"XPlat Code Coverage" `
            --results-directory ./TestResults `
            --verbosity normal

        if ($LASTEXITCODE -ne 0) {
            throw "Test execution failed for $projectName with exit code $LASTEXITCODE"
        }
        Write-Host ""
    }

    Write-Host "Generating consolidated coverage report..." -ForegroundColor Cyan
    reportgenerator `
        -reports:./TestResults/**/coverage.cobertura.xml `
        -targetdir:./TestResults/CoverageReport `
        -reporttypes:Html `
        "-classfilters:-*.g.cs" `
        "-filefilters:-*LoggerMessage.g.cs;-*/obj/*"

    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "`nCoverage report generated successfully!" -ForegroundColor Green
    Write-Host "Location: .\TestResults\CoverageReport\index.html" -ForegroundColor Green
    Write-Host "`nOpening report in browser..." -ForegroundColor Cyan
    Start-Process .\TestResults\CoverageReport\index.html
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
