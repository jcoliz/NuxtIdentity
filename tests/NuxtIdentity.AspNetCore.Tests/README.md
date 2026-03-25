# NuxtIdentity.AspNetCore.Tests

Integration tests for the NuxtIdentity ASP.NET Core library.

## Test Coverage

This test project covers:

### Controllers
- [`NuxtAuthControllerBase<TUser>`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs) - Authentication endpoints
  - Login flow
  - Signup flow (open and invitation-based)
  - Invitation validation (`GET /api/auth/invitations/{code}/status`)
  - Invitation-only registration mode
  - Token refresh flow
  - Session retrieval
  - Logout
  - Password management (forgot, reset, change)

### Configuration
- [`JwtBearerOptionsSetup`](../../src/AspNetCore/Configuration/JwtBearerOptionsSetup.cs) - JWT Bearer authentication configuration

### Services
- [`IdentityUserClaimsProvider<TUser>`](../../src/AspNetCore/Services/IdentityUserClaimsProvider.cs) - Claims extraction from Identity users

## Testing Approach

These are integration tests that use:
- `WebApplicationFactory` for in-memory API testing
- Moq for mocking Identity framework dependencies
- SQLite in-memory database for fast, isolated tests
- FluentAssertions for readable assertions

## Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Run specific test category
dotnet test --filter Category=Integration
```

## Test Structure

Tests follow the Arrange-Act-Assert (AAA) pattern and use NUnit as the test framework.

### Naming Convention
- Test class: `{ClassUnderTest}Tests.cs`
- Test method: `{MethodName}_{Scenario}_{ExpectedResult}`
- Example: `Login_ValidCredentials_ReturnsTokenAndUser`
