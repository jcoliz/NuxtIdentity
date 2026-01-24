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
    "RefreshTokenLifespan": "30.00:00:00"
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
using NuxtIdentity.EntityFrameworkCore;

namespace MyApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureNuxtIdentityRefreshTokens();
    }
}
```

## Step 5: Configure NuxtIdentity in Program.cs

Add these NuxtIdentity-specific configuration lines to your `Program.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using NuxtIdentity.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ⭐ Add NuxtIdentity services (JWT tokens, refresh tokens, authentication)
builder.Services.AddNuxtIdentity<ApplicationDbContext>(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

// ⭐ Add authentication middleware (must come before authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

The `AddNuxtIdentity` extension method automatically configures:
- JWT token generation and validation
- Refresh token storage and rotation
- JWT Bearer authentication
- User claims providers

## Step 6: Implement your Auth Controller

Create a new file `Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.Controllers;
using NuxtIdentity.Services;

namespace MyApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : NuxtIdentityController
{
    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IRefreshTokenService refreshTokenService,
        ILogger<AuthController> logger)
        : base(userManager, signInManager, refreshTokenService, logger)
    {
    }
}
```

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

```bash
POST https://localhost:5001/api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "confirmPassword": "Password123!"
}
```

### Login

```bash
POST https://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}
```

You'll receive a response with `accessToken` and `refreshToken`.

### Refresh the token

```bash
POST https://localhost:5001/api/auth/refresh
Authorization: Bearer your-access-token-here
Content-Type: application/json
{
  "refreshToken": "your-refresh-token-here"
}
```

### Logout

```bash
POST https://localhost:5001/api/auth/logout
Authorization: Bearer your-access-token-here
Content-Type: application/json

{
  "refreshToken": "your-refresh-token-here"
}
```

## Next Steps

- **Customize Identity User**: Extend `IdentityUser` with custom properties
- **Add Authorization**: Use `[Authorize]` attribute on your controllers
- **Configure CORS**: Enable CORS for your frontend application
- **Use User Secrets**: Store sensitive configuration in development
- **Add Email Confirmation**: Implement email verification for new users
- **Add Password Reset**: Implement password reset functionality

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
- Check that `JwtOptions:SecretKey` is at least 32 characters long
- Verify `Issuer` and `Audience` match between configuration and requests

### Identity validation errors
- Review password requirements in `Program.cs`
- Check that email format is valid

For more information, see the [API Reference](./API-REFERENCE.md) and the `local` sample project.
