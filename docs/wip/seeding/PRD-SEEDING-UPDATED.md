# Configuration Update Summary for PRD-SEEDING.md

## Changes to Make

This document summarizes the updates needed to [`PRD-SEEDING.md`](PRD-SEEDING.md) to address the configuration namespace conflict between application-level and environment-specific seeding.

### 1. User Stories - Add Two New Stories at the Beginning

Insert before current "Story 1: Developer - Seed roles from configuration":

#### Story 1: Application Developer - Seed application-wide roles and test fixtures
**As an** application developer using NuxtIdentity  
**I want** to define application-required roles (like "User", "Administrator") and test fixture data in application-level configuration  
**So that** these roles and fixtures remain consistent across all environments without being overwritten by environment-specific configuration

**Acceptance Criteria**:
- [ ] Can define roles in `IdentitySeeder:Application` section that persist across all environments
- [ ] Application-level roles include both business roles (User, Administrator) and test roles (Functional Tests)
- [ ] Application-level roles are seeded before environment-specific roles
- [ ] Application-level configuration can include roles with fixed GUIDs for test stability
- [ ] Environment-specific configuration in `appsettings.{Environment}.json` does not overwrite application-level fixtures
- [ ] Works with any IConfiguration source including appsettings.json, TOML, and environment variables

#### Story 2: Site Administrator - Configure environment-specific seeding without overwriting test fixtures
**As a** site administrator configuring a deployment environment  
**I want** to define environment-specific users, roles, and invitations  
**So that** I can seed environment-specific administrators and invitations without affecting application-level test fixtures

**Acceptance Criteria**:
- [ ] Can define environment-specific data in `IdentitySeeder:Environment` section
- [ ] Environment-specific seeding adds to (not replaces) application-level seeding
- [ ] Can seed site administrators with environment-specific credentials
- [ ] Can seed invitation codes for onboarding new users in specific environments
- [ ] Configuration in `appsettings.Production.json` seamlessly merges with application-level fixtures

### 2. User Stories - Renumber Existing Stories

- Current Story 1 → Story 3
- Current Story 2 → Story 4
- Current Story 3 → Story 5
- Current Story 4 → Story 6
- Current Story 5 → Story 7
- Current Story 6 → Story 8
- Current Story 7 → Story 9

### 3. User Stories - Update Story 3 (formerly Story 1)

Change acceptance criteria:
- OLD: "Roles defined in configuration are created..."
- NEW: "Roles defined in **either configuration section** are created..."

### 4. Technical Approach - Replace Entire Section

Replace from "## Technical Approach" through the end of "Environment Variables for Secrets" with:

---

## Technical Approach

The seeding feature reads from .NET `IConfiguration`, making it source-agnostic. Whether the developer uses appsettings.json, config.toml loaded as configuration, environment variables, or any combination, the library just works with the configuration system.

### Two-Section Configuration Structure

To address the requirement that application-level seeding (roles required by the application and functional tests) must coexist with environment-specific seeding (site administrators, invitations), the configuration uses two distinct subsections:

- **`IdentitySeeder:Application`** - Application-required roles (User, Administrator, Functional Tests) and test fixture data, consistent across all environments
- **`IdentitySeeder:Environment`** - Environment-specific users (site admins), invitations, and environment-specific roles

This solves the ASP.NET configuration array replacement problem: since `Application` and `Environment` are separate paths, environment-specific config files can define `Environment` data without overwriting `Application` data.

### Configuration Examples

**appsettings.json** (application-wide, all environments):

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

**appsettings.Production.json** (environment-specific):

```json
{
  "IdentitySeeder": {
    "Environment": {
      "Users": [
        {
          "UserName": "admin",
          "Email": "admin@production.com",
          "EmailConfirmed": true,
          "Roles": ["Administrator"],
          "Claims": [
            { "Type": "department", "Value": "operations" }
          ]
        }
      ],
      "Invitations": [
        {
          "Code": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Email": "newuser@production.com",
          "Roles": ["User"],
          "ExpiresIn": "30.00:00:00"
        }
      ],
      "RoleClaims": [
        {
          "Role": "Administrator",
          "Claims": [
            { "Type": "permission", "Value": "manage-users" },
            { "Type": "permission", "Value": "view-reports" }
          ]
        }
      ]
    }
  }
}
```

**docker/config-docker.toml** (container environment):

```toml
# Application-level roles (test fixtures) - defined in appsettings.json
# No need to repeat here unless overriding

[[IdentitySeeder.Environment.Users]]
Name = "Tester"
Email = "test@func.com"
Roles = ["Functional Tests"]
# Password via environment var: IdentitySeeder__Environment__Users__0__Password

[[IdentitySeeder.Environment.Invitations]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Email = "evaluator@tryout.com"
Roles = ["User"]
ExpiresIn = "7.00:00:00"
```

UserRoles and UserClaims are nested under their parent User entries for ergonomic configuration. RoleClaims are a separate top-level section because they associate claims with roles, not users. Invitations are a top-level section because they represent pending registrations, not existing users or roles.

**Environment Variables for Secrets** (recommended for production):

```
IdentitySeeder__Environment__Users__0__Password=SecureProductionPassword!
IdentitySeeder__Environment__Users__1__Password=AnotherSecurePassword!
```

This leverages .NET's built-in configuration layering — passwords stay out of config files and are injected via environment variables, container secrets, or Azure Key Vault.

### Section Usage Guidance

| Use Case | Section | Typical Location | Example Contents |
|----------|---------|------------------|------------------|
| Application-required business roles | `Application:Roles` | `appsettings.json` | User, Administrator |
| Functional test fixture roles | `Application:Roles` | `appsettings.json` | Functional Tests (with fixed GUID) |
| Functional test fixture users | `Application:Users` | `appsettings.json` | Test users with predictable credentials |
| Site administrator accounts | `Environment:Users` | `appsettings.{Env}.json` or TOML | Admin users per environment |
| Environment-specific roles | `Environment:Roles` | `appsettings.{Env}.json` | Roles unique to an environment |
| Invitation codes | `Environment:Invitations` | `appsettings.{Env}.json` or TOML | Pre-created invitations |
| Role permissions | Either section | Depends on use case | RoleClaims for role-based permissions |

---

### 5. Consumer API - Update

Change the implementation approach section:

```csharp
// Service registration
builder.Services.AddNuxtIdentitySeeding(builder.Configuration);

// Startup seeding
await app.SeedNuxtIdentityAsync();
```

The seeder internally processes both sections:
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

### 6. Seeding Order - Update

Update the seeding order to reflect two-section processing:

1. **Application Section**: Process in dependency order (Roles → Users → UserRoles → UserClaims → RoleClaims → Invitations)
2. **Environment Section**: Process in dependency order (Roles → Users → UserRoles → UserClaims → RoleClaims → Invitations)

### 7. Key Business Rules - Add New Rule

Add after rule #2 (Non-Destructive):

3. **Two-Section Merging** — The seeder processes `IdentitySeeder:Application` first, then `IdentitySeeder:Environment`. Both sections use the same upsert semantics, allowing environment-specific data to add to or override application data without deleting application fixtures.