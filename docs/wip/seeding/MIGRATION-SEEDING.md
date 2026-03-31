# Migration Guide: Current Seeding Implementation to NuxtIdentity Seeding Library

## Overview

This guide explains how to migrate from the current ListsWebApp.V3 seeding implementation to the planned NuxtIdentity seeding library with the two-section configuration structure.

## Current Implementation Analysis

### Current Structure (ListsWebApp.V3)

**Configuration Sections:**
- `User` - Environment-specific users (unnamed section)
- `Role` - Environment-specific roles (unnamed section)
- `Access` - Environment-specific access/entitlements (unnamed section)
- `DefaultSeeder:Role` - Application-level roles (named section)
- `DefaultSeeder:List` - Application-level lists (named section)
- `DefaultSeeder:Access` - Application-level access/entitlements (named section)

**Code Location:**
- [`Main.Vue/Startup/SetupCommonIdentity.cs`](../../../Main.Vue/Startup/SetupCommonIdentity.cs) - Service registration
- [`Controllers/Seeders/SeedIdentity.cs`](../../../Controllers/Seeders/SeedIdentity.cs) - Seeding logic
- [`Controllers/Seeders/SeedRoleOptions.cs`](../../../Controllers/Seeders/SeedRoleOptions.cs) - Role configuration model
- [`Controllers/Seeders/SeedUserOptions.cs`](../../../Controllers/Seeders/SeedUserOptions.cs) - User configuration model
- [`Controllers/Seeders/SeedAccessOptions.cs`](../../../Controllers/Seeders/SeedAccessOptions.cs) - Access configuration model

**Current Pattern:**
```csharp
// Service registration - separate named options to work around array replacement
builder.Services.Configure<List<SeedUserOptions>>(
    builder.Configuration.GetSection(SeedUserOptions.Section) // "User"
);
builder.Services.Configure<List<SeedRoleOptions>>(
    builder.Configuration.GetSection(SeedRoleOptions.Section) // "Role"
);
builder.Services.Configure<List<SeedRoleOptions>>(
    "DefaultSeeder",
    builder.Configuration.GetSection("DefaultSeeder:" + SeedRoleOptions.Section)
);
```

### Current Configuration Files

**appsettings.json:**
```json
{
  "DefaultSeeder": {
    "Role": [
      { "Name": "User", "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78" },
      { "Name": "Administrator", "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91" },
      { "Name": "Functional Tests", "Id": "7de7cfd4-572c-4c2a-8fd6-baf9eeec30f2" }
    ]
  }
}
```

**appsettings.Container.json:**
```json
{
  "User": [
    {
      "Name": "Tester",
      "Email": "test@func.com",
      "Roles": ["Functional Tests"]
    }
  ]
}
```

**docker/config-docker.toml:**
```toml
[[User]]
Name = "Tester"
Email = "test@func.com"
Roles = ["Functional Tests"]

[[Invitation]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Roles = ["User"]
```

## New Structure (NuxtIdentity Library)

### New Configuration Sections

- `IdentitySeeder:Application` - Application-level data (replaces `DefaultSeeder:*`)
- `IdentitySeeder:Environment` - Environment-specific data (replaces unnamed sections)

### Migration Mapping

| Current Section | New Section | Notes |
|----------------|-------------|-------|
| `DefaultSeeder:Role` | `IdentitySeeder:Application:Roles` | Application-required roles |
| `DefaultSeeder:List` | N/A | Lists are application-specific, not part of NuxtIdentity library |
| `DefaultSeeder:Access` | N/A | Access/entitlements are application-specific |
| `User` | `IdentitySeeder:Environment:Users` | Environment-specific users |
| `Role` | `IdentitySeeder:Environment:Roles` | Environment-specific roles (if any) |
| `Invitation` | `IdentitySeeder:Environment:Invitations` | Environment-specific invitations |

## Migration Steps

### Step 1: Update Configuration Files

**appsettings.json** (Before):
```json
{
  "DefaultSeeder": {
    "Role": [
      { "Name": "User", "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78" },
      { "Name": "Administrator", "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91" },
      { "Name": "Functional Tests", "Id": "7de7cfd4-572c-4c2a-8fd6-baf9eeec30f2" }
    ]
  }
}
```

**appsettings.json** (After):
```json
{
  "IdentitySeeder": {
    "Application": {
      "Roles": [
        { "Name": "User", "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78" },
        { "Name": "Administrator", "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91" },
        { "Name": "Functional Tests", "Id": "7de7cfd4-572c-4c2a-8fd6-baf9eeec30f2" }
      ],
      "Users": [],
      "RoleClaims": [],
      "Invitations": []
    },
    "Environment": {
      "Roles": [],
      "Users": [],
      "RoleClaims": [],
      "Invitations": []
    }
  }
}
```

**appsettings.Container.json** (Before):
```json
{
  "User": [
    { "Name": "Tester", "Email": "test@func.com", "Roles": ["Functional Tests"] }
  ]
}
```

**appsettings.Container.json** (After):
```json
{
  "IdentitySeeder": {
    "Environment": {
      "Users": [
        { "UserName": "Tester", "Email": "test@func.com", "Roles": ["Functional Tests"] }
      ]
    }
  }
}
```

**docker/config-docker.toml** (Before):
```toml
[[User]]
Name = "Tester"
Email = "test@func.com"
Roles = ["Functional Tests"]

[[Invitation]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Roles = ["User"]
```

**docker/config-docker.toml** (After):
```toml
[[IdentitySeeder.Environment.Users]]
UserName = "Tester"  # Note: "Name" becomes "UserName"
Email = "test@func.com"
Roles = ["Functional Tests"]

[[IdentitySeeder.Environment.Invitations]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Roles = ["User"]
ExpiresIn = "7.00:00:00"
```

### Step 2: Update Service Registration

**Before** (in `Main.Vue/Startup/SetupCommonIdentity.cs`):
```csharp
public static WebApplicationBuilder SetupCommonIdentityOptions(this WebApplicationBuilder builder)
{
    // Instance seeders
    builder.Services.Configure<List<SeedUserOptions>>(
        builder.Configuration.GetSection(SeedUserOptions.Section)
    );
    builder.Services.Configure<List<SeedRoleOptions>>(
        builder.Configuration.GetSection(SeedRoleOptions.Section)
    );
    
    // Default seeders
    builder.Services.Configure<List<SeedRoleOptions>>(
        "DefaultSeeder",
        builder.Configuration.GetSection("DefaultSeeder:" + SeedRoleOptions.Section)
    );
    
    return builder;
}
```

**After** (using NuxtIdentity library):
```csharp
public static WebApplicationBuilder SetupIdentitySeeding(this WebApplicationBuilder builder)
{
    // Single registration - library handles both Application and Environment sections
    builder.Services.AddNuxtIdentitySeeding(builder.Configuration);
    
    return builder;
}
```

### Step 3: Update Seeding Call

**Before** (in `Main.Vue/Startup/SetupDatabase.cs`):
```csharp
var seeder = scope.ServiceProvider.GetRequiredService<SeedIdentity>();
await seeder.SeedAsync();
```

**After** (using NuxtIdentity library):
```csharp
await app.SeedNuxtIdentityAsync();
```

### Step 4: Handle Application-Specific Seeding

The NuxtIdentity library only handles Identity and Invitations tables. Application-specific seeding (Lists, Access/Entitlements) must remain in application code.

**Option A**: Keep separate seeder for application-specific data
```csharp
// Seed Identity (using NuxtIdentity library)
await app.SeedNuxtIdentityAsync();

// Seed application-specific data (Lists, Access)
var appSeeder = scope.ServiceProvider.GetRequiredService<SeedApplicationData>();
await appSeeder.SeedAsync();
```

**Option B**: Extend NuxtIdentity seeder (if permitted by library design)
- Check if library provides extension points
- Implement custom seeder that inherits/extends library seeder

### Step 5: Update Configuration Property Names

**Key Changes:**
- `User.Name` → `User.UserName` (align with ASP.NET Identity)
- `Role.Id` remains the same (optional GUID for test stability)
- Add `User.EmailConfirmed` (defaults to `true` in library)
- Add `Invitation.ExpiresIn` (duration, optional)

### Step 6: Remove Old Seeding Code

Once migration is complete and tested, remove:
- `Controllers/Seeders/SeedIdentity.cs` (if replaced by library)
- `Controllers/Seeders/SeedUserOptions.cs` (if replaced by library)
- `Controllers/Seeders/SeedRoleOptions.cs` (if replaced by library)
- Option registration logic in `SetupCommonIdentity.cs`

**Keep:**
- `Controllers/Seeders/SeedAccessOptions.cs` (application-specific)
- Application-specific seeding logic for Lists, Access, etc.

## Testing Migration

### 1. Local Development
```bash
# Start with fresh database
dotnet ef database drop
dotnet ef database update

# Run application
dotnet run --project Main.Vue

# Verify:
# - Application roles exist (User, Administrator, Functional Tests)
# - No environment-specific users in dev (unless configured)
```

### 2. Container Environment
```bash
# Build and run container
docker compose up

# Verify:
# - Application roles exist
# - Tester user exists
# - Invitation code is seeded
```

### 3. Functional Tests
```bash
# Run functional tests
dotnet test Tests.Functional

# Verify:
# - Tests can authenticate with seeded credentials
# - Tests can use seeded invitation codes
# - Tests can access seeded lists with proper entitlements
```

## Benefits of Migration

1. **Cleaner Configuration**: Single `IdentitySeeder` section with clear Application/Environment subdivision
2. **Less Code**: No custom seeding logic for Identity tables - library handles it
3. **Better Documentation**: NuxtIdentity library provides consistent, tested seeding pattern
4. **Easier Maintenance**: Updates and bug fixes come from library
5. **Separation of Concerns**: Identity seeding vs application-specific seeding is clearer

## Application-Specific Seeding Remains

**These are NOT handled by NuxtIdentity library and must remain in application code:**
- Lists (`DefaultSeeder:List`)
- Access/Entitlements (`DefaultSeeder:Access`, `Access`)
- Any other application-specific tables

**Recommended Pattern:**
```csharp
// 1. Seed Identity (NuxtIdentity library)
await app.SeedNuxtIdentityAsync();

// 2. Seed Application-specific data (ListsWebApp code)
var scope = app.Services.CreateScope();
var listSeeder = scope.ServiceProvider.GetRequiredService<SeedLists>();
await listSeeder.SeedAsync();

var accessSeeder = scope.ServiceProvider.GetRequiredService<SeedAccess>();
await accessSeeder.SeedAsync();
```

## Rollback Plan

If issues arise during migration:

1. **Keep both implementations temporarily**: Old code and new library side-by-side
2. **Use feature flag**: Toggle between old and new seeding in `Program.cs`
3. **Gradual migration**: Migrate one environment at a time (Dev → Container → Production)

## Related Documents

- [`seeding-config-namespace-solution.md`](seeding-config-namespace-solution.md) - Detailed solution design
- [`PRD-SEEDING.md`](PRD-SEEDING.md) - Original product requirements
- [`PRD-SEEDING-UPDATED.md`](PRD-SEEDING-UPDATED.md) - Update summary for PRD
