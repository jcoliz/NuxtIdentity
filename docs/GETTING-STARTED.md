# How to integrate NuxtIdentity into a new application

This guide will walk you through integrating NuxtIdentity into a new .NET Web API project, taking you from `dotnet new webapi` to a fully functional authentication system similar to the `local` sample.

## Prerequisites

- .NET 8.0 SDK or later
- A SQL Server, PostgreSQL, or SQLite database (this guide uses SQLite for simplicity)

## Step 1: Create a new .NET Web API project

```bash
dotnet new webapi -n MyApp
cd MyApp
```

## Step 2: Add NuxtIdentity package references

Add the NuxtIdentity.EntityFrameworkCore package to your project:

```bash
dotnet add package NuxtIdentity.EntityFrameworkCore
```

If using SQLite, also add:

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

## Step 3: Configure JWT Options

In `appsettings.json`, add JWT configuration:

```json
{
  "Jwt": {
    "Key": "your-base64-secret-key-min-32-bytes-long-change-in-production==",
    "Issuer": "https://localhost:5001",
    "Audience": "https://localhost:5001",
    "Lifespan": "01:00:00",
    "RefreshTokenLifespan": "30.00:00:00",
    "ClockSkew": "00:00:30"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

### JWT Configuration Options

- **`Key`** (required): Base64-encoded secret key for signing tokens. Must be at least 32 bytes (256 bits) for HMAC-SHA256 security.
- **`Issuer`** (required): Identifies who issued the token (e.g., your app name or URL).
- **`Audience`** (required): Identifies who the token is intended for (e.g., your API URL).
- **`Lifespan`** (optional): How long access tokens remain valid. Format: "HH:MM:SS" or "D.HH:MM:SS". Default: 1 hour.
- **`RefreshTokenLifespan`** (optional): How long refresh tokens remain valid. Format: "D.HH:MM:SS". Default: 30 days.
- **`ClockSkew`** (optional): Allowed clock drift for token lifetime validation. Must be between 0 and 5 minutes. Default: 30 seconds.

### Generating a Secure Key

**PowerShell:**
```powershell
$bytes = [byte[]]::new(32)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

**Bash (Linux/macOS):**
```bash
openssl rand -base64 32
```

**Important:** Never commit secrets to source control. Use user secrets for development and secure vaults (Azure Key Vault, AWS Secrets Manager) for production.

## Step 4: Create your Application DbContext

Create a new file `Data/ApplicationDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace MyApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureNuxtIdentityRefreshTokens();
        builder.ConfigureNuxtIdentityInvitations();
    }
}
```

## Step 5: Configure NuxtIdentity in Program.cs

Add these NuxtIdentity-specific configuration lines to your `Program.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using NuxtIdentity.EntityFrameworkCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ⭐ Add NuxtIdentity services (JWT tokens, refresh tokens, invitations, authentication)
builder.Services.AddNuxtIdentityWithEntityFramework<IdentityUser, ApplicationDbContext>(
    builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

// ⭐ Add authentication middleware (must come before authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

The `AddNuxtIdentityWithEntityFramework` extension method automatically configures:
- JWT token generation and validation
- Refresh token storage and rotation (Entity Framework Core)
- Invitation management (Entity Framework Core)
- JWT Bearer authentication
- User claims providers

## Step 6: Implement your Auth Controller

Create a new file `Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;

namespace MyApp.Controllers;

public class AuthController(
    IJwtTokenService<IdentityUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<IdentityUser>> userClaimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IEnumerable<IUserNotifier<IdentityUser>> userNotifiers,
    IEnumerable<IInvitationService> invitationServices,
    ILogger<AuthController> logger)
    : NuxtAuthControllerBase<IdentityUser>(
        jwtTokenService,
        userClaimsProviders,
        refreshTokenService,
        userManager,
        signInManager,
        userNotifiers,
        invitationServices,
        logger)
{
    // No additional implementation needed; all functionality is in the base class.
}
```

The base controller uses primary constructor injection and requires all eight parameters. Services like `IUserNotifier<TUser>` and `IInvitationService` are injected as `IEnumerable<>` so they are optional — the controller works without any registered implementations, and throws `NuxtIdentityConfigurationException` only when an endpoint that requires them is actually called.

## Step 7: Create and apply database migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Step 8: Test your API

Run your application:

```bash
dotnet run
```

### Register a new user

```http
POST https://localhost:5001/api/auth/signup
Content-Type: application/json

{
  "username": "newuser",
  "email": "user@example.com",
  "password": "Password123!"
}
```

### Login

```http
POST https://localhost:5001/api/auth/login
Content-Type: application/json

{
  "username": "newuser",
  "password": "Password123!"
}
```

You'll receive a response with `token` (containing `accessToken` and `refreshToken`) and `user` information.

### Refresh the token

```http
POST https://localhost:5001/api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "your-refresh-token-here"
}
```

Note: The refresh endpoint validates the refresh token directly — no JWT Bearer authorization header is needed.

### Logout

```http
POST https://localhost:5001/api/auth/logout
Content-Type: application/json

{
  "refreshToken": "your-refresh-token-here"
}
```

## Step 9: Add Password Management (Optional)

NuxtIdentity includes built-in password management endpoints: forgot-password, reset-password, and change-password. These wrap ASP.NET Core Identity's token-based password reset and require no database changes.

### Implement IUserNotifier

The forgot-password flow requires at least one `IUserNotifier<TUser>` implementation to deliver reset codes to users. NuxtIdentity provides the reset token — you decide how to deliver it (email, SMS, etc.). Multiple notifiers can be registered and all will be called:

```csharp
using NuxtIdentity.Core.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace MyApp.Services;

public class EmailUserNotifier : IUserNotifier<IdentityUser>
{
    private readonly IEmailSender _emailSender;

    public EmailUserNotifier(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task SendResetCodeAsync(IdentityUser user, string resetCode)
    {
        // Build your reset URL and email body
        var resetUrl = $"https://myapp.com/reset-password?code={Uri.EscapeDataString(resetCode)}&email={user.Email}";
        await _emailSender.SendAsync(user.Email!, "Password Reset", $"Reset your password: {resetUrl}");
    }

    public Task SendEmailConfirmationAsync(IdentityUser user, string confirmationCode)
    {
        // Reserved for future email confirmation feature
        return Task.CompletedTask;
    }
}
```

### Register in DI

Add the notifier registration in `Program.cs`:

```csharp
// ⭐ Register your notifier for password reset delivery
builder.Services.AddSingleton<IUserNotifier<IdentityUser>, EmailUserNotifier>();
```

### Available Endpoints

Once registered, the following endpoints are available automatically:

| Endpoint | Method | Auth Required | Description |
|---|---|---|---|
| `/api/auth/forgot-password` | POST | No | Generates a reset code and calls all registered `IUserNotifier` implementations |
| `/api/auth/reset-password` | POST | No | Validates the reset code and sets a new password |
| `/api/auth/change-password` | POST | Yes (JWT) | Changes password for the authenticated user |

**Important:** If no `IUserNotifier` is registered, calling `forgot-password` will throw a `NuxtIdentityConfigurationException`. The reset-password and change-password endpoints work without a notifier.

### Security Notes

- `forgot-password` always returns `204 No Content` regardless of whether the user exists (prevents user enumeration)
- Both `reset-password` and `change-password` revoke all existing refresh tokens after success — the client should log the user out and prompt re-authentication

## Step 10: Add Invitation-Based Registration (Optional)

NuxtIdentity supports invitation-based registration where users can only sign up with a valid invitation code. Invitations can carry pre-assigned roles and claims that are automatically applied to the new user.

### How It Works

By default, the `SignUp` endpoint uses **open registration** — anyone can register without an invitation. To restrict registration to invitation holders, override `RegistrationOptions` in your auth controller:

```csharp
    // ⭐ Require invitation codes for all registrations
    protected override RegistrationOptions RegistrationOptions => new(RegistrationMode.InvitationOnly);
```

### Registration Modes

| Mode | Behavior |
|---|---|
| `RegistrationMode.Open` (default) | Anyone can register. If an invitation code is provided, it's validated and roles/claims are assigned. |
| `RegistrationMode.InvitationOnly` | An invitation code is required. Signup without a code returns `403 Forbidden`. |
| `RegistrationMode.EmailConfirmation` | Not yet implemented (Phase 3). Setting this mode throws `NotImplementedException`. |

### Managing Invitations

The `IInvitationService` is registered automatically by `AddNuxtIdentityWithEntityFramework`. To create invitations, inject `IInvitationService` into your own admin controller:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace MyApp.Controllers;

[ApiController]
[Route("api/admin/invitations")]
[Authorize(Roles = "Admin")]
public class InvitationController(IInvitationService invitationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest request)
    {
        var invitation = await invitationService.CreateAsync(
            email: request.Email,
            roles: request.Roles,
            claims: request.Claims,
            expiresIn: TimeSpan.FromDays(7)
        );

        return Ok(new { code = invitation.Code, expiresAt = invitation.ExpiresAt });
    }
}

public record CreateInvitationRequest(
    string? Email = null,
    List<string>? Roles = null,
    List<ClaimInfo>? Claims = null
);
```

### Signing Up with an Invitation

Clients include the invitation code in the signup request:

```http
POST https://localhost:5001/api/auth/signup
Content-Type: application/json

{
  "username": "inviteduser",
  "email": "invited@example.com",
  "password": "Password123!",
  "invitationCode": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

When an invitation code is provided:
- The invitation is validated (must be pending and not expired)
- Roles and claims from the invitation are assigned to the new user
- The user's email is auto-confirmed
- The invitation is marked as accepted

### Validating an Invitation (Frontend)

The frontend can check an invitation's status before showing the registration form:

```http
GET https://localhost:5001/api/auth/invitations/a1b2c3d4-e5f6-7890-abcd-ef1234567890/status
```

Returns:
```json
{
  "status": "Pending",
  "email": "invited@example.com"
}
```

The `email` field is only returned for `Pending` invitations (to pre-fill the registration form). For all other statuses (`Accepted`, `Expired`, `Revoked`, `NotFound`), `email` is null to prevent information leakage.

### Invitation Lifecycle Hooks

Override `OnInvitationAcceptedAsync` in your auth controller for custom post-acceptance logic:

```csharp
protected override async Task OnInvitationAcceptedAsync(
    IdentityUser user, InvitationEntity invitation)
{
    // Custom logic after invitation acceptance
    // e.g., send welcome email, create default resources, etc.
}
```

## Next Steps

- **Customize Identity User**: Extend `IdentityUser` with custom properties
- **Add Authorization**: Use `[Authorize]` attribute on your controllers
- **Configure CORS**: Enable CORS for your frontend application
- **Use User Secrets**: Store sensitive configuration in development
- **Add Custom Claims Providers**: Implement `IUserClaimsProvider<TUser>` for additional claims
- **Override Endpoints**: All base controller methods are virtual — override for custom behavior

## Production Considerations

1. **Never commit secrets**: Use Azure Key Vault, AWS Secrets Manager, or environment variables
2. **Use HTTPS**: Ensure all communications are encrypted
3. **Configure CORS properly**: Only allow trusted origins
4. **Set up rate limiting**: Protect against brute force attacks
5. **Use a production database**: Replace SQLite with SQL Server, PostgreSQL, or similar
6. **Enable logging and monitoring**: Track authentication events
7. **Regular security updates**: Keep all packages up to date

## Troubleshooting

### Database connection issues
- Verify your connection string in `appsettings.json`
- Ensure migrations have been applied: `dotnet ef database update`

### JWT validation errors
- Check that `Jwt:Key` is a valid Base64-encoded value of at least 32 bytes
- Verify `Issuer` and `Audience` match between configuration and requests
- Check that `ClockSkew` is between 0 and 5 minutes

### Identity validation errors
- Review password requirements in `Program.cs`
- Check that email format is valid

### Invitation errors
- Ensure `IInvitationService` is registered (included automatically with `AddNuxtIdentityWithEntityFramework`)
- Verify that `InvitationEntity` is configured in your DbContext's `OnModelCreating`
- Check invitation status — codes can only be used once and expire after the configured duration

For more information, see the [API Reference](./API-REFERENCE.md) and the `local` sample project.
