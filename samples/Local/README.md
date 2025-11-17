# Basic Authentication Example with Local Provider

This sample demonstrates the basic authentication features ASP.NET Core Identity working with
@sidebase/nuxt-auth, brought together by NuxtIdentity.

## Features

* Register as a new user
* Log in, receiving an auth token
* View user session information
* Stay logged in, refreshing the auth token
* Log out, invalidating further refreshes

### What's not here

NuxtIdentity also supports authorization scenarios by surfacing ASP.NET Core Identity roles and claims.
These more advanced scenarios will be covered in a future sample.

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
