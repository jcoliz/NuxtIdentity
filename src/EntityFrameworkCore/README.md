# NuxtIdentity.EntityFrameworkCore

Entity Framework Core integration for NuxtIdentity refresh token storage.

## Overview

This library provides a production-ready implementation of `IRefreshTokenService` using Entity Framework Core for persistent storage of refresh tokens.

## Features

- **Generic DbContext**: Works with any `DbContext` via `TContext` type parameter
- **Secure Storage**: Tokens are hashed using SHA256 before storage
- **Token Rotation**: Automatic revocation of old tokens when refreshing
- **Configurable Expiration**: Default 30-day token lifetime
- **Structured Logging**: LoggerMessage support throughout
- **Easy Configuration**: ModelBuilder extension for entity setup

## Components

### Services

- **`EfRefreshTokenService<TContext>`** - EF Core implementation of `IRefreshTokenService`
  - Generates cryptographically secure refresh tokens
  - Stores hashed tokens (never stores plaintext)
  - Validates tokens against database
  - Supports token revocation (single token or all user tokens)

### Extensions

- **`ConfigureNuxtIdentityRefreshTokens()`** - ModelBuilder extension to configure `RefreshTokenEntity`
- **`AddNuxtIdentityEntityFramework<TContext>()`** - Service registration extension

## Installation

```bash
dotnet add package NuxtIdentity.EntityFrameworkCore
dotnet add package NuxtIdentity.Core
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite  # Or your preferred provider
```

## Usage

### 1. Configure Your DbContext

```csharp
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    // Add DbSet for refresh tokens
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Configure refresh token entity
        builder.ConfigureNuxtIdentityRefreshTokens();
    }
}
```

### 2. Add DbContext and Services

```csharp
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.EntityFrameworkCore.Extensions;

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add NuxtIdentity EF services
builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
```

### 3. Create Migration

```bash
dotnet ef migrations add AddRefreshTokens
dotnet ef database update
```

## What Gets Created

The `RefreshTokenEntity` table includes:

- **`Id`** - Primary key (GUID)
- **`TokenHash`** - SHA256 hash of the token (indexed)
- **`UserId`** - User identifier (indexed)
- **`ExpiresAt`** - Token expiration timestamp
- **`CreatedAt`** - Token creation timestamp
- **`IsRevoked`** - Revocation flag

## Security Features

### Token Hashing

Tokens are never stored in plaintext:

```csharp
// What you receive
var refreshToken = "base64-encoded-random-bytes";

// What gets stored in the database
var tokenHash = SHA256.Hash(refreshToken);  // Only the hash is stored
```

This ensures that even if the database is compromised, tokens cannot be extracted.

### Token Rotation

When refreshing tokens, old tokens are automatically revoked:

```csharp
// This happens automatically in NuxtAuthControllerBase
var newAccessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
var newRefreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(userId);

// Old token is marked as revoked
await RefreshTokenService.RevokeRefreshTokenAsync(oldRefreshToken);
```

## Advanced Usage

### Revoke All User Tokens

Useful for logout from all devices:

```csharp
await _refreshTokenService.RevokeAllUserTokensAsync(userId);
```

### Custom Token Expiration

The default expiration is 30 days. To customize, you can inherit from `EfRefreshTokenService<TContext>`:

```csharp
public class CustomRefreshTokenService<TContext> : EfRefreshTokenService<TContext>
    where TContext : DbContext
{
    private const int CustomExpirationDays = 90;
    
    // Override and customize as needed
}
```

## Integration with Identity

Works seamlessly with `IdentityDbContext`:

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);  // Call base for Identity tables
        
        builder.ConfigureNuxtIdentityRefreshTokens();  // Add refresh tokens
    }
}
```

## Database Provider Support

Works with any EF Core provider:

- **SQLite** - `Microsoft.EntityFrameworkCore.Sqlite`
- **SQL Server** - `Microsoft.EntityFrameworkCore.SqlServer`
- **PostgreSQL** - `Npgsql.EntityFrameworkCore.PostgreSQL`
- **MySQL** - `Pomelo.EntityFrameworkCore.MySql`
- **In-Memory** - `Microsoft.EntityFrameworkCore.InMemory` (testing only)

## Performance Considerations

The library automatically creates indexes on:
- `TokenHash` - For fast token validation lookups
- `UserId` - For fast user-based queries (e.g., revoke all tokens)

## Logging

The service includes structured logging for:
- Token generation (Info level)
- Token validation (Debug level)
- Token revocation (Info level)
- Errors (Error level)

## Next Steps

For ASP.NET Core integration, see:
- **NuxtIdentity.AspNetCore** - Base controllers, authentication setup, Identity integration

For a complete example, see:
- **Playground** project in the repository

## License

[Your License Here]