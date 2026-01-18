<#
.SYNOPSIS
Builds local copies of all NuGet packages and validates them.

.DESCRIPTION
This script builds all NuxtIdentity NuGet packages with full validation to ensure
they are ready for publishing. It performs a clean build, creates packages in the
./artifacts directory, and validates each package using dotnet-validate.

The script builds packages in dependency order:
1. NuxtIdentity.Core
2. NuxtIdentity.AspNetCore
3. NuxtIdentity.EntityFrameworkCore

.PARAMETER SkipValidation
When specified, skips package validation with dotnet-validate. Useful for quick builds
during development.

.EXAMPLE
.\Build-Packages.ps1
Builds all packages and validates them.

.EXAMPLE
.\Build-Packages.ps1 -SkipValidation
Builds all packages without validation.

.NOTES
Requires dotnet-validate to be installed globally for validation:
    dotnet tool install -g dotnet-validate

Packages are output to ./artifacts directory and include both .nupkg and .snupkg (symbols) files.

.LINK
https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack
https://github.com/NuGet/NuGetGallery/tree/main/src/VerifyMicrosoftPackage
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]
    $SkipValidation
)

$ErrorActionPreference = "Stop"

# Helper function to validate a package
function Test-Package {
    param(
        [Parameter(Mandatory=$true)]
        [string]
        $PackagePath
    )

    Write-Host "  Validating package: $(Split-Path $PackagePath -Leaf)" -ForegroundColor Cyan

    dotnet validate package $PackagePath

    if ($LASTEXITCODE -ne 0) {
        throw "Package validation failed for $PackagePath with exit code $LASTEXITCODE"
    }

    Write-Host "  OK Package validation passed" -ForegroundColor Green
}

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    Push-Location $repoRoot

    $ArtifactsDir = "./artifacts"
    $PackageProjects = @(
        @{ Path = "src/Core/NuxtIdentity.Core.csproj"; Name = "NuxtIdentity.Core" },
        @{ Path = "src/AspNetCore/NuxtIdentity.AspNetCore.csproj"; Name = "NuxtIdentity.AspNetCore" },
        @{ Path = "src/EntityFrameworkCore/NuxtIdentity.EntityFrameworkCore.csproj"; Name = "NuxtIdentity.EntityFrameworkCore" }
    )

    Write-Host "Building NuxtIdentity NuGet Packages" -ForegroundColor Cyan
    Write-Host "===================================`n" -ForegroundColor Cyan

    # Clean artifacts directory
    Write-Host "Cleaning artifacts directory..." -ForegroundColor Cyan
    Remove-Item $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
    Write-Host "OK Artifacts directory cleaned`n" -ForegroundColor Green

    # Build and pack each project
    foreach ($project in $PackageProjects) {
        Write-Host "Building: $($project.Name)" -ForegroundColor Yellow

        dotnet pack $project.Path `
            --configuration Release `
            --output $ArtifactsDir `
            /p:ContinuousIntegrationBuild=true

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $($project.Name) with exit code $LASTEXITCODE"
        }

        Write-Host "OK Package created: $($project.Name)`n" -ForegroundColor Green
    }

    # List created packages
    Write-Host "Packages created in $ArtifactsDir`:" -ForegroundColor Cyan
    Get-ChildItem $ArtifactsDir -Filter "*.nupkg" | ForEach-Object {
        $size = [math]::Round($_.Length / 1KB, 2)
        Write-Host "  $($_.Name) ($size KB)" -ForegroundColor White
    }
    Write-Host ""

    # Validate packages if not skipped
    if (-not $SkipValidation) {
        Write-Host "Validating packages..." -ForegroundColor Cyan

        # Check if dotnet-validate is installed
        $validateInstalled = dotnet tool list -g | Select-String "dotnet-validate"
        if (-not $validateInstalled) {
            Write-Host "WARNING: dotnet-validate is not installed globally." -ForegroundColor Yellow
            Write-Host "Install it with: dotnet tool install -g dotnet-validate" -ForegroundColor Yellow
            Write-Host "Skipping validation...`n" -ForegroundColor Yellow
        }
        else {
            # Validate each package (only .nupkg, not .snupkg)
            Get-ChildItem $ArtifactsDir -Filter "*.nupkg" -Exclude "*.snupkg" | ForEach-Object {
                Test-Package -PackagePath $_.FullName
            }
            Write-Host "`nOK All packages validated successfully" -ForegroundColor Green
        }
    }
    else {
        Write-Host "Skipping package validation (use without -SkipValidation to validate)`n" -ForegroundColor Yellow
    }

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Packages location: $ArtifactsDir" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build packages: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
