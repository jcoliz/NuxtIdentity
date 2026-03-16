---
status: Approved
prd: PRD-PASSWORD-MANAGEMENT.md
---

# Design Document: Password Management

## Overview

This document describes the changes needed to add password management endpoints to NuxtIdentity: forgot-password, reset-password, and change-password. It also introduces the `IUserNotifier<TUser>` abstraction for delivering reset codes, and an `InMemoryUserNotifier<TUser>` for test environments.

All new controller methods are `virtual`, following the existing pattern in [`NuxtAuthControllerBase`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs).

---

## Component Changes

### 1. IUserNotifier&lt;TUser&gt; Interface

**File**: `src/Core/Abstractions/IUserNotifier.cs` (new)

Define the notification abstraction in the Core project, following the same pattern as [`IUserClaimsProvider<TUser>`](../../src/Core/Abstractions/IUserClaimsProvider.cs):

```csharp
namespace NuxtIdentity.Core.Abstractions;

public interface IUserNotifier<TUser> where TUser : class
{
    Task SendResetCodeAsync(TUser user, string resetCode);
    Task SendEmailConfirmationAsync(TUser user, string confirmationCode);
}
```

The interface is generic with `TUser : class` (not `IdentityUser`) to match the claims provider pattern. The `SendEmailConfirmationAsync` method is included for future use per PRD but is not called by any endpoint in this implementation.

---

### 2. Request Models

**File**: [`src/Core/Models/AuthModels.cs`](../../src/Core/Models/AuthModels.cs)

Add three new request records to the existing `#region Request Models` section, after `RefreshRequest`:

```csharp
public record ForgotPasswordRequest
{
    public string? Username { get; init; }
    public string? Email { get; init; }
}

public record ResetPasswordRequest
{
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

public record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
```

The `ForgotPasswordRequest` and `ResetPasswordRequest` have separate optional `Username` and `Email` fields. The consumer populates whichever one their app uses. Each record needs full XML documentation following the project conventions.

---

### 3. Controller Endpoints

**File**: [`src/AspNetCore/Controllers/NuxtAuthControllerBase.cs`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs)

#### 3.1. New Constructor Parameter

Add an `IEnumerable<IUserNotifier<TUser>>` parameter to the primary constructor. Consumers may not register a notifier (per PRD Story 4 — if no notifier is registered, the endpoint still succeeds but generates a warning log). ASP.NET Core's built-in DI container does not support nullable constructor parameters for unregistered services — it throws `InvalidOperationException`. Using `IEnumerable<T>` is the idiomatic workaround: if no implementation is registered, an empty enumerable is injected. This follows the same pattern as `claimsProviders` which is already injected as `IEnumerable`.

Update the primary constructor signature (line 61):

```csharp
public abstract partial class NuxtAuthControllerBase<TUser>(
    IJwtTokenService<TUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<TUser>> claimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<TUser> userManager,
    SignInManager<TUser> signInManager,
    IEnumerable<IUserNotifier<TUser>> userNotifiers,
    ILogger logger) : ControllerBase
    where TUser : IdentityUser, new()
```

Add a protected property:

```csharp
protected IUserNotifier<TUser>? UserNotifier { get; } = userNotifiers.FirstOrDefault();
```

#### 3.2. User Lookup Helper

Add a private helper method to consolidate the username/email lookup pattern used by both forgot-password and reset-password:

```csharp
private async Task<TUser?> FindUserByUsernameOrEmailAsync(string? username, string? email)
{
    if (!string.IsNullOrEmpty(username))
        return await UserManager.FindByNameAsync(username);

    if (!string.IsNullOrEmpty(email))
        return await UserManager.FindByEmailAsync(email);

    return null;
}
```

#### 3.3. ForgotPassword Endpoint

Add a new virtual method in a new `#region Password Management Endpoints` section after the Authentication Endpoints region:

```csharp
[HttpPost("forgot-password")]
[ProducesResponseType(StatusCodes.Status200OK)]
public virtual async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
{
    LogStarting();

    var user = await FindUserByUsernameOrEmailAsync(request.Username, request.Email);

    if (user != null)
    {
        var code = await UserManager.GeneratePasswordResetTokenAsync(user);

        if (UserNotifier != null)
        {
            await UserNotifier.SendResetCodeAsync(user, code);
        }
        else
        {
            LogNoUserNotifierConfigured();
        }
    }

    // Always return success to prevent user enumeration
    LogOk();
    return Ok(new { success = true });
}
```

Key design decisions:
- Always returns 200 OK regardless of whether the user exists (prevents user enumeration)
- Only one `ProducesResponseType` — this endpoint never returns an error status
- If no `IUserNotifier` is registered, logs a warning but still succeeds
- The reset code is generated by ASP.NET Identity's `GeneratePasswordResetTokenAsync`

#### 3.4. ResetPassword Endpoint

```csharp
[HttpPost("reset-password")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public virtual async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
{
    LogStarting();

    var user = await FindUserByUsernameOrEmailAsync(request.Username, request.Email);

    if (user == null)
    {
        LogResetPasswordFailed("User not found");
        return Problem(
            title: "Password Reset Failed",
            detail: "Invalid request",
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    var result = await UserManager.ResetPasswordAsync(user, request.Code, request.NewPassword);

    if (!result.Succeeded)
    {
        LogResetPasswordFailed(string.Join(", ", result.Errors.Select(e => e.Description)));
        return Problem(
            title: "Password Reset Failed",
            detail: string.Join("; ", result.Errors.Select(e => e.Description)),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    // Revoke all refresh tokens for security (Story 7)
    await RefreshTokenService.RevokeAllUserTokensAsync(user.Id);

    LogOk();
    return Ok(new { success = true });
}
```

Key design decisions:
- Returns 400 Bad Request if the user is not found (unlike forgot-password, this endpoint is meant to fail visibly since the user already has a code)
- ASP.NET Identity's `ResetPasswordAsync` handles both code validation and password validation in one call. If the code is invalid/expired or the password is weak, the errors come back in `IdentityResult.Errors`
- All refresh tokens are revoked after successful reset (Story 7)
- Error detail includes all Identity error descriptions joined together (Story 5 — multiple validation failures returned together)

#### 3.5. ChangePassword Endpoint

```csharp
[HttpPost("change-password")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public virtual async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
{
    LogStarting();

    var username = GetCurrentUsername();
    if (username == null)
    {
        LogChangePasswordUnauthorized("No username in token");
        return Problem(
            title: "Authentication Required",
            detail: "No valid authentication token provided",
            statusCode: StatusCodes.Status401Unauthorized
        );
    }

    var user = await UserManager.FindByNameAsync(username);
    if (user == null)
    {
        LogChangePasswordUnauthorized($"User not found: {username}");
        return Problem(
            title: "User Not Found",
            detail: "The authenticated user no longer exists",
            statusCode: StatusCodes.Status401Unauthorized
        );
    }

    var result = await UserManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

    if (!result.Succeeded)
    {
        LogChangePasswordFailed(username, string.Join(", ", result.Errors.Select(e => e.Description)));
        return Problem(
            title: "Password Change Failed",
            detail: string.Join("; ", result.Errors.Select(e => e.Description)),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    // Revoke all refresh tokens for security (Story 7)
    await RefreshTokenService.RevokeAllUserTokensAsync(user.Id);

    LogOkUsername(username);
    return Ok(new { success = true });
}
```

Key design decisions:
- Requires `[Authorize]` — valid JWT access token must be present
- Uses `GetCurrentUsername()` and `FindByNameAsync()` — same pattern as [`GetSession()`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs:304)
- ASP.NET Identity's `ChangePasswordAsync` verifies the current password and validates the new password in one call
- Returns 401 if unauthenticated/user not found, 400 for password validation failures
- All refresh tokens are revoked after successful change (Story 7)

#### 3.6. New Logger Messages

Add new logger messages to the `#region Logger Messages` section. Continue from the existing event ID sequence (currently 10 is the highest):

```csharp
[LoggerMessage(11, LogLevel.Warning, "{Location}: No IUserNotifier configured")]
private partial void LogNoUserNotifierConfigured([CallerMemberName] string? location = null);

[LoggerMessage(12, LogLevel.Warning, "{Location}: Reset password failed {Reason}")]
private partial void LogResetPasswordFailed(string reason, [CallerMemberName] string? location = null);

[LoggerMessage(13, LogLevel.Warning, "{Location}: Change password unauthorized {Reason}")]
private partial void LogChangePasswordUnauthorized(string reason, [CallerMemberName] string? location = null);

[LoggerMessage(14, LogLevel.Warning, "{Location}: Change password failed {Username} {Errors}")]
private partial void LogChangePasswordFailed(string username, string errors, [CallerMemberName] string? location = null);
```

---

### 4. InMemoryUserNotifier&lt;TUser&gt;

**File**: `src/Core/Services/InMemoryUserNotifier.cs` (new)

This is a test/development helper that captures notification data in memory (Story 8). It's in the Core project so test projects can reference it without depending on ASP.NET Core.

```csharp
namespace NuxtIdentity.Core.Services;

public class InMemoryUserNotifier<TUser> : IUserNotifier<TUser> where TUser : class
{
    private readonly List<NotificationRecord> _notifications = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SendResetCodeAsync(TUser user, string resetCode) { ... }
    public async Task SendEmailConfirmationAsync(TUser user, string confirmationCode) { ... }
    public async Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync() { ... }
    public async Task ClearAsync() { ... }
}
```

#### NotificationRecord Model

**File**: `src/Core/Models/NotificationRecord.cs` (new)

```csharp
namespace NuxtIdentity.Core.Models;

public record NotificationRecord
{
    public string Code { get; init; } = string.Empty;
    public NotificationType Type { get; init; }
    public DateTime Timestamp { get; init; }
}

public enum NotificationType
{
    PasswordReset,
    EmailConfirmation
}
```

Note: The PRD mentions capturing the email address, but since the `IUserNotifier` receives a `TUser` object (not specifically an `IdentityUser`), the `InMemoryUserNotifier` stores the raw user object reference. Consumers who need to query by email can filter using their own logic, or the design can be extended with a `Func<TUser, string>` email extractor in the constructor. For simplicity in this implementation, we store the notification indexed in a simple list and provide a `GetNotificationsAsync()` that returns all notifications. The consumer wraps this in a test endpoint as described in the PRD.

---

### 5. TestAuthController Update

**File**: [`tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestAuthController.cs`](../../tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestAuthController.cs)

Update the constructor to include the new `IEnumerable<IUserNotifier<TUser>>` parameter:

```csharp
public class TestAuthController : NuxtAuthControllerBase<TestUser>
{
    public TestAuthController(
        IJwtTokenService<TestUser> jwtTokenService,
        IRefreshTokenService refreshTokenService,
        UserManager<TestUser> userManager,
        SignInManager<TestUser> signInManager,
        IEnumerable<IUserNotifier<TestUser>> userNotifiers,
        ILogger<TestAuthController> logger)
        : base(jwtTokenService, [], refreshTokenService, userManager, signInManager, userNotifiers, logger)
    {
    }
}
```

Note: The existing constructor passes `claimsProviders` positionally. The new parameter order must match the updated base constructor. The `[]` for `claimsProviders` uses the existing pattern — check the current test to confirm whether claims providers are injected from DI or passed as empty.

---

### 6. TestWebApplicationFactory Update

**File**: [`tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestWebApplicationFactory.cs`](../../tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestWebApplicationFactory.cs)

Register `InMemoryUserNotifier<TestUser>` as both `IUserNotifier<TestUser>` and as a singleton (so tests can retrieve captured notifications):

```csharp
// In ConfigureWebHost, after AddNuxtIdentity<TestUser>():
var testNotifier = new InMemoryUserNotifier<TestUser>();
services.AddSingleton<InMemoryUserNotifier<TestUser>>(testNotifier);
services.AddSingleton<IUserNotifier<TestUser>>(testNotifier);
```

Register as singleton so all requests within a test share the same notifier instance, allowing tests to read back captured codes.

---

## Testing Strategy

All tests go in the existing [`NuxtAuthControllerTests.cs`](../../tests/NuxtIdentity.AspNetCore.Tests/Controllers/NuxtAuthControllerTests.cs) file, following the existing integration test pattern using `TestWebApplicationFactory` and `HttpClient`.

### New Test Regions

#### ForgotPassword Tests

| Test | Description | Expected Status |
|------|-------------|-----------------|
| `ForgotPassword_ExistingUser_ReturnsSuccess` | Submit username of existing user | 200 OK, `{ success: true }` |
| `ForgotPassword_ExistingUserByEmail_ReturnsSuccess` | Submit email of existing user | 200 OK, `{ success: true }` |
| `ForgotPassword_NonexistentUser_ReturnsSuccess` | Submit nonexistent username | 200 OK (prevents enumeration) |
| `ForgotPassword_EmptyRequest_ReturnsSuccess` | Submit empty username and email | 200 OK (prevents enumeration) |
| `ForgotPassword_ExistingUser_NotifiesUser` | Verify `InMemoryUserNotifier` captures the reset code | 200 OK, notifier has 1 notification |

#### ResetPassword Tests

| Test | Description | Expected Status |
|------|-------------|-----------------|
| `ResetPassword_ValidCode_ReturnsSuccess` | Full flow: create user → forgot-password → extract code from notifier → reset-password | 200 OK |
| `ResetPassword_ValidCode_CanLoginWithNewPassword` | After reset, login with new password succeeds | 200 OK |
| `ResetPassword_ValidCode_OldPasswordFails` | After reset, login with old password fails | 401 Unauthorized |
| `ResetPassword_InvalidCode_ReturnsBadRequest` | Submit invalid reset code | 400 Bad Request |
| `ResetPassword_NonexistentUser_ReturnsBadRequest` | Submit nonexistent username | 400 Bad Request |
| `ResetPassword_WeakPassword_ReturnsBadRequest` | Submit password that doesn't meet requirements | 400 Bad Request |
| `ResetPassword_ValidCode_RevokesRefreshTokens` | After reset, existing refresh tokens are invalid | Refresh returns 401 |

Note: For the weak password test, the `TestWebApplicationFactory` currently disables all password requirements. This test will need either: (a) a separate test factory with stricter password rules, or (b) temporarily enabling password requirements via a custom `IPasswordValidator`. The simplest approach is to create a separate test that uses the existing factory since even with relaxed rules, an empty string password will still fail Identity's minimum length of 1.

Actually, looking at the test factory configuration (line 48: `options.Password.RequiredLength = 1`), an empty password will fail the minimum length check. So the weak password test can use an empty string `""` to trigger the validation error.

#### ChangePassword Tests

| Test | Description | Expected Status |
|------|-------------|-----------------|
| `ChangePassword_ValidCurrentPassword_ReturnsSuccess` | Logged-in user changes password successfully | 200 OK |
| `ChangePassword_ValidChange_CanLoginWithNewPassword` | After change, login with new password succeeds | 200 OK |
| `ChangePassword_ValidChange_OldPasswordFails` | After change, login with old password fails | 401 Unauthorized |
| `ChangePassword_WrongCurrentPassword_ReturnsBadRequest` | Current password is incorrect | 400 Bad Request |
| `ChangePassword_Unauthenticated_ReturnsUnauthorized` | No JWT token provided | 401 Unauthorized |
| `ChangePassword_ValidChange_RevokesRefreshTokens` | After change, existing refresh tokens are invalid | Refresh returns 401 |
| `ChangePassword_WithDeletedUser_ReturnsUnauthorized` | Token valid but user deleted | 401 Unauthorized |
| `ChangePassword_WithTokenMissingUsername_ReturnsUnauthorized` | Token has no name claim | 401 Unauthorized (unit-level test via direct controller call, same pattern as existing `GetSession_WithTokenMissingUsername_ReturnsUnauthorized`) |

### Test Patterns

All tests follow the Gherkin-style Given/When/Then comments per project rules. Example:

```csharp
[Test]
public async Task ResetPassword_ValidCode_ReturnsSuccess()
{
    // Given: An existing user with a password
    var username = "testuser";
    var password = "Test123!";
    var user = new TestUser(username);
    await _userManager.CreateAsync(user, password);

    // And: A password reset code has been generated
    var forgotRequest = new ForgotPasswordRequest { Username = username };
    await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);

    // And: The reset code is retrieved from the test notifier
    var scope = _factory.Services.CreateScope();
    var notifier = scope.ServiceProvider.GetRequiredService<InMemoryUserNotifier<TestUser>>();
    var notifications = await notifier.GetNotificationsAsync();
    var resetCode = notifications.First().Code;

    // When: User resets password with the valid code
    var resetRequest = new ResetPasswordRequest
    {
        Username = username,
        Code = resetCode,
        NewPassword = "NewPassword123!"
    };
    var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

    // Then: 200 OK should be returned with success
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var content = await response.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<JsonElement>(content);
    result.GetProperty("success").GetBoolean().Should().BeTrue();
}
```

### InMemoryUserNotifier Tests

Add a small set of unit tests for `InMemoryUserNotifier` in the Core.Tests project:

**File**: `tests/NuxtIdentity.Core.Tests/Services/InMemoryUserNotifierTests.cs` (new)

| Test | Description |
|------|-------------|
| `SendResetCodeAsync_CapturesNotification` | Verify notification is stored |
| `SendEmailConfirmationAsync_CapturesNotification` | Verify email confirmation notification is stored |
| `GetNotificationsAsync_ReturnsAllNotifications` | Verify multiple notifications are returned |
| `ClearAsync_RemovesAllNotifications` | Verify clear works |

---

## Files to Modify

| File | Type of Change |
|------|----------------|
| `src/Core/Abstractions/IUserNotifier.cs` | New file — interface |
| `src/Core/Models/AuthModels.cs` | Add 3 new request records |
| `src/Core/Models/NotificationRecord.cs` | New file — notification data model |
| `src/Core/Services/InMemoryUserNotifier.cs` | New file — test notifier |
| `src/AspNetCore/Controllers/NuxtAuthControllerBase.cs` | Add 3 endpoints, constructor parameter, helper method, logger messages |
| `tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestAuthController.cs` | Update constructor for new parameter |
| `tests/NuxtIdentity.AspNetCore.Tests/Helpers/TestWebApplicationFactory.cs` | Register `InMemoryUserNotifier` |
| `tests/NuxtIdentity.AspNetCore.Tests/Controllers/NuxtAuthControllerTests.cs` | Add ~16 new integration tests |
| `tests/NuxtIdentity.Core.Tests/Services/InMemoryUserNotifierTests.cs` | New file — 4 unit tests |

## Files NOT Modified

| File | Reason |
|------|--------|
| `src/Core/Abstractions/IRefreshTokenService.cs` | `RevokeAllUserTokensAsync` already exists |
| `src/Core/Services/InMemoryRefreshTokenService.cs` | `RevokeAllUserTokensAsync` already implemented |
| `src/EntityFrameworkCore/Services/EfRefreshTokenService.cs` | `RevokeAllUserTokensAsync` already implemented |
| `src/AspNetCore/Extensions/ServiceCollectionExtensions.cs` | No new DI registrations needed in the library — `IUserNotifier` is registered by the consumer (or test infrastructure) |

---

## Implementation Order

1. Add `IUserNotifier<TUser>` interface to `src/Core/Abstractions/`
2. Add `NotificationRecord` and `NotificationType` to `src/Core/Models/`
3. Add request records (`ForgotPasswordRequest`, `ResetPasswordRequest`, `ChangePasswordRequest`) to `AuthModels.cs`
4. Add `InMemoryUserNotifier<TUser>` to `src/Core/Services/`
5. Add `InMemoryUserNotifier` unit tests and run them
6. Update `NuxtAuthControllerBase` constructor to accept `IEnumerable<IUserNotifier<TUser>>`
7. Add `FindUserByUsernameOrEmailAsync` helper method
8. Add `ForgotPassword` endpoint
9. Add `ResetPassword` endpoint
10. Add `ChangePassword` endpoint
11. Add logger messages
12. Update `TestAuthController` constructor
13. Update `TestWebApplicationFactory` to register notifier
14. Add integration tests and run them
15. Iterate until all tests pass

---

## Design Decisions

### Why IEnumerable&lt;IUserNotifier&gt; instead of nullable parameter?

ASP.NET Core's built-in DI container throws when resolving a service that isn't registered, unless `GetService<T>()` (vs `GetRequiredService<T>()`) is used. For constructor injection, unregistered services cause startup failures. Using `IEnumerable<T>` is the idiomatic way to handle optional dependencies in ASP.NET Core — if no implementation is registered, an empty enumerable is injected. This avoids requiring consumers to register a no-op notifier.

### Why not add IUserNotifier registration to AddNuxtIdentity?

The `IUserNotifier` is intentionally not registered automatically. The consumer is responsible for providing their own implementation (email service, SMS gateway, etc.). Registering a no-op default would silently swallow notifications in production, which is worse than the warning log approach.

### Why InMemoryUserNotifier in Core (not in a test package)?

Placing it in Core allows both the library's own tests and consumer test projects to use it without creating a separate test utilities package. It has no ASP.NET Core dependencies and is a simple in-memory collection wrapper.

### Why return 200 OK always for forgot-password?

This is a security best practice to prevent user enumeration attacks. If the endpoint returned different responses for existing vs. non-existing users, an attacker could probe for valid usernames/emails.

### Why return 400 (not 200) for reset-password with invalid user?

Unlike forgot-password, the reset-password endpoint is called after the user has already received a code. At this point, hiding the user's existence provides no security benefit — the attacker would need a valid code anyway. Returning 400 with a clear error helps legitimate users understand what went wrong.
