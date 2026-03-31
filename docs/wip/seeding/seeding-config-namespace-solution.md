# Identity Seeding Configuration Namespace Solution

## Problem Statement

The Identity Seeding PRD needs to address two distinct use cases that currently conflict due to ASP.NET configuration's array replacement behavior:

1. **Application-level seeding** - Roles and data required by functional tests, consistent across all environments
2. **Environment-level seeding** - Site administrators, invitations, and environment-specific users/roles

Since ASP.NET configuration doesn't merge arrays, environment-specific config overwrites application-level config when using the same namespace.

## Current Workaround

The current ListsWebApp.V3 implementation uses separate named options sections:
- Default/unnamed section for environment-specific seeding
- `"DefaultSeeder"` named section for application-level seeding

See [`Main.Vue/Startup/SetupCommonIdentity.cs:41-66`](../../../Main.Vue/Startup/SetupCommonIdentity.cs:41) for the current pattern.

## Selected Solution: Separate Subsections (Option A)

### Configuration Structure

Use two distinct subsections under a single `IdentitySeeder` root:
- `IdentitySeeder:Application` - Application-level fixtures (roles for tests, etc.)
- `IdentitySeeder:Environment` - Environment-specific data (admins, invitations, etc.)

### Example Configuration

**appsettings.json** (application-wide)
```json
{
  "IdentitySeeder": {
    "Application": {
      "Roles": [
        { "Name": "User", "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78" },
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

**appsettings.Production.json** (environment-specific)
```json
{
  "IdentitySeeder": {
    "Environment": {
      "Roles": [
        { "Name": "Administrator", "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91" }
      ],
      "Users": [
        {
          "UserName": "admin",
          "Email": "admin@production.com",
          "EmailConfirmed": true,
          "Roles": ["Administrator"]
        }
      ],
      "Invitations": [
        {
          "Code": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Email": "newuser@production.com",
          "Roles": ["User"],
          "ExpiresIn": "30.00:00:00"
        }
      ]
    }
  }
}
```

**docker/config-docker.toml** (container environment)
```toml
[[IdentitySeeder.Environment.Users]]
Name = "Tester"
Email = "test@func.com"
Roles = ["Functional Tests"]
# Password via environment var: IdentitySeeder__Environment__Users__0__Password

[[IdentitySeeder.Environment.Invitations]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Roles = ["User"]
ExpiresIn = "7.00:00:00"
```

### Seeder Implementation Pattern

```csharp
// Service registration
builder.Services.AddNuxtIdentitySeeding(builder.Configuration);

// Startup seeding
await app.SeedNuxtIdentityAsync();
```

Internally, the seeder processes both sections:
```csharp
var config = configuration.GetSection("IdentitySeeder");

// 1. Seed application-level data first (test fixtures, required roles)
var appConfig = config.GetSection("Application").Get<IdentitySeedOptions>();
if (appConfig != null)
    await SeedFromOptions(appConfig);

// 2. Seed environment-specific data (admins, invitations, env-specific roles)
var envConfig = config.GetSection("Environment").Get<IdentitySeedOptions>();
if (envConfig != null)
    await SeedFromOptions(envConfig);
```

### Benefits

1. **Clear Separation**: Configuration structure self-documents the two use cases
2. **Simple Implementation**: No custom merge logic - just read two sections sequentially
3. **Natural Merging**: Application roles + Environment roles work together automatically
4. **Flexible**: Each section can evolve independently
5. **Environment Override**: `appsettings.{Environment}.json` can override either or both sections

### Use Case Mapping

| Use Case | Configuration Section | Typical Contents |
|----------|----------------------|------------------|
| Functional test required roles | `Application:Roles` | Roles with fixed GUIDs for test stability |
| Functional test fixtures | `Application:Users`, `Application:Invitations` | Seeded test data with predictable codes |
| Site administrator accounts | `Environment:Users` | Admin users per environment (dev, staging, prod) |
| Environment-specific roles | `Environment:Roles` | Roles unique to an environment |
| Invitation codes for onboarding | `Environment:Invitations` | Pre-created invitation codes for initial users |
| Role permissions (claims) | Either section | `RoleClaims` for role-based permissions |

### Migration from Current Implementation

The current ListsWebApp.V3 pattern:
- `User`, `Role`, `Access` sections → Move to `IdentitySeeder:Environment`
- `DefaultSeeder:*` sections → Move to `IdentitySeeder:Application`

Migration is straightforward - restructure config sections into the new hierarchy.

## Alternative Considered: Merge Control Flag (Option B)

Single `IdentitySeeder` section with `MergeWithApplicationDefaults: true` flag. The seeder would traverse configuration provider layers to manually merge arrays.

**Rejected because**:
- Significantly more complex implementation
- Requires accessing `IConfigurationRoot.Providers` directly
- Less clear configuration structure
- Potential performance impact from layer traversal

## Design Decisions

1. **Section Names**: Use `Application` and `Environment` (not `ApplicationDefaults`/`EnvironmentSpecific`) for brevity
2. **Seeding Order**: Application section processed first, then Environment section
3. **Upsert Semantics**: Both sections use non-destructive upsert (create if missing, update if different, never delete)
4. **Empty Sections**: Empty/missing sections are allowed - seeder skips gracefully

## Impact on PRD-SEEDING.md

The PRD needs updates to:
1. Add new user story for application developer needing test fixtures separate from environment config
2. Add new user story for site administrator needing environment-specific seeding without overwriting test fixtures
3. Update Technical Approach section with two-section structure
4. Update Configuration Structure examples to show both `Application` and `Environment` subsections
5. Add guidance on which section to use for which purpose
6. Update Consumer API examples to show both sections in use

## Next Steps

1. ✅ Define solution architecture
2. Update PRD-SEEDING.md with new user stories and technical details
3. Create comprehensive configuration examples for different scenarios
4. Document migration path from current implementation
