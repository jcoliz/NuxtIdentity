# NuxtIdentity.Core

Generic JWT authentication library with no dependencies on ASP.NET Core Identity or Entity Framework.

## Overview

This library provides the core abstractions and implementations for JWT-based authentication with refresh token support. It's designed to be technology-agnostic and can work with any user management system.

## Features

- **Generic Design**: Works with any user type via `TUser` type parameter
- **JWT Token Generation**: Create access tokens with configurable claims
- **Refresh Token Management**: Secure token rotation pattern
- **Extensible Claims Provider**: Plug in any claims extraction logic
- **Minimal Dependencies**: Only requires JWT and Options libraries

## Components

### Interfaces

- **`IJwtTokenService<TUser>`** - JWT token generation and validation
- **`IRefreshTokenService`** - Refresh token storage and validation
- **`IUserClaimsProvider<TUser>`** - Extract claims from user objects

### Implementations

- **`JwtTokenService<TUser>`** - Complete JWT token service with logging
- **`InMemoryRefreshTokenService`** - In-memory token storage for testing/development

### Models

- **`JwtOptions`** - JWT configuration (secret key, issuer, audience, expiration)
- **`RefreshTokenEntity`** - Refresh token entity for database storage
- **`AuthModels`** - Request/Response DTOs:
  - `LoginRequest`, `LoginResponse`
  - `RefreshRequest`, `RefreshResponse`
  - `SessionResponse`, `TokenPair`, `UserInfo`

## Installation

```bash
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

In `Program.cs`:

```csharp
using NuxtIdentity.Core.Configuration;

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
```

### 2. Implement Claims Provider

```csharp
using NuxtIdentity.Core.Abstractions;

public class MyUserClaimsProvider : IUserClaimsProvider<MyUser>
{
    public Task<IEnumerable<Claim>> GetClaimsAsync(MyUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            // Add custom claims
        };
        
        return Task.FromResult<IEnumerable<Claim>>(claims);
    }
}
```

### 3. Register Services

```csharp
using NuxtIdentity.Core.Services;

builder.Services.AddScoped<IUserClaimsProvider<MyUser>, MyUserClaimsProvider>();
builder.Services.AddScoped<IJwtTokenService<MyUser>, JwtTokenService<MyUser>>();
builder.Services.AddScoped<IRefreshTokenService, InMemoryRefreshTokenService>();
```

### 4. Generate Tokens

```csharp
public class AuthService
{
    private readonly IJwtTokenService<MyUser> _jwtService;
    private readonly IRefreshTokenService _refreshService;
    
    public async Task<LoginResponse> LoginAsync(MyUser user)
    {
        var accessToken = await _jwtService.GenerateAccessTokenAsync(user);
        var refreshToken = await _refreshService.GenerateRefreshTokenAsync(user.Id);
        
        return new LoginResponse
        {
            Token = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            },
            User = new UserInfo
            {
                Id = user.Id,
                Name = user.Username,
                Email = user.Email
            }
        };
    }
}
```

## Design Philosophy

### Technology Independence

This library doesn't depend on:
- ASP.NET Core Identity (use any user management system)
- Entity Framework Core (use any data store)
- Specific database providers

### Generic Types

All services use `TUser` type parameter, allowing you to work with:
- Custom user classes
- Identity's `IdentityUser`
- Third-party user models
- Any class that represents a user

### Claims-Based Architecture

The `IUserClaimsProvider<TUser>` abstraction separates claim extraction from token generation, allowing:
- Different claim strategies per user type
- Easy testing and mocking
- Integration with various auth systems

## Next Steps

For ASP.NET Core integration, see:
- **NuxtIdentity.AspNetCore** - Base controllers, authentication setup, Identity integration

For Entity Framework Core storage, see:
- **NuxtIdentity.EntityFrameworkCore** - EF Core refresh token storage

For a complete example, see:
- **Playground** project in the repository

## License

TBD