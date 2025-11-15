# Refactoring into library

I started the project by bringing up a stand-alone webapi app, so I could get it working. Now I am going to break it out.

Prompt: "I'm going to start refactoring the auth controller and related components out to a reusable library. What are some considerations to keep in mind as I am doing this?"

Great idea! Here are key considerations for refactoring the auth controller into a reusable library:

## Architecture Considerations

### 1. **Separation of Concerns**
- **Core library** - Abstract interfaces and base implementations
- **Specific implementations** - EF Core, Identity, etc. in separate packages
- **Consumer flexibility** - Allow users to swap implementations

### 2. **Dependency Management**
```
NuxtIdentity.Core (minimal dependencies)
├── Abstractions (interfaces, models)
├── Services (base implementations)
└── Extensions (DI helpers)

NuxtIdentity.EntityFrameworkCore (depends on Core + EF)
├── DbContext implementations
└── EF-specific services

NuxtIdentity.Identity (depends on Core + ASP.NET Identity)
└── Identity-specific implementations
```

### 3. **Configuration & Extensibility**

**Use extension methods for clean DI registration:**
```csharp
// In the library
public static class NuxtIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddNuxtIdentity(
        this IServiceCollection services,
        Action<NuxtIdentityOptions>? configure = null)
    {
        // Register core services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, InMemoryRefreshTokenService>();
        
        // Allow customization
        if (configure != null)
        {
            services.Configure(configure);
        }
        
        return services;
    }
    
    public static IServiceCollection AddNuxtIdentityWithEntityFramework(
        this IServiceCollection services,
        Action<NuxtIdentityOptions>? configure = null)
    {
        services.AddNuxtIdentity(configure);
        services.AddScoped<IRefreshTokenService, EfRefreshTokenService>();
        return services;
    }
}

// In Program.cs
builder.Services.AddNuxtIdentityWithEntityFramework(options =>
{
    options.UseDefaultJwtOptions();
});
```

### 4. **Avoid Hard Dependencies**

**Problems in current code:**
- Direct dependency on `ApplicationUser` - make it generic
- Direct dependency on SQLite - abstract data access
- Hardcoded configuration paths

**Solutions:**
```csharp
// Make services generic
public interface IJwtTokenService<TUser> where TUser : class
{
    Task<string> GenerateAccessTokenAsync(TUser user);
}

// Or use a base interface
public interface IAuthUser
{
    string Id { get; }
    string? UserName { get; }
    string? Email { get; }
}

public class ApplicationUser : IdentityUser, IAuthUser
{
    // ...
}
```

### 5. **Models & DTOs**

**Keep request/response models in the library:**
```csharp
// NuxtIdentity.Core/Models/Auth/
public record LoginRequest(string Username, string Password);
public record LoginResponse(TokenPair Token, UserInfo User);
```

**But allow customization:**
```csharp
public interface IUserInfoMapper<TUser>
{
    UserInfo Map(TUser user);
}
```

### 6. **Controller Base Class**

**Provide a base controller:**
```csharp
// In library
public abstract class NuxtIdentityControllerBase<TUser> : ControllerBase
    where TUser : class
{
    protected readonly IJwtTokenService<TUser> JwtTokenService;
    protected readonly IRefreshTokenService RefreshTokenService;
    
    // Implement common logic
    protected async Task<IActionResult> LoginAsync(...)
    {
        // Reusable implementation
    }
}

// In consumer app
[ApiController]
[Route("api/auth")]
public class AuthController : NuxtIdentityControllerBase<ApplicationUser>
{
    // Can override or extend as needed
}
```

### 7. **Configuration Flexibility**

**Don't hardcode configuration sections:**
```csharp
public class NuxtIdentityOptions
{
    public string JwtConfigSection { get; set; } = "Jwt";
    public string ConnectionStringName { get; set; } = "DefaultConnection";
    public Action<IdentityOptions>? ConfigureIdentity { get; set; }
    public Action<JwtBearerOptions>? ConfigureJwtBearer { get; set; }
}
```

### 8. **Logging Abstractions**

**Move LoggerMessage definitions to the library:**
```csharp
// But make them accessible/overridable
public partial class JwtTokenService<TUser>
{
    [LoggerMessage(...)]
    protected virtual partial void LogTokenGenerationStarted(string username);
}
```

### 9. **Testing Considerations**

**Design for testability:**
- All services behind interfaces
- Mock-friendly dependencies
- In-memory implementations for testing

**Include test helpers:**
```csharp
// NuxtIdentity.Testing
public class TestAuthHelpers
{
    public static Mock<IJwtTokenService> CreateMockJwtService()
    {
        // Pre-configured mocks
    }
}
```

### 10. **Versioning & Breaking Changes**

**Plan for evolution:**
- Use semantic versioning
- Mark obsolete features clearly
- Provide migration guides
- Consider multi-targeting (.NET 8, .NET 9, etc.)

### 11. **Documentation**

**Essential docs:**
- README with quick start
- Sample projects
- API documentation (XML comments)
- Migration guide from playground to library
- Security best practices

### 12. **Package Structure**

```
NuxtIdentity/
├── src/
│   ├── NuxtIdentity.Core/
│   ├── NuxtIdentity.EntityFrameworkCore/
│   ├── NuxtIdentity.AspNetIdentity/
│   └── NuxtIdentity.Testing/
├── samples/
│   ├── BasicAuth/
│   ├── WithIdentity/
│   └── CustomImplementation/
└── tests/
    ├── NuxtIdentity.Core.Tests/
    └── NuxtIdentity.Integration.Tests/
```

### 13. **Security Considerations**

**Don't ship with defaults that are insecure:**
- No default JWT keys
- Require explicit configuration
- Validate configuration on startup
- Document security best practices

```csharp
public static class NuxtIdentityOptionsValidator
{
    public static void Validate(NuxtIdentityOptions options)
    {
        if (options.JwtKey.Length < 32)
            throw new InvalidOperationException("JWT key must be at least 32 characters");
    }
}
```

### 14. **Feature Flags**

**Allow optional features:**
```csharp
builder.Services.AddNuxtIdentity(options =>
{
    options.EnableRefreshTokenRotation = true;
    options.EnableTokenRevocation = true;
    options.EnableSwaggerAuth = true;
    options.RequireEmailVerification = false;
});
```

### 15. **Minimal API Support**

**Consider supporting both controllers and minimal APIs:**
```csharp
// Extension for minimal API
public static class NuxtIdentityEndpointExtensions
{
    public static IEndpointRouteBuilder MapNuxtIdentityEndpoints(
        this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/api/auth/login", async (LoginRequest req) => { });
        return builder;
    }
}
```

Would you like me to help you create the initial library structure or focus on any specific aspect?