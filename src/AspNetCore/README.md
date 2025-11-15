# NuxtIdentity.AspNetCore

ASP.NET Core integration for NuxtIdentity, including base controllers, authentication setup, and ASP.NET Core Identity support.

## Overview

This library provides ready-to-use ASP.NET Core components for JWT authentication, including a base controller with common endpoints and seamless integration with ASP.NET Core Identity.

## Features

- **Base Controller**: Generic `NuxtAuthControllerBase<TUser>` with virtual refresh and logout endpoints
- **Identity Integration**: Ready-made claims provider for ASP.NET Core Identity
- **JWT Bearer Setup**: Automatic configuration of JWT Bearer authentication
- **Helper Methods**: Token generation, user claim extraction, and more
- **Structured Logging**: LoggerMessage support throughout

## Components

### Controllers

- **`NuxtAuthControllerBase<TUser>`** - Abstract base controller providing:
  - Virtual `RefreshTokens()` endpoint with token rotation
  - Virtual `Logout()` endpoint with token revocation
  - Helper methods: `CreateLoginResponseAsync()`, `CreateRefreshResponseAsync()`
  - User claim helpers: `GetCurrentUserId()`, `GetCurrentUsername()`
  - Abstract `GetUserByIdAsync()` for derived classes to implement

### Services

- **`IdentityUserClaimsProvider<TUser>`** - Claims provider for ASP.NET Core Identity users
  - Extracts standard claims (NameIdentifier, Name, Email, Sub, Jti)
  - Loads user roles from Identity
  - Works with any `IdentityUser`-derived type

### Configuration

- **`JwtBearerOptionsSetup`** - Configures JWT Bearer authentication from `JwtOptions`

### Extensions

- **`AddNuxtIdentity<TUser>()`** - Registers JWT token service and claims provider
- **`AddNuxtIdentityAuthentication()`** - Configures JWT Bearer authentication

## Installation

```bash
dotnet add package NuxtIdentity.AspNetCore
dotnet add package NuxtIdentity.Core
```

## Usage

### 1. Configure JWT Options

In `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-32-characters-long",
    "Issuer": "your-app-name",
    "Audience": "your-app-users",
    "ExpirationMinutes": 60
  }
}
```

### 2. Set Up ASP.NET Core Identity

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

### 3. Add NuxtIdentity Services

```csharp
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Configuration;

// Configure JWT options
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// Add NuxtIdentity
builder.Services.AddNuxtIdentity<ApplicationUser>();
builder.Services.AddNuxtIdentityAuthentication();
```

### 4. Create Your Auth Controller

Inherit from `NuxtAuthControllerBase<TUser>` and implement your login/signup logic:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

public class AuthController : NuxtAuthControllerBase<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    
    public AuthController(
        IJwtTokenService<ApplicationUser> jwtTokenService,
        IRefreshTokenService refreshTokenService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
        : base(jwtTokenService, refreshTokenService, logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }
    
    // Implement the required abstract method
    protected override async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }
    
    // Implement your login endpoint
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user == null)
            return Unauthorized(new { message = "Invalid credentials" });
        
        var result = await _signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: false);
            
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid credentials" });
        
        var userInfo = new UserInfo
        {
            Id = user.Id,
            Name = user.UserName ?? "",
            Email = user.Email ?? ""
        };
        
        var response = await CreateLoginResponseAsync(user, user.Id, userInfo);
        return Ok(response);
    }
    
    // Refresh and Logout endpoints are inherited from base class!
}
```

### 5. Configure the Pipeline

```csharp
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

## What You Get

By inheriting from `NuxtAuthControllerBase<TUser>`, you automatically get:

### ✅ Virtual Endpoints (Override if needed)

- **`POST /api/auth/refresh`** - Refresh access token with rotation
- **`POST /api/auth/logout`** - Revoke refresh token

### ✅ Helper Methods

- `CreateLoginResponseAsync()` - Generate login response with tokens
- `CreateRefreshResponseAsync()` - Generate refresh response with new tokens
- `GetCurrentUserId()` - Extract user ID from claims
- `GetCurrentUsername()` - Extract username from claims

### ✅ Protected Services

- `JwtTokenService` - Access to JWT token generation
- `RefreshTokenService` - Access to refresh token management
- `Logger` - Structured logging

## Design Rationale

### Why a Base Controller?

The base controller provides:
1. **Common Infrastructure** - Refresh and logout work the same everywhere
2. **Best Practices** - Token rotation, proper error handling, logging
3. **Flexibility** - Override any endpoint or add custom ones
4. **Type Safety** - Generic `TUser` works with any user type

### What You Implement

You implement the **app-specific** logic:
- Login (username/password, email, social, etc.)
- Signup (with or without email verification)
- User session retrieval
- Custom endpoints

This separation keeps the library generic while allowing full customization.

## Without ASP.NET Core Identity?

If you're not using Identity, you can:
1. Implement your own `IUserClaimsProvider<TUser>`
2. Still use `AddNuxtIdentityAuthentication()` for JWT setup
3. Manually register your claims provider:

```csharp
builder.Services.AddScoped<IUserClaimsProvider<MyUser>, MyCustomClaimsProvider>();
builder.Services.AddScoped<IJwtTokenService<MyUser>, JwtTokenService<MyUser>>();
builder.Services.AddNuxtIdentityAuthentication();
```

## Next Steps

For Entity Framework Core storage, see:
- **NuxtIdentity.EntityFrameworkCore** - EF Core refresh token storage

For a complete example, see:
- **Playground** project in the repository

## License

TBD