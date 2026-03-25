# NuxtIdentity.EntityFrameworkCore.Tests

Integration tests for the NuxtIdentity.EntityFrameworkCore library.

## Overview

This test project contains comprehensive integration tests for the EF Core implementation:
- [`EfRefreshTokenService<TContext>`](../../src/EntityFrameworkCore/Services/EfRefreshTokenService.cs) - Database-backed refresh token management
- [`EfInvitationService<TContext>`](../../src/EntityFrameworkCore/Services/EfInvitationService.cs) - Database-backed invitation management
- [`ModelBuilderExtensions`](../../src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs) - Entity configuration
- [`ServiceCollectionExtensions`](../../src/EntityFrameworkCore/Extensions/ServiceCollectionExtensions.cs) - Dependency injection setup

## Test Coverage

### Current Coverage

**83 integration tests** covering database persistence, configuration, and service registration:

#### EfRefreshTokenServiceTests (15 tests)
Tests use **Gherkin-style comments** (Given/When/Then/And) for improved readability and an **in-memory database** for isolation:

**Token Generation:**
- ✅ Token generation returns non-empty token
- ✅ Token is stored in database with correct user ID
- ✅ Token has correct expiration date
- ✅ Multiple calls generate unique tokens
- ✅ Different users' tokens are isolated

**Token Validation:**
- ✅ Valid token validates successfully
- ✅ Non-existent token validation fails
- ✅ Wrong user ID validation fails

**Token Revocation:**
- ✅ Revoked token becomes invalid
- ✅ Revoking all user tokens invalidates all tokens for that user
- ✅ Revocation only affects specific user's tokens

**Error Cases:**
- ✅ Expired tokens are rejected
- ✅ Non-existent token revocation doesn't throw
- ✅ Expired tokens are automatically cleaned up
- ✅ Revoked tokens update expiration date
- ✅ Token hash is stored (not plaintext)
- ✅ Base64 token format validation

#### ModelBuilderExtensionsTests (12 tests)
Tests verify Entity Framework configuration:

**Entity Configuration:**
- ✅ Entity can be queried
- ✅ Supports basic CRUD operations
- ✅ TokenHash index allows fast lookup
- ✅ UserId index allows fast user lookup
- ✅ Required properties are enforced (TokenHash, UserId)
- ✅ Primary key auto-generation works
- ✅ Multiple entities get unique IDs
- ✅ Update operations work correctly
- ✅ Delete operations work correctly

#### EfInvitationServiceTests (30 tests)
Tests use **Gherkin-style comments** (Given/When/Then/And) and **in-memory database** for isolation:

**Invitation Creation:**
- ✅ Create with valid data returns entity with generated code
- ✅ Create persists to database
- ✅ Create always uses Pending status
- ✅ Roles and claims are JSON-serialized
- ✅ Empty roles/claims stored as null
- ✅ Metadata is stored correctly
- ✅ Expiration calculated correctly from duration
- ✅ No parameters uses sensible defaults (30 day expiry)
- ✅ Null roles/claims/email stored as null

**Test Invitation Support:**
- ✅ `CreateTestAsync` with all properties persists correctly
- ✅ `CreateTestAsync` forces IsTest=true even if caller sets false
- ✅ `CreateTestAsync` generates code when Guid.Empty
- ✅ `CreateTestAsync` applies default timestamps
- ✅ `CreateTestAsync` ignores caller-provided Id
- ✅ `CreateTestAsync` with null email throws ArgumentException
- ✅ `CreateTestAsync` with empty email throws ArgumentException
- ✅ `CreateTestAsync` with null invitation throws ArgumentNullException
- ✅ Generated code available on passed-in object
- ✅ `DeleteTestInvitationsAsync` deletes test invitations only
- ✅ `DeleteTestInvitationsAsync` returns zero when no test invitations
- ✅ `DeleteTestInvitationsAsync` returns zero on empty database

**Invitation Queries and Validation:**
- ✅ GetByCodeAsync, ResolveStatusAsync, ValidateAsync, AcceptAsync

#### ServiceCollectionExtensionsTests (13 tests)
Tests verify dependency injection registration:

**Service Registration:**
- ✅ Registers IRefreshTokenService
- ✅ Service is scoped
- ✅ Returns same service collection for chaining
- ✅ Can be called multiple times
- ✅ Can resolve service in scope
- ✅ Service can perform operations
- ✅ Registers correct implementation for specific context

**Full Stack Registration:**
- ✅ AddNuxtIdentityWithEntityFramework registers all required services
- ✅ Configures JWT options from configuration
- ✅ Configures authentication services
- ✅ Returns same service collection for chaining

## Test Infrastructure

### Helpers
- [`TestDbContext`](Helpers/TestDbContext.cs) - Test database context with RefreshTokens DbSet
- [`TestJwtOptions`](Helpers/TestJwtOptions.cs) - Pre-configured JWT settings for tests

### Test Classes
- [`EfRefreshTokenServiceTests`](Services/EfRefreshTokenServiceTests.cs) - Integration tests for EF Core refresh token service

### Database Strategy
Tests use **Entity Framework Core InMemory provider** for:
- Fast test execution
- Test isolation (each test gets a new database)
- No external dependencies
- Reliable cleanup

Each test creates a unique in-memory database instance using:
```csharp
var options = new DbContextOptionsBuilder<TestDbContext>()
    .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
    .Options;
```

## Running Tests

### Run all EF Core integration tests
```bash
dotnet test tests/NuxtIdentity.EntityFrameworkCore.Tests
```

### Run with detailed output
```bash
dotnet test tests/NuxtIdentity.EntityFrameworkCore.Tests --logger "console;verbosity=detailed"
```

### Run a specific test
```bash
dotnet test tests/NuxtIdentity.EntityFrameworkCore.Tests --filter "FullyQualifiedName~ValidateRefreshTokenAsync_ValidToken_ReturnsTrue"
```

## Test Structure Example

All tests follow Gherkin-style comments and use FluentAssertions for clarity:

```csharp
[Test]
public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsTrue()
{
    // Given a valid user ID
    var userId = "user123";
    // And a generated refresh token for that user
    var token = await _service.GenerateRefreshTokenAsync(userId);

    // When validating the token with the correct user ID
    var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

    // Then validation should succeed
    isValid.Should().BeTrue();
}
```

## Key Differences from Core Tests

| Aspect | Core Tests | EF Core Tests |
|--------|-----------|---------------|
| **Scope** | Unit tests | Integration tests |
| **Storage** | In-memory collection | In-memory EF Core database |
| **Focus** | Business logic | Database persistence & configuration |
| **Dependencies** | Mocked | Real DbContext |
| **Test Count** | 14 tests | 40 tests |
| **Coverage Target** | 100% code coverage | Database operations & DI setup |

## Next Steps (Future Phases)

### Phase 3 (API Controllers)
- Endpoint integration tests
- Authentication flow tests
- Error response validation

See [`../../plans/testing-strategy.md`](../../plans/testing-strategy.md) for the complete testing strategy.

## Technologies

- **Test Framework:** NUnit 4.3.1
- **Database:** EF Core 10.0.0 InMemory Provider
- **Mocking:** Moq 4.20.72
- **Assertions:** FluentAssertions 6.12.1
- **Coverage:** coverlet.collector 6.0.2
