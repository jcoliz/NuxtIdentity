# NuxtIdentity.EntityFrameworkCore.Tests

Integration tests for the NuxtIdentity.EntityFrameworkCore library.

## Overview

This test project contains Phase 2 happy path integration tests for the EF Core implementation:
- [`EfRefreshTokenService<TContext>`](../../src/EntityFrameworkCore/Services/EfRefreshTokenService.cs) - Database-backed refresh token management

## Test Coverage

### Current Coverage (Phase 2 - Happy Paths)

**11 integration tests** covering database persistence scenarios:

#### EfRefreshTokenServiceTests (11 tests)
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
| **Focus** | Business logic | Database persistence |
| **Dependencies** | Mocked | Real DbContext |
| **Test Count** | 28 tests | 11 tests |
| **Coverage Target** | 100% code coverage | Happy path scenarios |

## Next Steps (Future Phases)

### Phase 2 Remaining (Error Cases)
- Database constraint violations
- Concurrent access scenarios
- Transaction rollback handling
- Connection failure scenarios

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
