# Scripts

This directory contains PowerShell scripts for development, testing, and package management tasks.

## Available Scripts

### Build-Packages.ps1

Builds local copies of all NuGet packages and validates them before publishing.

**Usage:**
```powershell
# Build and validate all packages
.\scripts\Build-Packages.ps1

# Build packages without validation
.\scripts\Build-Packages.ps1 -SkipValidation
```

**What it does:**
- Cleans the `./artifacts` directory
- Builds all three NuGet packages in dependency order:
  - NuxtIdentity.Core
  - NuxtIdentity.AspNetCore
  - NuxtIdentity.EntityFrameworkCore
- Creates both `.nupkg` (package) and `.snupkg` (symbols) files
- Validates each package using `dotnet-validate` (if installed)
- Lists all created packages with file sizes

**Prerequisites:**
- For validation: `dotnet tool install -g dotnet-validate`

**Output:**
- Packages are created in `./artifacts` directory

---

### Collect-CodeCoverage.ps1

Runs all tests and collects code coverage metrics with a consolidated HTML report.

**Usage:**
```powershell
# Run all tests with code coverage
.\scripts\Collect-CodeCoverage.ps1
```

**What it does:**
- Cleans up previous test results
- Runs all test projects with code coverage collection:
  - NuxtIdentity.Core.Tests
  - NuxtIdentity.EntityFrameworkCore.Tests
  - NuxtIdentity.AspNetCore.Tests
- Generates a consolidated HTML coverage report
- Opens the report in your default browser

**Prerequisites:**
- ReportGenerator: `dotnet tool install -g dotnet-reportgenerator-globaltool`

**Output:**
- Coverage report: `./TestResults/CoverageReport/index.html`

---

## General Notes

- All scripts use `$PSScriptRoot` for path resolution, so they can be run from any directory
- Scripts follow PowerShell best practices with proper error handling and cleanup
- Exit codes indicate success (0) or failure (non-zero)
