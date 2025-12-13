<#
.SYNOPSIS
Runs tests and collects code coverage metrics.

.DESCRIPTION
This script executes tests with code coverage collection and generates an HTML report.
It uses the XPlat Code Coverage collector and ReportGenerator to create a detailed
coverage report that opens automatically in your browser.

.PARAMETER Tests
The test category to run. Default value is "Unit". Other possible values include "Functional".

.EXAMPLE
.\Collect-CodeCoverage.ps1
Runs Unit tests and generates a code coverage report.

.EXAMPLE
.\Collect-CodeCoverage.ps1 -Tests "Functional"
Runs Functional tests and generates a code coverage report.

.NOTES
Requires ReportGenerator to be installed globally:
    dotnet tool install -g dotnet-reportgenerator-globaltool

The coverage report will be generated in .\bin\result\index.html and opened automatically.

.LINK
https://github.com/danielpalme/ReportGenerator
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("Core")]
    [string]
    $Tests="Core"
)

$ErrorActionPreference = "Stop"

try {
    $TestPath = "$PSScriptRoot/../tests/NuxtIdentity.$Tests.Tests"

    if (-not (Test-Path $TestPath)) {
        throw "Test directory not found: $TestPath"
    }

    Push-Location $TestPath

    Write-Host "Cleaning up previous test results..." -ForegroundColor Cyan
    Remove-Item TestResults -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item bin\result -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "Running $Tests tests with code coverage..." -ForegroundColor Cyan
    dotnet clean --nologo --verbosity quiet
    dotnet test --collect:"XPlat Code Coverage" --settings:coverlet.runsettings
    if ($LASTEXITCODE -ne 0) {
        throw "Test execution failed with exit code $LASTEXITCODE"
    }

    Write-Host "Generating coverage report..." -ForegroundColor Cyan
    reportgenerator `
        -reports:.\TestResults\*\coverage.cobertura.xml `
        -targetdir:.\bin\result `
        "-classfilters:-*.g.cs" `
        "-filefilters:-*LoggerMessage.g.cs;-*\obj\*"
    if ($LASTEXITCODE -ne 0) {
        throw "Report generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "Coverage report generated successfully" -ForegroundColor Green
    Write-Host "Opening report in browser..." -ForegroundColor Cyan
    Start-Process .\bin\result\index.html
}
catch {
    Write-Error "Failed to collect code coverage: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
