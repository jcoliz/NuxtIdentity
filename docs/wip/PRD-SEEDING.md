---
status: In Review
design_document: TBD
ado: TBD
---

# Product Requirements Document: Identity Seeding

## Problem Statement

Developers using NuxtIdentity need a way to pre-populate ASP.NET Identity tables (users, roles, claims, and their associations) and NuxtIdentity-owned tables (invitations) from configuration at application startup. Today, each consumer writes ad-hoc seeding code (as seen in the playground and samples `DatabaseExtensions.cs` files), leading to duplicated boilerplate and inconsistent approaches. A reusable, configuration-driven seeding library would eliminate this duplication and provide a consistent, well-tested pattern across all environments.

---

## Goals & Non-Goals

### Goals
- [ ] Provide a reusable library feature that seeds ASP.NET Identity tables and NuxtIdentity-owned tables from .NET Configuration
- [ ] Support seeding all five key Identity tables: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, and AspNetRoleClaims
- [ ] Support seeding the NuxtIdentity Invitations table for test and development environments
- [ ] Use non-destructive upsert semantics: create missing items, update existing ones to match config, but never remove data
- [ ] Log warnings when the database contains seeded-category items that are not present in the current configuration
- [ ] Work across all environments: Local Development, CI Containers, and Production
- [ ] Be agnostic to configuration source — works with appsettings.json, TOML, environment variables, or any IConfiguration provider

### Non-Goals
- Will NOT delete or remove data from the database that is not in the configuration (non-destructive)
- Will NOT manage runtime-created data (e.g., users who sign up through the application)
- Will NOT provide a UI or API for managing seed data — this is a startup-time, configuration-driven feature
- Will NOT enforce security policies around passwords in production — this is documented guidance, not enforcement
- Will NOT seed application-specific tables beyond the standard ASP.NET Identity tables and NuxtIdentity-owned tables (e.g., Invitations)
- Will NOT handle database migrations — the database schema must already exist before seeding runs

---

## User Stories

### Story 1: Developer - Seed roles from configuration
**As a** developer using NuxtIdentity
**I want** to define roles in my application configuration
**So that** my application has the required roles available on every startup without writing custom code

**Acceptance Criteria**:
- [ ] Roles defined in configuration are created in the AspNetRoles table if they do not exist
- [ ] If a role already exists by name, no error occurs and no duplicate is created
- [ ] Works with any IConfiguration source including appsettings.json, TOML, and environment variables

### Story 2: Developer - Seed users with role assignments from configuration
**As a** developer using NuxtIdentity
**I want** to define users with their passwords and role assignments in configuration
**So that** my application has the required user accounts ready on every startup

**Acceptance Criteria**:
- [ ] Users defined in configuration are created in the AspNetUsers table if they do not exist
- [ ] User passwords are set using ASP.NET Identity's UserManager so password hashing and policy validation apply
- [ ] If a user already exists by username, their password is updated to match configuration
- [ ] Users are assigned to the roles specified in configuration via the AspNetUserRoles table
- [ ] If a user already has a role assignment, no duplicate is created
- [ ] Missing role assignments from config are added to existing users

### Story 3: Developer - Seed user claims from configuration
**As a** developer using NuxtIdentity
**I want** to define claims for specific users in configuration
**So that** my users have the required claims for authorization on every startup

**Acceptance Criteria**:
- [ ] Claims defined under a user in configuration are added to the AspNetUserClaims table
- [ ] If a claim with the same type and value already exists for the user, no duplicate is created
- [ ] Claims with the same type but different values are added as additional claims, matching ASP.NET Identity behavior

### Story 4: Developer - Seed role claims from configuration
**As a** developer using NuxtIdentity
**I want** to define claims for specific roles in configuration
**So that** all users in a role automatically inherit those claims

**Acceptance Criteria**:
- [ ] Claims defined under a role in configuration are added to the AspNetRoleClaims table
- [ ] If a claim with the same type and value already exists for the role, no duplicate is created

### Story 5: Developer - Receive warnings for config drift
**As a** developer using NuxtIdentity
**I want** to be warned at startup if the database contains seeded items not in my current configuration
**So that** I can identify configuration drift or stale seed data

**Acceptance Criteria**:
- [ ] If the database contains roles that were previously seeded but are no longer in config, a warning is logged
- [ ] If the database contains user-role assignments for seeded users that are no longer in config, a warning is logged
- [ ] Warnings do not block application startup
- [ ] Warnings include enough detail to identify the specific items causing the drift

### Story 6: Developer - Seed invitations from configuration
**As a** developer using NuxtIdentity
**I want** to define invitations in my application configuration
**So that** my test and development environments have predictable invitation codes available without writing custom setup code

**Acceptance Criteria**:
- [ ] Invitations defined in configuration are created in the Invitations table if they do not exist
- [ ] Seeded invitations support specifying: Code (GUID, required), Email, Roles, Claims, Metadata, and ExpiresIn (duration)
- [ ] The developer provides a predictable Code (GUID) in configuration so that tests and manual workflows can reference it
- [ ] All seeded invitations are created with Pending status
- [ ] Seeded invitations follow the same upsert semantics as other seeded data (create if missing, update if different, never delete)
- [ ] The upsert match key is Code — if an invitation with the same Code already exists, it is updated rather than duplicated
- [ ] The seeder never seeds an empty GUID (`00000000-0000-0000-0000-000000000000`). An empty GUID in configuration is treated as a misconfiguration and generates a warning
- [ ] Seeding invitations requires `IInvitationService` to be registered; if missing, invitation seeding is skipped with a warning

### Story 7: Developer - Integrate seeding into application startup
**As a** developer using NuxtIdentity
**I want** a simple API to add seeding to my application
**So that** I don't need to write boilerplate seeding code

**Acceptance Criteria**:
- [ ] Seeding can be added with a service registration call and an application startup call
- [ ] Seeding runs after database creation/migration and Identity registration
- [ ] Seeding is idempotent — running it multiple times produces the same result
- [ ] Seeding logs information about what actions were taken at startup

---

## Technical Approach

The seeding feature reads from .NET `IConfiguration`, making it source-agnostic. Whether the developer uses appsettings.json, config.toml loaded as configuration, environment variables, or any combination, the library just works with the configuration system.

**Configuration Structure**:

```json
{
  "IdentitySeeder": {
    "Roles": [
      { "Name": "admin" },
      { "Name": "user" }
    ],
    "Users": [
      {
        "UserName": "admin",
        "Email": "admin@example.com",
        "Password": "Admin-123!",
        "EmailConfirmed": true,
        "Roles": ["admin"],
        "Claims": [
          { "Type": "department", "Value": "engineering" }
        ]
      }
    ],
    "RoleClaims": [
      {
        "Role": "admin",
        "Claims": [
          { "Type": "permission", "Value": "manage-users" },
          { "Type": "permission", "Value": "view-reports" }
        ]
      }
    ],
    "Invitations": [
      {
        "Code": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "Email": "newdev@example.com",
        "Roles": ["user"],
        "Claims": [
          { "Type": "department", "Value": "engineering" }
        ],
        "ExpiresIn": "7.00:00:00"
      }
    ]
  }
}
```

UserRoles and UserClaims are nested under their parent User entries for ergonomic configuration. RoleClaims are a separate top-level section because they associate claims with roles, not users. Invitations are a top-level section because they represent pending registrations, not existing users or roles.

**Equivalent TOML** (loaded as IConfiguration):

```toml
[[IdentitySeeder.Roles]]
Name = "admin"

[[IdentitySeeder.Roles]]
Name = "user"

[[IdentitySeeder.Users]]
UserName = "admin"
Email = "admin@example.com"
Password = "Admin-123!"
EmailConfirmed = true
Roles = ["admin"]

[[IdentitySeeder.Users.Claims]]
Type = "department"
Value = "engineering"

[[IdentitySeeder.RoleClaims]]
Role = "admin"

[[IdentitySeeder.RoleClaims.Claims]]
Type = "permission"
Value = "manage-users"

[[IdentitySeeder.Invitations]]
Code = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
Email = "newdev@example.com"
Roles = ["user"]
ExpiresIn = "7.00:00:00"

[[IdentitySeeder.Invitations.Claims]]
Type = "department"
Value = "engineering"
```

**Environment Variables for Secrets** (recommended for production):

```
IdentitySeeder__Users__0__Password=SecureProductionPassword!
```

This leverages .NET's built-in configuration layering — passwords stay out of config files and are injected via environment variables, container secrets, or Azure Key Vault.

**Consumer API**:

```csharp
// In Program.cs — Service registration
builder.Services.AddNuxtIdentitySeeding(builder.Configuration);

// In Program.cs — After database setup, before app.Run()
await app.SeedNuxtIdentityAsync();
```

**Upsert Semantics**:

The seeder uses non-destructive upsert behavior:

| Scenario | Behavior |
|---|---|
| Item in config, not in DB | Create it |
| Item in config and in DB, matching | No change |
| Item in config and in DB, different | Update DB to match config |
| Item in DB, not in config | Log warning, do not remove |

**Seeding Order**:

Seeding must execute in dependency order:
1. Roles (no dependencies)
2. Users (no dependencies)
3. UserRoles (depends on Roles and Users)
4. UserClaims (depends on Users)
5. RoleClaims (depends on Roles)
6. Invitations (no dependencies on other seeded data; requires `IInvitationService`)

**Layers Affected**:
- [ ] Frontend (Vue/Nuxt)
- [ ] Controllers (API endpoints)
- [X] Application (Features/Business logic): Seeding service and configuration models
- [X] Entities (Domain models): Configuration option classes for seed data
- [ ] Database (Schema changes)

**Key Business Rules**:
1. **Idempotent Execution** — Running the seeder multiple times produces the same database state. No duplicates, no errors on re-runs.
2. **Non-Destructive** — The seeder never removes data. Items present in the database but absent from configuration trigger a warning log, not a deletion.
3. **Configuration Layering** — Passwords and other secrets should be provided via environment variables or secret stores, not config files. The library leverages standard .NET configuration layering for this.
4. **Dependency Order** — Roles must be seeded before UserRoles and RoleClaims. Users must be seeded before UserRoles and UserClaims. Invitations have no dependencies on other seeded data.
5. **Identity API Usage** — All operations go through UserManager and RoleManager (for Identity tables) or `IInvitationService` (for Invitations), not direct database access, ensuring password hashing, validation, and Identity behaviors are preserved.
6. **Optional Invitation Seeding** — Invitation seeding requires `IInvitationService` to be registered. If the service is not available (e.g., the consuming app doesn't use invitations), invitation configuration is skipped with a warning log. This keeps the seeder usable for Identity-only scenarios.

**Code Patterns to Follow**:
- Configuration options: [`JwtOptions`](../../src/Core/Configuration/JwtOptions.cs) for options class pattern
- Extension methods: [`ServiceCollectionExtensions`](../../src/AspNetCore/Extensions/ServiceCollectionExtensions.cs) for registration pattern
- Existing seeding reference: [`DatabaseExtensions.cs`](../../playground/local/Extensions/DatabaseExtensions.cs) for current ad-hoc approach to replace
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

- [X] **Library Packaging**: Should seeding live in an existing NuxtIdentity package or in a new dedicated package? **A**: Seeding lives in `NuxtIdentity.AspNetCore`. It shares the same ASP.NET Core Identity dependencies (UserManager, RoleManager) and will always be used together. Extraction to a separate package remains possible later without breaking the API.
- [X] **Custom User Types**: Should the seeder support seeding custom properties on derived user types? **A**: Not for the initial release. Only standard `IdentityUser` properties (UserName, Email, Password, EmailConfirmed) are supported. Custom user type extensibility can be added when the need arises.
- [X] **Password Update on Upsert**: When a user already exists and the config has a password, should the password always be reset to match config? **A**: Yes, always reset to match config. This follows the "ensure DB matches config" principle. If a developer doesn't want to re-set a password, they omit the `Password` field for that user — the seeder skips password operations when Password is null/empty.
- [X] **EmailConfirmed Default**: Should `EmailConfirmed` default to `true` for seeded users? **A**: Yes. Seeded users are pre-trusted, so `EmailConfirmed` defaults to `true`. Developers can explicitly set `"EmailConfirmed": false` per user in config for test scenarios that require an unconfirmed user.
- [X] **Section Name**: Is `IdentitySeeder` the right configuration section name? **A**: Yes. `IdentitySeeder` is concise, descriptive, and doesn't tie it to Nuxt since the feature is Identity-generic. Follows .NET PascalCase conventions.
- [ ] **Invitation Seeding API**: `IInvitationService.CreateAsync` currently generates a random GUID for the code. The seeder needs to provide a specific code from configuration. Should `CreateAsync` accept an optional `code` parameter, or should the seeder bypass the service and write directly via EF Core? This is a design-time decision.

---

## Success Metrics

- Playground and sample projects can replace their ad-hoc `DatabaseExtensions` seeding code with the library feature
- Seeded invitations have predictable codes that tests and manual workflows can reference
- Consumers can define their complete Identity seed data in configuration with zero custom C# seeding code
- All seeded data survives application restarts without duplication
- Configuration changes are reflected in the database on next startup
- Warnings surface configuration drift clearly in logs

---

## Dependencies & Constraints

**Dependencies**:
- ASP.NET Core Identity (UserManager, RoleManager) must be registered before seeding runs
- Database must be created/migrated before seeding runs
- IConfiguration must be available with seed data populated
- `IInvitationService` must be registered for invitation seeding (optional — skipped with warning if absent)

**Constraints**:
- Must work with the generic `TUser : IdentityUser` pattern used throughout NuxtIdentity
- Must use UserManager/RoleManager APIs (for Identity data) and `IInvitationService` (for invitations), not direct database access, to preserve Identity behaviors and invitation lifecycle semantics
- Must not add heavy dependencies that would bloat the package

---

## Notes & Context

The playground currently seeds with hard-coded roles `["guest", "account", "admin"]` and a hard-coded admin user in [`DatabaseExtensions.cs`](../../playground/local/Extensions/DatabaseExtensions.cs). The samples have a similar but empty pattern in [`DatabaseExtensions.cs`](../../samples/Local/Backend/Extensions/DatabaseExtensions.cs). Both would be replaced by this library feature.

The developer's existing ListsWebApp project uses a similar configuration-driven pattern with both JSON and TOML, proving the approach works in practice. This PRD formalizes and generalizes that pattern as a reusable library.

**Security Note**: Passwords in configuration files are a known risk. The recommended practice is to use .NET configuration layering — define non-secret seed data in appsettings.json and inject passwords via environment variables (`IdentitySeeder__Users__0__Password`), container secrets, or Azure Key Vault. The library documentation should include this guidance prominently.

**Related Documents**:
- [PRD Template](PRD-TEMPLATE.md)
- [PRD Token GUIDs](PRD-TOKEN-GUIDS.md) — reference for PRD quality
- [PRD Registration](PRD-REGISTRATION.md) — defines the invitation system that the seeder supports

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [X] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [X] All user stories have clear acceptance criteria
- [X] Open questions are resolved or documented as design decisions
- [X] Technical approach section indicates affected layers
- [X] Code patterns to follow are referenced (links to similar controllers/features)
