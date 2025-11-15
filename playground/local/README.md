# NuxtIdentity Playground

Reference implementation demonstrating how to use the NuxtIdentity libraries to build a complete JWT authentication system with ASP.NET Core Identity and Entity Framework Core.

## Overview

This playground application shows best practices for integrating all three NuxtIdentity libraries:
- **NuxtIdentity.Core** - Generic JWT and refresh token services
- **NuxtIdentity.AspNetCore** - Base controller and Identity integration
- **NuxtIdentity.EntityFrameworkCore** - Persistent refresh token storage

## What's Included

### Authentication

- ✅ **Login** - Username/password authentication via ASP.NET Core Identity
- ✅ **Refresh** - Token refresh with automatic rotation (inherited from base controller)
- ✅ **Logout** - Token revocation (inherited from base controller)
- ✅ **Session** - Get current user information

### Infrastructure

- ✅ SQLite database with EF Core
- ✅ ASP.NET Core Identity (Users, Roles)
- ✅ Refresh token storage with token rotation
- ✅ NSwag/Swagger UI with JWT support
- ✅ CORS configured for Nuxt.js frontend

## Running the Playground

### Standalone API Testing

1. **Build and Run**
   ```bash
   cd playground/local
   dotnet run
   ```

2. **Access Swagger UI**  
   Navigate to `https://localhost:5001/swagger`

3. **Test Endpoints**
   - Login, get tokens
   - Use "Authorize" button to add token
   - Test authorized endpoints
   - Refresh tokens
   - Logout

## Using with @sidebase/nuxt-auth Frontend

This sample is designed to work with the [@sidebase/nuxt-auth](https://nuxt.com/modules/sidebase-auth) local playground.

### 1. Start the .NET Backend

```bash
cd playground/local
dotnet run
```

The backend will listen on `http://localhost:3001/` for auth requests.

### 2. Clone and Setup nuxt-auth

In a separate terminal:

```bash
git clone https://github.com/sidebase/nuxt-auth
cd nuxt-auth/playground-local
pnpm i
```

### 3. Configure the Nuxt App

Update `playground-local/nuxt.config.ts`:

```diff
--- a/playground-local/nuxt.config.ts
+++ b/playground-local/nuxt.config.ts
@@ -5,6 +5,7 @@ export default defineNuxtConfig({
     transpile: ['jsonwebtoken']
   },
   auth: {
+    baseURL: 'http://localhost:3001/api/auth',
     provider: {
       type: 'local',
       endpoints: {
@@ -26,6 +27,7 @@ export default defineNuxtConfig({
         // We do an environment variable for E2E testing both options.    
         isEnabled: process.env.NUXT_AUTH_REFRESH_ENABLED !== 'false',     
         endpoint: { path: '/refresh', method: 'post' },
+        refreshOnlyToken: false, // rotate refresh tokens!
         token: {
           signInResponseRefreshTokenPointer: '/token/refreshToken',       
           refreshResponseTokenPointer: '',
```

### 4. Start the Nuxt Frontend

```bash
pnpm generate
pnpm start
```

The frontend will be available at `http://localhost:3000/`

### 5. Register and Login

1. Visit `http://localhost:3000/register`
2. Enter a username and password, click "sign up"
3. Click "navigate to login page"
4. Enter the same credentials to log in

The Nuxt app will automatically handle token refresh and rotation!

## Configuration

See `appsettings.json` for JWT settings, connection strings, and CORS configuration.

## What to Learn From This

This playground demonstrates:
- Minimal setup with maximum functionality
- Token rotation best practices
- Integration with ASP.NET Core Identity
- CORS configuration for frontend apps
- Working with @sidebase/nuxt-auth

## Next Steps

- See library READMEs for detailed API documentation
- Customize the user model for your needs
- Add signup, password reset, email verification
- Deploy with proper production configuration

## Key Implementation Details

### Custom User Model

```csharp
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
```

### Auth Controller

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

### Complete Setup (Program.cs)

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
```
