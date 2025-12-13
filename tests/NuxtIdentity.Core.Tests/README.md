# NuxtIdentity.Core.Tests

Unit tests for the NuxtIdentity.Core library.

## Overview

This test project contains Phase 1 happy path tests for the core authentication services:
- [`JwtTokenService<TUser>`](../../src/Core/Services/JwtTokenService.cs) - JWT token generation and validation
- [`InMemoryRefreshTokenService`](../../src/Core/Services/InMemoryRefreshTokenService.cs) - Refresh token management

## Test Coverage

### Current Coverage (Phase 1 - Happy Paths)

```
Overall: 73.3% line coverage

Core Services:
- InMemoryRefreshTokenService: 98.7% coverage
- JwtTokenService<T>: 64.7% coverage
```

**Note:** The 0% coverage on model classes is expected - they are simple DTOs with no logic to test.

### Test Count
- **Total Tests:** 21
- **All Passing:** ✅

### Breakdown by Class

#### JwtTokenServiceTests (8 tests)
Tests use **Gherkin-style comments** (Given/When/Then/And) for improved readability:

- ✅ Token generation returns valid JWT
- ✅ Token contains user claims (NameIdentifier, Name, Email)
- ✅ Token contains standard claims (iat, nbf, issuer, audience)
- ✅ Token expires at correct time
- ✅ Token validation returns ClaimsPrincipal
- ✅ Validated principal contains all claims
- ✅ GetTokenValidationParameters returns correct configuration
- ✅ Multiple users generate unique tokens

#### InMemoryRefreshTokenServiceTests (13 tests)
Tests use **Gherkin-style comments** (Given/When/Then/And) for improved readability:

- ✅ Token generation returns non-empty token
- ✅ Token generation returns valid Base64 string
- ✅ Multiple calls return unique tokens
- ✅ Valid token validates successfully
- ✅ Non-existent token validation fails
- ✅ Wrong user ID validation fails
- ✅ Revoked token validation fails
- ✅ Token revocation makes token invalid
- ✅ Revoking non-existent token doesn't throw
- ✅ Revoking all user tokens invalidates all
- ✅ Revocation only affects specific user
- ✅ Revoking for non-existent user doesn't throw
- ✅ Different users' tokens are isolated

### Test Structure Example

All tests follow Gherkin-style comments for clarity:

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

This makes tests more readable and helps understand:
- **Given**: The preconditions and setup
- **When**: The action being tested
- **Then**: The expected outcome
- **And**: Additional context or assertions

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run with coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Generate coverage report
```bash
reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:TestResults/CoverageReport -reporttypes:Html
```

## Test Structure

### Helpers
- [`TestJwtOptions`](Helpers/TestJwtOptions.cs) - Pre-configured JWT settings for tests
- [`TestUser`](Helpers/TestUser.cs) - Simple test user class
- [`TestUserClaimsProvider`](Helpers/TestUserClaimsProvider.cs) - Claims provider for test users

### Test Classes
- [`JwtTokenServiceTests`](Services/JwtTokenServiceTests.cs) - Tests for JWT token service
- [`InMemoryRefreshTokenServiceTests`](Services/InMemoryRefreshTokenServiceTests.cs) - Tests for refresh token service

## Next Steps (Future Phases)

### Phase 1 Remaining (Error Cases)
- Token tampering detection
- Expired token validation
- Invalid configuration handling
- Clock skew edge cases
- Thread safety tests

### Phase 2 (EF Core Integration)
- Database persistence tests
- Constraint validation
- Transaction handling

### Phase 3 (API Controllers)
- Endpoint integration tests
- Authentication flow tests
- Error response validation

See [`../../plans/testing-strategy.md`](../../plans/testing-strategy.md) for complete strategy.

## Technologies

- **Test Framework:** NUnit 4.3.1
- **Mocking:** Moq 4.20.72
- **Assertions:** FluentAssertions 6.12.1
- **Coverage:** coverlet.collector 6.0.2
