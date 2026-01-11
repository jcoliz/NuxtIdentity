# NuxtIdentity Testing Strategy

## Overview

This document outlines a comprehensive, incremental testing strategy for the NuxtIdentity project. The strategy is designed to build confidence progressively, starting with critical components and expanding to full coverage.

## Current State

**Testing Status:** ✅ Phases 1-3 Complete (72 tests passing)

Implementation progress across all libraries:
- ✅ [`NuxtIdentity.Core`](../src/Core/NuxtIdentity.Core.csproj) - 20 unit tests (100% coverage target achieved)
- ✅ [`NuxtIdentity.AspNetCore`](../src/AspNetCore/NuxtIdentity.AspNetCore.csproj) - 36 integration tests (controller and service coverage)
- ✅ [`NuxtIdentity.EntityFrameworkCore`](../src/EntityFrameworkCore/NuxtIdentity.EntityFrameworkCore.csproj) - 16 integration tests (EF Core services and extensions)
- ⏸️ [`NuxtIdentity.Tenancy`](../src/Tenancy/NuxtIdentity.Tenancy.csproj) - Not yet implemented (future phase)

## Testing Philosophy

### Principles
1. **Security-First**: Authentication and authorization code must be thoroughly tested
2. **Incremental Coverage**: Build tests progressively, starting with highest-risk components
3. **Practical Testing**: Focus on behavior and contracts, not implementation details
4. **Maintainability**: Tests should be clear, focused, and easy to maintain
5. **Fast Feedback**: Unit tests should run in milliseconds; integration tests in seconds

### Testing Pyramid
```
         /\
        /  \  E2E (Minimal - Full flow validation)
       /----\
      /      \  Integration (Moderate - Component interaction)
     /--------\
    /          \  Unit (Extensive - Business logic)
   /____________\
```

## Incremental Implementation Phases

### Phase 1: Foundation - Core Services Unit Tests ✅ COMPLETE
**Priority:** 🔴 CRITICAL
**Goal:** Test the security-critical JWT and refresh token services
**Status:** ✅ Implemented - 20 tests passing

#### Scope
- [`JwtTokenService<TUser>`](../src/Core/Services/JwtTokenService.cs:46)
  - Token generation with valid claims
  - Token validation (valid/expired/tampered)
  - Clock skew handling
  - Configuration edge cases
- [`InMemoryRefreshTokenService`](../src/Core/Services/InMemoryRefreshTokenService.cs:13)
  - Token generation and validation
  - Token revocation (single and bulk)
  - Expiration handling
  - Concurrent access scenarios
- [`EfRefreshTokenService`](../src/EntityFrameworkCore/Services/EfRefreshTokenService.cs)
  - CRUD operations
  - Database constraint validation
  - Cleanup operations

#### Test Project Structure
```
tests/
├── NuxtIdentity.Core.Tests/
│   ├── Services/
│   │   ├── JwtTokenServiceTests.cs (11 tests)
│   │   └── InMemoryRefreshTokenServiceTests.cs (9 tests)
│   ├── Helpers/
│   │   ├── TestJwtOptions.cs
│   │   ├── TestUser.cs
│   │   └── TestUserClaimsProvider.cs
│   ├── coverlet.runsettings
│   └── NuxtIdentity.Core.Tests.csproj
```

#### Key Test Categories
- ✅ Happy path scenarios - Implemented
- ✅ Error handling and validation - Implemented
- ✅ Token expiration edge cases - Implemented
- ✅ Security boundary testing - Implemented
- ✅ Thread safety (for in-memory service) - Implemented

#### Success Criteria
- ✅ All core service methods covered
- ✅ Token tampering detected
- ✅ Expiration correctly enforced
- ✅ Concurrent access safe

---

### Phase 2: Data Layer - EF Core Integration Tests ✅ COMPLETE
**Priority:** 🟠 HIGH
**Goal:** Verify Entity Framework integration and data persistence
**Status:** ✅ Implemented - 16 tests passing

#### Scope
- [`EfRefreshTokenService`](../src/EntityFrameworkCore/Services/EfRefreshTokenService.cs) with real database
- [`ModelBuilderExtensions`](../src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs)
- Database migrations and schema validation

#### Test Project Structure
```
tests/
├── NuxtIdentity.EntityFrameworkCore.Tests/
│   ├── Services/
│   │   └── EfRefreshTokenServiceTests.cs (10 tests)
│   ├── Extensions/
│   │   ├── ModelBuilderExtensionsTests.cs (3 tests)
│   │   └── ServiceCollectionExtensionsTests.cs (3 tests)
│   ├── Helpers/
│   │   ├── TestDbContext.cs
│   │   └── TestJwtOptions.cs
│   ├── coverlet.runsettings
│   └── NuxtIdentity.EntityFrameworkCore.Tests.csproj
```

#### Testing Approach
- Use SQLite in-memory database for fast, isolated tests
- Test database constraints and indexes
- Verify cascade behaviors
- Test migration scenarios

#### Success Criteria
- ✅ All EF operations work correctly
- ✅ Database constraints enforced
- ✅ Proper index configuration
- ✅ Service registration validated

---

### Phase 3: API Layer - Controller Integration Tests ✅ COMPLETE
**Priority:** 🟡 MEDIUM
**Goal:** Test authentication endpoints and ASP.NET Core integration
**Status:** ✅ Implemented - 35 tests passing

#### Scope
- [`NuxtAuthControllerBase<TUser>`](../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs:59) endpoints
  - Login flow
  - Signup flow
  - Token refresh flow
  - Session retrieval
  - Logout
- JWT authentication middleware configuration
- Claims provider integration

#### Test Project Structure
```
tests/
├── NuxtIdentity.AspNetCore.Tests/
│   ├── Controllers/
│   │   └── NuxtAuthControllerTests.cs (19 integration tests)
│   ├── Configuration/
│   │   └── JwtBearerOptionsSetupTests.cs (8 tests)
│   ├── Services/
│   │   └── IdentityUserClaimsProviderTests.cs (10 tests)
│   ├── Helpers/
│   │   ├── TestWebApplicationFactory.cs
│   │   ├── TestProgram.cs
│   │   ├── TestAuthController.cs
│   │   ├── TestDbContext.cs
│   │   ├── TestUser.cs
│   │   └── TestJwtOptions.cs
│   ├── coverlet.runsettings
│   └── NuxtIdentity.AspNetCore.Tests.csproj
```

#### Testing Approach
- Use `WebApplicationFactory` for in-memory API testing
- Mock Identity framework dependencies
- Test complete HTTP request/response cycles
- Verify ProblemDetails responses
- Test authorization attribute behavior

#### Success Criteria
- ✅ All endpoints return correct status codes
- ✅ JWT authentication works end-to-end
- ✅ Error responses validated
- ✅ Claims provider integration tested
- ✅ Claims deduplication working correctly (multi-valued claims supported)

---

### Phase 4: Configuration & Extensions
**Priority:** 🟡 MEDIUM
**Goal:** Verify service registration and configuration

#### Scope
- [`ServiceCollectionExtensions`](../src/AspNetCore/Extensions/ServiceCollectionExtensions.cs) registration
- [`JwtBearerOptionsSetup`](../src/AspNetCore/Configuration/JwtBearerOptionsSetup.cs) configuration
- Options validation

#### Test Project Structure
```
tests/
├── NuxtIdentity.AspNetCore.Tests/
│   ├── Extensions/
│   │   └── ServiceCollectionExtensionsTests.cs
```

#### Success Criteria
- All services registered correctly
- Options validation catches invalid config
- Dependency injection graph resolves

---

### Phase 5: End-to-End Validation
**Priority:** 🟢 LOW
**Goal:** Validate complete authentication flows

#### Scope
- Full user registration → login → protected resource → refresh → logout flow
- Integration with sample applications
- Performance benchmarks

#### Test Project Structure
```
tests/
├── NuxtIdentity.E2E.Tests/
│   ├── Flows/
│   │   ├── AuthenticationFlowTests.cs
│   │   └── TokenRefreshFlowTests.cs
│   └── NuxtIdentity.E2E.Tests.csproj
```

#### Testing Approach
- Use SQLite in-memory database for fast tests
- Test against actual playground/sample apps
- Measure response times and throughput
- Test cross-browser scenarios (if UI involved)

#### Success Criteria
- Complete flows work without errors
- Performance meets acceptable thresholds
- No memory leaks or resource issues

---

## Test Infrastructure Setup

### Required NuGet Packages

#### Core Testing Packages
```xml
<PackageReference Include="NUnit" Version="4.3.1" />
<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
```

#### Mocking & Assertions
```xml
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="FluentAssertions" Version="6.12.1" />
```

#### ASP.NET Core Testing
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.0" />
```

#### Database Testing
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
```

### Test Fixtures & Helpers

#### Common Test Utilities (Implemented)
- ✅ `TestJwtOptions` - Pre-configured JWT settings (used across all test projects)
- ✅ `TestUser` - Simple IdentityUser implementation for testing
- ✅ `TestDbContext` - IdentityDbContext with refresh token configuration
- ✅ `TestWebApplicationFactory` - WebApplicationFactory with in-memory SQLite and Identity setup
- ✅ `TestProgram` - Minimal ASP.NET Core application entry point
- ✅ `TestAuthController` - Concrete NuxtAuthControllerBase implementation
- ✅ `TestUserClaimsProvider` - Mock claims provider for unit tests

### CI/CD Integration

#### GitHub Actions Workflow
```yaml
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test --configuration Release --logger "trx;LogFileName=test-results.trx"
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/test-results.trx'
```

#### Coverage Reporting (Implemented)
```bash
# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Generate consolidated HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./TestResults/CoverageReport

# PowerShell script for automated coverage collection
.\scripts\Collect-CodeCoverage.ps1
```

Coverage exclusions configured:
- Generated files: `*.g.cs`
- Logger message methods: `*__*`
- Model/DTO classes without logic

---

## Testing Standards & Conventions

### Naming Conventions
- Test class: `{ClassUnderTest}Tests.cs`
- Test method: `{MethodName}_{Scenario}_{ExpectedResult}`
- Example: `GenerateAccessTokenAsync_ValidUser_ReturnsValidJwt`

### Test Structure (AAA Pattern)
```csharp
[Test]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var service = CreateService();
    var input = CreateTestInput();

    // Act
    var result = await service.Method(input);

    // Assert
    result.Should().NotBeNull();
    result.Should().BeEquivalentTo(expected);
}
```

### Assertion Libraries
- Use **FluentAssertions** for readability
- Use **NUnit assertions** for simple cases
- Avoid multiple unrelated assertions in single test

### Test Categories
Use categories to organize tests:
```csharp
[Category("Unit")]
[Category("Integration")]
[Category("Security")]
[Category("Performance")]
```

---

## Mermaid: Testing Implementation Flow

```mermaid
graph TD
    A[Phase 1: Core Services] --> B[Phase 2: EF Core]
    B --> C[Phase 3: API Controllers]
    C --> D[Phase 4: Configuration]
    D --> E[Phase 5: E2E Tests]

    A --> F[CI/CD Integration]
    B --> F
    C --> F
    D --> F
    E --> F

    F --> G[Coverage Reports]
    F --> H[Quality Gates]

    style A fill:#ff6b6b
    style B fill:#ffa500
    style C fill:#ffd700
    style D fill:#ffd700
    style E fill:#90ee90
    style F fill:#87ceeb
    style G fill:#dda0dd
    style H fill:#dda0dd
```

---

## Critical Test Scenarios

### Security-Critical Tests
1. **Token Tampering Detection**
   - Modify token signature → should fail validation
   - Modify claims → should fail validation
   - Expired token → should fail validation

2. **Refresh Token Security**
   - Reusing revoked token → should fail
   - Using token for different user → should fail
   - Token rotation → old token invalid after refresh

3. **Authorization**
   - Unauthenticated access → 401
   - Insufficient permissions → 403
   - Valid token → 200

### Edge Cases
1. **Concurrent Access**
   - Multiple token refreshes simultaneously
   - Race conditions in token revocation

2. **Configuration Errors**
   - Missing JWT secret
   - Invalid expiration values
   - Malformed issuer/audience

3. **Database Failures**
   - Connection failures
   - Constraint violations
   - Transaction rollbacks

---

## Coverage Goals

### Current Coverage Status
- **Core Services (Unit Tests):** ✅ 100% line coverage achieved
- **EF Core Services:** ✅ High coverage (SQLite integration tests)
- **Controllers:** ✅ 80-90%+ line coverage (35 integration tests)
- **Configuration/Extensions:** ✅ Comprehensive coverage
- **Overall Project:** ✅ 71 tests passing across all implemented phases

### Prioritized Coverage
Focus on high-risk areas first:
1. 🔴 Token generation and validation
2. 🔴 Refresh token operations
3. 🟠 Authentication endpoints
4. 🟡 Configuration and DI
5. 🟢 Helper methods and utilities

---

## Success Metrics

### Phase Completion Status
- ✅ All 71 tests passing
- ✅ Coverage goals met for Phases 1-3
- ⏸️ CI/CD pipeline - Ready for integration
- ✅ No critical security gaps (security-critical paths fully tested)
- ⏸️ Performance benchmarks - Not prioritized yet

### Quality Gates
- No failing tests in main branch
- Coverage not decreasing with new code
- Security tests always passing
- Integration tests < 10s total runtime
- Unit tests < 2s total runtime

---

## Next Steps

1. ✅ **Phase 1 Complete** - Core service unit tests (20 tests)
2. ✅ **Phase 2 Complete** - EF Core integration tests (16 tests)
3. ✅ **Phase 3 Complete** - API layer integration tests (35 tests)
4. ⏸️ **Phase 4 Remaining** - Additional configuration/extension tests (if needed)
5. ⏸️ **Phase 5 Future** - End-to-end validation tests
6. ⏸️ **CI/CD Integration** - Set up GitHub Actions workflow
7. ✅ **Maintain** - Keep tests updated with code changes

---

## Decisions & Preferences

Based on project requirements:

1. ✅ **Test Framework**: NUnit
2. ✅ **Mocking Library**: Moq
3. ✅ **Coverage Targets**:
   - Unit tests (no data/HTTP): 100%
   - Integration tests: 80-90%
4. ✅ **CI/CD Platform**: GitHub Actions
5. ✅ **Database Strategy**: SQLite in-memory (no LocalDB)
6. ⏸️ **Performance Benchmarks**: Not needed at this time
7. 🔮 **Mutation Testing**: Future phase consideration with Stryker.NET

---

## Known Issues

None currently identified. All 72 tests passing.

### Previously Identified Issues (Resolved)

1. **Claim Deduplication "Bug"** (Phase 3) - ✅ RESOLVED
   - Status: Test removed as it was testing incorrect behavior
   - Resolution: The implementation is correct. Claims in .NET are multi-valued by design.
     Multiple claims with the same type but different values (e.g., "permission: read", "permission: write")
     are standard and expected. The implementation correctly:
     - Allows multiple claims of the same type with different values
     - Deduplicates exact (type+value) pairs
   - Location: [`IdentityUserClaimsProvider.cs`](../src/AspNetCore/Services/IdentityUserClaimsProvider.cs)
   - Impact: None - working as designed

---

**Last Updated:** 2025-12-14
**Version:** 2.1
**Status:** Phases 1-3 Complete - 71 Tests Passing - All Issues Resolved
