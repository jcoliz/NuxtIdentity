
# Key Implementation Details

## Custom User Model

```csharp
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    // Subscriptions stored as Identity claims, not properties
}
```

## Auth Controller

```csharp
public class AuthController : NuxtAuthControllerBase<ApplicationUser>
{
    // Implement required abstract method
    protected override async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }
    
    // Implement login endpoint
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Authenticate with Identity, generate tokens using helper methods
    }
    
    // Refresh and Logout are inherited from base controller!
}
```

## Admin Controller

```csharp
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    [HttpPost("setrole")]
    public async Task<IActionResult> SetRole([FromBody] SetRoleRequest request)
    {
        // Change user roles
    }
    
    [HttpPost("setsubscriptions")]
    public async Task<IActionResult> SetSubscriptions([FromBody] SetSubscriptionsRequest request)
    {
        // Manage user subscriptions via Identity claims
    }
}
```

## Custom Authorization Policy

```csharp
// Requirement and Handler
public class SubscriptionRequirement : IAuthorizationRequirement { }
public class SubscriptionHandler : AuthorizationHandler<SubscriptionRequirement> { }

// Registration
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireActiveSubscription", policy =>
        policy.Requirements.Add(new SubscriptionRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, SubscriptionHandler>();

// Usage
[Authorize(Policy = "RequireActiveSubscription")]
public class WeatherForecastController : ControllerBase { }
```

## Complete Setup (Program.cs)

```csharp
// Configure JWT
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// Add DbContext and Identity
builder.Services.AddDbContext<ApplicationDbContext>(...);
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(...)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add NuxtIdentity - Three simple calls!
builder.Services.AddNuxtIdentity<ApplicationUser>();
builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
builder.Services.AddNuxtIdentityAuthentication();

// Add custom authorization
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization(options => { /* policies */ });
builder.Services.AddScoped<IAuthorizationHandler, SubscriptionHandler>();
```
