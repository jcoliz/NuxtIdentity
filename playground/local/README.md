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
- ✅ **SignUp** - User registration with automatic 'guest' role assignment
- ✅ **Refresh** - Token refresh with automatic rotation (inherited from base controller)
- ✅ **Logout** - Token revocation (inherited from base controller)
- ✅ **Session** - Get current user information including roles and subscriptions

### Authorization

- ✅ **Role-Based Access Control** - Three roles: guest, account, admin
- ✅ **Subscription-Based Access** - Custom authorization using Identity claims
- ✅ **Admin Endpoints** - User and subscription management

### Infrastructure

- ✅ SQLite database with EF Core
- ✅ ASP.NET Core Identity (Users, Roles, Claims)
- ✅ Refresh token storage with token rotation
- ✅ NSwag/Swagger UI with JWT support
- ✅ CORS configured for Nuxt.js frontend
- ✅ Automatic admin user and role seeding

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

## Advanced Usage

This playground demonstrates advanced authorization features using ASP.NET Core Identity's role and claims systems.

### User Roles

The application includes three predefined roles that work seamlessly with @sidebase/nuxt-auth:
- **guest** - Default role for new users (limited access)
- **account** - Standard authenticated users
- **admin** - Full administrative access

#### Testing Role-Based Access

1. **Create a Test User** (via Frontend or Swagger)
   - Visit `http://localhost:3000/register` or use POST `/api/auth/signup`
   - Username: `testuser`, Password: `Test123!`
   - User is automatically assigned the `guest` role

2. **Check Initial Role** (via Frontend)
   - Login as `testuser`
   - View the session data - you'll see `"role": "guest"`
   - The Nuxt app automatically includes this in the user object

3. **Login as Admin** (via Swagger)
   - Open Swagger UI at `https://localhost:5001/swagger`
   - Use POST `/api/auth/login`
   - Default admin credentials: `admin` / `Admin123!` (see console output on startup)
   - Copy the `accessToken` from the response
   - Click "Authorize" button, paste the token, click "Authorize"

4. **Change User Role** (via Swagger)
   - Use POST `/api/admin/setrole`
   - Request body:
     ```json
     {
       "userId": "user-id-from-session",
       "role": "account"
     }
     ```
   - Valid roles: `guest`, `account`, `admin`

5. **Verify Role Change** (via Frontend)
   - Return to the Nuxt app logged in as `testuser`
   - Refresh the page or wait for token refresh
   - View session data - you'll now see `"role": "account"`
   - **Important**: The role is embedded in the JWT token, so it updates when the token refreshes

#### How It Works

- Roles are stored in ASP.NET Core Identity's `AspNetUserRoles` table
- The `IdentityUserClaimsProvider` adds role claims to the JWT token
- Frontend receives the role in both the login response and session endpoint
- Token refresh automatically includes the latest role

### Subscriptions

Subscriptions are implemented using ASP.NET Core Identity's claims system, allowing fine-grained access control beyond simple roles.

#### Understanding Subscriptions

A subscription represents access to a specific feature or service, with status tracking:
```json
{
  "id": 1,
  "status": ["Active"]
}
```

Possible statuses: `Active`, `Inactive`

#### Testing Subscription Management

1. **Create a Test User and Get User ID**
   - Sign up via frontend: `http://localhost:3000/register`
   - Username: `subscriber`, Password: `Test123!`
   - Login as this user
   - Note the `id` field in the session response (e.g., `"id": "abc123..."`)

2. **Login as Admin** (via Swagger)
   - Open Swagger UI
   - POST `/api/auth/login` with admin credentials
   - Authorize with the admin token

3. **Assign Subscriptions** (via Swagger)
   - Use POST `/api/admin/setsubscriptions`
   - Request body:
     ```json
     {
       "userId": "abc123...",
       "subscriptions": [
         {
           "id": 1,
           "status": ["Active"]
         },
         {
           "id": 2,
           "status": ["Active"]
         },
         {
           "id": 99,
           "status": ["Inactive"]
         }
       ]
     }
     ```
   - This creates Identity claims of type `"subscription"` with JSON values

4. **Verify Subscriptions in Session** (via Frontend)
   - Return to Nuxt app logged in as `subscriber`
   - Refresh the page (to get new JWT token)
   - View session data - you'll see:
     ```json
     {
       "id": "abc123...",
       "name": "subscriber",
       "email": "subscriber@sample.com",
       "role": "guest",
       "subscriptions": [
         { "id": 1, "status": ["Active"] },
         { "id": 2, "status": ["Active"] },
         { "id": 99, "status": ["Inactive"] }
       ]
     }
     ```

5. **View Subscriptions** (via Swagger as Admin)
   - Use GET `/api/admin/subscriptions/{userId}`
   - Replace `{userId}` with the user's ID
   - Returns all subscriptions for that user

#### Testing Subscription-Based Authorization

The playground includes a `WeatherForecastController` that demonstrates subscription-based access control using a custom authorization policy.

1. **Ensure User Has Subscriptions**
   - Follow steps above to give `subscriber` user subscriptions with IDs 1 and 2 (both Active)

2. **Login as Subscriber** (via Swagger)
   - POST `/api/auth/login` as `subscriber`
   - Authorize with the subscriber's token

3. **Access Protected Endpoint - Success Case**
   - Use GET `/api/Subscriptions/1/WeatherForecast`
   - ✅ **Success** - Returns weather data because:
     - User is authenticated
     - User has subscription with `id: 1`
     - Subscription status is `Active`

4. **Access Protected Endpoint - Different Subscription**
   - Use GET `/api/Subscriptions/2/WeatherForecast`
   - ✅ **Success** - Returns weather data (user has active subscription 2)

5. **Access Protected Endpoint - No Subscription**
   - Use GET `/api/Subscriptions/99/WeatherForecast`
   - ❌ **403 Forbidden** - Access denied because:
     - Subscription 99 exists but status is `Inactive`

6. **Access Protected Endpoint - Missing Subscription**
   - Use GET `/api/Subscriptions/999/WeatherForecast`
   - ❌ **403 Forbidden** - User doesn't have subscription 999

#### How Subscription Authorization Works

1. **Storage**: Subscriptions are stored as Identity claims:
   - Claim Type: `"subscription"`
   - Claim Value: JSON-serialized `SubscriptionInfo`

2. **JWT Inclusion**: The `IdentityUserClaimsProvider` automatically includes all user claims in the JWT token

3. **Authorization Policy**: Custom `SubscriptionRequirement` and `SubscriptionHandler`:
   - Extracts `subscriptionId` from the route (e.g., `/api/Subscriptions/{subscriptionId}/WeatherForecast`)
   - Reads subscription claims from the JWT token
   - Deserializes each claim to find matching subscription ID
   - Verifies the subscription status is `Active`
   - Returns 403 Forbidden if no matching active subscription

4. **Controller Protection**: Apply the policy with:
   ```csharp
   [Authorize(Policy = "RequireActiveSubscription")]
   public class WeatherForecastController : ControllerBase
   ```

5. **Token Refresh**: Subscriptions update when the JWT refreshes (not in real-time)

#### Subscription Workflow Summary

```
Admin → POST /api/admin/setsubscriptions
  ↓
Identity Claim Created (type: "subscription", value: JSON)
  ↓
IdentityUserClaimsProvider includes all claims in JWT
  ↓
User Logs In / Refreshes Token
  ↓
JWT Contains Subscription Claims
  ↓
Frontend: Session shows subscriptions array
  ↓
Backend: Authorization handler validates subscription for protected resources
```

### Real-World Use Cases

**Subscriptions enable:**
- SaaS tier management (free, pro, enterprise)
- Feature flags per user
- Multi-tenant resource access
- Time-limited access (add expiry date to claim value)
- A/B testing cohorts

**Example: Multi-tier SaaS**
```json
{
  "subscriptions": [
    { "id": "basic", "status": ["Active"] },
    { "id": "advanced-analytics", "status": ["Active"] },
    { "id": "api-access", "status": ["Inactive"] }
  ]
}
```

Different routes/controllers can require different subscriptions:
- `/api/Subscriptions/basic/*` - Available to all tiers
- `/api/Subscriptions/advanced-analytics/*` - Pro tier and above
- `/api/Subscriptions/api-access/*` - Enterprise only

## Configuration

See `appsettings.json` for JWT settings, connection strings, and CORS configuration.

## What to Learn From This

This playground demonstrates:
- Minimal setup with maximum functionality
- Token rotation best practices
- Integration with ASP.NET Core Identity (Users, Roles, Claims)
- Role-based authorization
- Custom authorization policies using claims
- CORS configuration for frontend apps
- Working with @sidebase/nuxt-auth

## Next Steps

- See library READMEs for detailed API documentation
- See [IMPLEMENTATION.md](IMPLEMENTATION.md) for detailed code examples
- Customize the user model for your needs
- Add password reset, email verification
- Implement more complex subscription logic (expiry, limits, etc.)
- Add middleware for subscription validation
- Deploy with proper production configuration
