# Basic Authentication Example with Local Provider

Reference implementation demonstrating how to use the NuxtIdentity libraries to build a complete JWT authentication system with ASP.NET Core Identity and Entity Framework Core, surfaced to a Nuxt frontend.

This playground application shows best practices for integrating all three NuxtIdentity libraries:
- **NuxtIdentity.Core** - Generic JWT and refresh token services
- **NuxtIdentity.AspNetCore** - Base controller and Identity integration  
- **NuxtIdentity.EntityFrameworkCore** - Persistent refresh token storage

## 🎯 What You'll Learn

- Setting up JWT authentication with refresh token rotation
- Integrating ASP.NET Core Identity with a modern frontend
- Configuring @sidebase/nuxt-auth for secure token management
- Building professional authentication UI with Bootstrap and Vue
- Using .NET Aspire for local development orchestration

## ✨ Features

### Authentication Flow

- ✅ **Login** - Username/password authentication via ASP.NET Core Identity
- ✅ **Sign Up** - User registration with automatic 'guest' role assignment
- ✅ **Refresh** - Automatic token refresh with secure rotation (inherited from base controller)
- ✅ **Logout** - Token revocation and cleanup (inherited from base controller)
- ✅ **Session** - Get current user information including roles and claims
- ✅ **Protected Routes** - Automatic redirect for unauthenticated users

### User Interface

- 🎨 **Professional UI** - Bootstrap 5 with custom styling and animations
- 📱 **Responsive Design** - Mobile-friendly authentication forms
- 🔍 **Form Validation** - Real-time validation with helpful error messages
- 🔐 **Security Indicators** - Clear visual feedback for auth states

## 🏗️ Architecture

### Frontend Stack
- **Framework**: Nuxt 4 with TypeScript
- **Authentication**: @sidebase/nuxt-auth with local provider
- **Styling**: Bootstrap 5 + Custom SCSS
- **Token Management**: JWT with automatic refresh

### Backend Stack  
- **Framework**: .NET 10 Web API
- **Authentication**: NuxtIdentity + ASP.NET Core Identity
- **Database**: SQLite with Entity Framework Core
- **Documentation**: NSwag/OpenAPI with Swagger UI
- **Token Storage**: Persistent refresh tokens with automatic cleanup

### Development Environment
- **Orchestration**: .NET Aspire for service coordination
- **Hot Reload**: Both frontend and backend support live updates
- **Debugging**: Integrated logging and development tools

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) (for frontend development)

### Running the Application

1. **Start the application stack**
   ```bash
   dotnet watch --project AppHost
   ```

2. **Access the services**
   - 🌐 **Aspire Dashboard** opens automatically in your browser
   - ⏳ **Wait** for both backend and frontend to show as "Healthy"
   - 🖱️ **Click** the frontend URL to open the app

3. **Try it out**
   - Create a new account on the registration page
   - Log in with your credentials
   - Explore the protected dashboard area

## 📁 Project Structure

```
samples/Local/
├── AppHost/              # .NET Aspire orchestration
├── Backend/              # ASP.NET Core API
│   ├── Program.cs        # NuxtIdentity configuration
│   └── Controllers/      # Authentication endpoints
├── Frontend/             # Nuxt 4 application  
│   ├── nuxt.config.ts    # Auth provider setup
│   ├── pages/            # Login, register, dashboard
│   └── components/       # Reusable UI components
└── ServiceDefaults/      # Shared Aspire configuration
```

## 🔧 Key Configuration Files

### Backend: Program.cs
```csharp
// Add NuxtIdentity services with Entity Framework
builder.Services.AddNuxtIdentity<IdentityUser, ApplicationDbContext>()
    .AddNuxtIdentityAuthentication();
```

### Frontend: nuxt.config.ts  
```typescript
auth: {
  provider: {
    type: 'local',
    endpoints: {
      signIn: { path: '/login', method: 'post' },
      refresh: { path: '/refresh', method: 'post' },
      // ...
    }
  }
}
```

## 🔮 What's Not Included (Yet)

The following advanced scenarios are planned for future samples:

### Advanced Authorization
- 🚧 **Role-Based Access Control** - Fine-grained permission systems
- 🚧 **Subscription-Based Access** - Custom authorization using Identity claims  
- 🚧 **Admin Endpoints** - User and subscription management interfaces
- 🚧 **Multi-Tenant Support** - Organization-based data isolation

See [ASPNET-IDENTITY](../../docs/ASPNET-IDENTITY.md) for details on how these features integrate with NuxtIdentity.

## 🛠️ Development Tips

### Frontend Development
```bash
cd Frontend
npm run dev  # Start frontend only (backend must be running separately)
```

### Backend Development  
```bash
cd Backend
dotnet watch  # Start backend with hot reload
```

### Database Management
- SQLite database is created automatically
- Database file: `Backend/app.db`
- Migrations applied on startup

## 📚 Learn More

- [NuxtIdentity Documentation](../../docs/)
- [@sidebase/nuxt-auth Guide](https://sidebase.io/nuxt-auth/getting-started)
- [ASP.NET Core Identity](https://docs.microsoft.com/aspnet/core/security/authentication/identity)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)

## 🤝 Contributing

This is a reference implementation. For questions or improvements:
1. Check the [project documentation](../../docs/)
2. Open an issue for bugs or feature requests
3. Submit PRs for enhancements

---

**Built with ❤️ using NuxtIdentity** - Secure, scalable authentication for modern web applications.
