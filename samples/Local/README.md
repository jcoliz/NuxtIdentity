# Basic Authentication Example with Local Provider

Reference implementation demonstrating how to use the NuxtIdentity libraries to build a complete JWT authentication system with ASP.NET Core Identity and Entity Framework Core, surfaced to a Nuxt frontend.

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

## What's Not Included

### Authorization

NuxtIdentity also supports authorization scenarios by surfacing ASP.NET Core Identity roles and claims.
These more advanced scenarios will be covered in a future sample. See [ASPNET-IDENTITY](../../docs/ASPNET-IDENTITY.md) 
for a dicussion on how these are integrated.

- ✅ **Role-Based Access Control** - Application-defined roles.
- ✅ **Subscription-Based Access** - Custom authorization using Identity claims
- ✅ **Admin Endpoints** - User and subscription management

## Getting Started

1. Ensure .NET 10 SDK Installed
2. Run the .NET Aspire host
    ```
    dotnet watch --project AppHost
    ```
3. This will open the Aspire dashboard in a browser window
4. Wait until backand and frontend components are reported as healthy
5. Click on the frontend URL to open the app

## Architecture

## Frontend

* Running Nuxt 4
* With @sidebase/nuxt-auth
* Recommended configuration of local provider for calling a backend running NuxtIdentity.
* Good-looking professional-quality UI
* Custom UI we control, so don't have to rely on external repo for front end

### Backend

* .NET Web API
* Using NuxtIdentity
* Bare minimum functionality, just the auth backend APIs
* With swagger UI for further investigation.

### Orchestration

This sample uses .NET Aspire to orchestrate these components. This is purely a demonstration convenience.
