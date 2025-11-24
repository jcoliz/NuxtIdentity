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
  "JwtOptions": {
    "Issuer": "https://localhost:5001",
    "Audience": "https://localhost:5001",
    "SecretKey": "your-base64-secret-key-min-32-characters-long-change-in-production=="
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

**Important:** Generate a strong secret key for production use and store it securely (e.g., in Azure Key Vault or user secrets).

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

## Step 5: Set up ASP.NET Core Identity and JWT Authentication

In `Program.cs`, configure services:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using NuxtIdentity.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add NuxtIdentity services
builder.Services.AddNuxtIdentity<ApplicationDbContext>(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

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
