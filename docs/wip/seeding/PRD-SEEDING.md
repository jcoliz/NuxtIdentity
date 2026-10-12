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
- [ ] Support seeding the NuxtIdentity Invitations table across all environments, with invitation-first onboarding as the recommended practice
- [ ] Use non-destructive upsert semantics: create missing items, update existing ones to match config, but never remove data
- [ ] Log warnings when the database contains seeded-category items that are not present in the current configuration
- [ ] Work across all three environments: Local Development, Container, and Deployment (including production)
- [ ] Be agnostic to configuration source — works with appsettings.json, TOML, environment variables, or any IConfiguration provider
- [ ] Support any number of named seed groups so independent configuration contributions do not compete for array indices

### Non-Goals

- Will NOT delete or remove data from the database that is not in the configuration (non-destructive)
- Will NOT manage runtime-created data (e.g., users who sign up through the application)
- Will NOT provide a UI or API for managing seed data — this is a startup-time, configuration-driven feature
- Will NOT enforce security policies around passwords in production — this is documented guidance, not enforcement
- Will NOT seed application-specific tables beyond the standard ASP.NET Identity tables and NuxtIdentity-owned tables (e.g., Invitations)
- Will NOT handle database migrations — the database schema must already exist before seeding runs

## User Stories

### Story 1: Application Developer - Seed test fixture roles separate from environment configuration

**As an** application developer using NuxtIdentity
**I want** to define roles and data required by functional tests in application-level configuration
**So that** my test fixtures remain consistent across all environments without being overwritten by environment-specific configuration

**Acceptance Criteria**:

- [ ] Can define roles in `IdentitySeeder:Application` section that persist across all environments
- [ ] Application-level roles are seeded before environment-specific roles when their groups use the recommended ordering
- [ ] Application-level configuration can include roles with fixed GUIDs for test stability
- [ ] Environment-specific configuration in `appsettings.Container.json` or `appsettings.Development.json` does not overwrite application-level fixtures in `appsettings.json`
- [ ] Works with any IConfiguration source including appsettings.json, TOML, and environment variables

### Story 2: Application Developer - Configure development and testing convenience data

**As an** application developer using NuxtIdentity
**I want** to define special environment-specific roles and invitations for development and testing, with direct user seeding available when needed
**So that** local development and container-based tests have convenient seed data without overwriting shared application-level fixtures or being leaked to production

**Acceptance Criteria**:

- [ ] Can define development and testing convenience data in the `IdentitySeeder:Environment` group
- [ ] Environment-specific seeding adds to (not replaces) application-level seeding
- [ ] Can seed predictable invitation codes with roles and claims for development and test workflows without configuring seeded passwords
- [ ] Direct user seeding remains supported for development and test scenarios that explicitly require pre-created accounts
- [ ] Configuration in `appsettings.Container.json` or `appsettings.Development.json` contributes the `Environment` group without colliding with application-level fixture arrays
- [ ] Production environment-specific seeding is allowed, but is not a current use case and is not required by the recommended configuration examples

### Story 3: Developer - Seed roles from configuration

**As a** developer using NuxtIdentity
**I want** to define roles in my application configuration
**So that** my application has the required roles available on every startup without writing custom code

**Acceptance Criteria**:

- [ ] Roles defined in any seed group are created in the AspNetRoles table if they do not exist
- [ ] If a role already exists by name, no error occurs and no duplicate is created
- [ ] Works with any IConfiguration source including appsettings.json, TOML, and environment variables

### Story 4: Developer - Seed users with role assignments from configuration

**As a** developer using NuxtIdentity
**I want** to define users with their passwords and role assignments in configuration
**So that** my application has the required user accounts ready on every startup

Direct user seeding, including passwords, is a supported capability rather than the recommended onboarding workflow. Prefer seeding invitations and letting users choose their passwords when claiming them.

**Acceptance Criteria**:

- [ ] Users defined in configuration are created in the AspNetUsers table if they do not exist
- [ ] User passwords are set using ASP.NET Identity's UserManager so password hashing and policy validation apply
- [ ] If a user already exists by username, their password is updated to match configuration
- [ ] Users are assigned to the roles specified in configuration via the AspNetUserRoles table
- [ ] If a user already has a role assignment, no duplicate is created
- [ ] Missing role assignments from config are added to existing users

### Story 5: Developer - Seed user claims from configuration

**As a** developer using NuxtIdentity
**I want** to define claims for specific users in configuration
**So that** my users have the required claims for authorization on every startup

**Acceptance Criteria**:

- [ ] Claims defined under a user in configuration are added to the AspNetUserClaims table
- [ ] If a claim with the same type and value already exists for the user, no duplicate is created
- [ ] Claims with the same type but different values are added as additional claims, matching ASP.NET Identity behavior

### Story 6: Developer - Seed role claims from configuration

**As a** developer using NuxtIdentity
**I want** to define claims for specific roles in configuration
**So that** all users in a role automatically inherit those claims

**Acceptance Criteria**:

- [ ] Claims defined under a role in configuration are added to the AspNetRoleClaims table
- [ ] If a claim with the same type and value already exists for the role, no duplicate is created

### Story 7: Developer - Receive warnings for config drift

**As a** developer using NuxtIdentity
**I want** to be warned at startup if the database contains seeded items not in my current configuration
**So that** I can identify configuration drift or stale seed data

**Acceptance Criteria**:

- [ ] If the database contains roles that were previously seeded but are no longer in config, a warning is logged
- [ ] If the database contains user-role assignments for seeded users that are no longer in config, a warning is logged
- [ ] Warnings do not block application startup
- [ ] Warnings include enough detail to identify the specific items causing the drift
- [ ] Drift is evaluated against the combined desired data from all groups after seeding, respecting override precedence and additive associations; items configured in another group do not cause false drift warnings

### Story 8: Developer - Seed invitations from configuration

**As a** developer using NuxtIdentity
**I want** to define invitations in my application configuration
**So that** all environments can onboard users by claiming invitations without seeding passwords, and tests have predictable invitation codes without custom setup code

**Acceptance Criteria**:

- [ ] Invitations defined in configuration are created in the Invitations table if they do not exist
- [ ] Seeded invitations support specifying: Code (GUID, required), Email, Roles, Claims, Metadata, and ExpiresIn (duration)
- [ ] Recommended examples seed invitations instead of password-bearing users; recipients choose their passwords through the existing invitation-claim registration flow
- [ ] The developer provides a predictable Code (GUID) in configuration so that tests and manual workflows can reference it
- [ ] All seeded invitations are created with Pending status
- [ ] Seeded invitations follow the same upsert semantics as other seeded data (create if missing, update if different, never delete)
- [ ] The upsert match key is Code — if an invitation with the same Code already exists, it is updated rather than duplicated
- [ ] The seeder never seeds an empty GUID (`00000000-0000-0000-0000-000000000000`). An empty GUID in configuration is treated as a misconfiguration and generates a warning
- [ ] Seeding invitations requires `IInvitationService` to be registered; if missing, invitation seeding is skipped with a warning

### Story 9: Developer - Integrate seeding into application startup

**As a** developer using NuxtIdentity
**I want** a simple API to add seeding to my application
**So that** I don't need to write boilerplate seeding code

**Acceptance Criteria**:

- [ ] Seeding can be added with a service registration call and an application startup call
- [ ] Seeding runs after database creation/migration and Identity registration
- [ ] Seeding is idempotent — running it multiple times produces the same result
- [ ] Seeding logs information about what actions were taken at startup

### Story 10: Site Administrator - Configure site-specific deployment seeding

**As a** site administrator configuring a deployment
**I want** to define site-specific roles and invitations in `config.toml` alongside developer-owned application and environment configuration
**So that** I can onboard site administrators and other users by claiming invitations without seeding their passwords or overwriting application and test configuration

**Acceptance Criteria**:

- [ ] Can define deployment-specific data in `IdentitySeeder:Deployment`
- [ ] `config.toml` supplies the `Deployment` group independently of environment JSON and can be loaded in Local Development, Container, or Deployment
- [ ] Can seed invitations granting administrator roles, then claim them through the existing registration flow without configuring a seeded password
- [ ] Can seed site-specific invitation codes for onboarding other users
- [ ] Direct user seeding with site-specific credentials remains supported when explicitly needed
- [ ] Supports any number of arbitrary named groups; `Application`, `Environment`, and `Deployment` are conventions, not reserved names
- [ ] Every group supports the same roles, users, role claims, and invitations
- [ ] Groups are processed by ascending optional `Order` (default `0`), with group name in ordinal, case-insensitive order as the tie-breaker
- [ ] Later groups can intentionally update the same seeded item using the existing non-destructive upsert semantics
- [ ] Separate groups preserve independent contributions; providers targeting the same group path follow standard .NET configuration precedence
- [ ] Secrets can target a specific group's fields without duplicating that group's non-secret configuration

---

## Technical Approach

The seeding feature reads from .NET `IConfiguration`, making it source-agnostic. Whether the developer uses appsettings.json, config.toml loaded as configuration, environment variables, or any combination, the library just works with the configuration system.

### Named Seed Group Configuration Structure

`IdentitySeeder` is an object keyed by arbitrary group names, not an array or a fixed set of subsections. Each immediate child is a seed group with the same data shape and an optional integer `Order`. This allows application, environment, deployment, and further independent contributions to coexist:

- **`IdentitySeeder:Application`** - Application-required roles (User, Administrator, Functional Tests) and test fixture data from `appsettings.json`, consistent across all environments
- **`IdentitySeeder:Environment`** - Developer-owned development and testing convenience roles and invitations from `appsettings.Container.json` or `appsettings.Development.json`
- **`IdentitySeeder:Deployment`** - Site-administrator-owned invitations or roles from `config.toml`, supplied independently of application and environment configuration; direct user seeding remains available when needed

The three execution environments are Local Development, Container, and Deployment. The `Deployment` group names the site-specific contribution, not an execution-environment restriction: `config.toml` can be loaded in any of the three environments. The examples use this source mapping as a consumer convention; the library remains agnostic to configuration providers.

There is no current use case for an `Environment` contribution in production. Such configuration is allowed, but the recommended production setup uses application data and site-specific `Deployment` data rather than requiring a third, environment-specific contribution.

These names are recommended conventions, not reserved names. Consumers can add further groups without library changes. Use `Order: 0` for `Application`, `Order: 100` for `Environment`, and `Order: 200` for `Deployment` in the examples. Groups are processed by ascending `Order`, defaulting to `0` when omitted, then by group name using ordinal, case-insensitive ordering. Provider registration order and JSON property order do not determine group execution order.

.NET configuration merges flattened keys: later providers override matching keys, but do not append arrays or reliably replace whole arrays. Arrays use numeric child keys, so contributions to the same array path can collide by index or retain earlier entries not overwritten by a later provider. Distinct group paths isolate independent contributions instead of requiring providers to coordinate numeric slots in an outer array.

A group is a logical contribution, not necessarily a configuration provider. Each independent contribution should use a distinct group name. Multiple providers may intentionally target the same group to override individual fields, such as supplying passwords from a secret store. Nested arrays within a shared group still follow standard key-level configuration merging; the seeder does not provide custom array-append or array-replacement behavior. Group names are case-insensitive, as in .NET configuration.

### Configuration Examples

**appsettings.json** (application-wide, all environments):

```json
{
  "IdentitySeeder": {
    "Application": {
      "Order": 0,
      "Roles": [
        { "Name": "User", "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78" },
        { "Name": "Administrator", "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91" },
        { "Name": "Functional Tests", "Id": "7de7cfd4-572c-4c2a-8fd6-baf9eeec30f2" }
      ],
      "Users": [],
      "RoleClaims": [],
      "Invitations": []
    }
  }
}
```

**appsettings.Container.json / appsettings.Development.json** (developer-owned development and testing convenience data):

```json
{
  "IdentitySeeder": {
    "Environment": {
      "Order": 100,
      "Invitations": [
        {
          "Code": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Email": "tester@example.com",
          "Roles": ["Functional Tests"],
          "Claims": [
            { "Type": "department", "Value": "testing" }
          ],
          "ExpiresIn": "30.00:00:00"
        }
      ],
      "RoleClaims": [
        {
          "Role": "Functional Tests",
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

**config.toml** (site-administrator-owned contribution, usable in any of the three environments):

```toml
# Application roles come from appsettings.json.
# Environment data comes from appsettings.Container.json or appsettings.Development.json.
# This independent site-specific contribution can be loaded in any environment.

[IdentitySeeder.Deployment]
Order = 200

[[IdentitySeeder.Deployment.Invitations]]
Code = "7fc2ff35-e15d-42c1-98fa-1b0a729cfef2"
Email = "operator@production.com"
Roles = ["Administrator"]
ExpiresIn = "7.00:00:00"

[[IdentitySeeder.Deployment.Invitations]]
Code = "ec493b28-18fb-4b0d-a686-f0eb230678b3"
Email = "evaluator@tryout.com"
Roles = ["User"]
ExpiresIn = "7.00:00:00"
```

UserRoles and UserClaims are nested under their parent User entries for ergonomic configuration. RoleClaims are a separate top-level section because they associate claims with roles, not users. Invitations are a top-level section because they represent pending registrations, not existing users or roles.

### Recommended Onboarding: Seed Invitations, Not Passwords

Seed roles and invitations, then have recipients claim their invitations through the existing registration flow. Roles and claims come from the invitation, while the recipient supplies their password during registration. The seeder does not need to receive, store, or reset that password. This is the preferred practice across Local Development, Container, and Deployment.

Direct user seeding remains supported for scenarios that require pre-created accounts, including test fixtures. It is not disabled or deprecated, and its password-upsert semantics remain unchanged.

**Optional Direct User Seeding** (capability example, not the recommended onboarding workflow):

```toml
[[IdentitySeeder.Deployment.Users]]
UserName = "Tester"
Email = "test@func.com"
Roles = ["Functional Tests"]
# If a password is required, inject it from a secret provider rather than this file.
```

**Environment Variables for Secrets** (only when direct password-based user seeding is needed):

```env
IdentitySeeder__Deployment__Users__0__Password=<password-from-secret-provider>
```

This targets the optional user example above and leverages .NET's built-in configuration layering — passwords stay out of config files and are injected via environment variables, container secrets, or Azure Key Vault. Invitation-only seeding requires no seeded-user password secrets.

### Section Usage Guidance

| Use Case | Section | Typical Location | Example Contents |
| ---------- | --------- | ------------------ | ------------------ |
| Application-required business roles | `Application:Roles` | `appsettings.json` | User, Administrator |
| Functional test fixture roles | `Application:Roles` | `appsettings.json` | Functional Tests (with fixed GUID) |
| Functional test fixture invitations | `Application:Invitations` | `appsettings.json` | Predictable invitation codes with test roles |
| Functional test fixture users (optional) | `Application:Users` | `appsettings.json`, with passwords from secret providers | Pre-created test accounts when required |
| Development and test onboarding | `Environment:Invitations` | `appsettings.Container.json` or `appsettings.Development.json` | Predictable invitations with test roles and claims |
| Development and test convenience roles | `Environment:Roles` | `appsettings.Container.json` or `appsettings.Development.json` | Additional roles for development or testing |
| Site administrator onboarding | `Deployment:Invitations` | `config.toml` in any of the three environments | Invitations granting Administrator |
| Site-specific invitations | `Deployment:Invitations` | `config.toml` in any of the three environments | Operator and evaluator onboarding codes |
| Site-specific roles | `Deployment:Roles` | `config.toml` in any of the three environments | Additional roles for a site |
| Site-specific users (optional) | `Deployment:Users` | `config.toml`, with passwords from secret providers | Pre-created accounts when required |
| Additional independent contributions | `<GroupName>:Roles`, `Users`, `RoleClaims`, or `Invitations` | Any IConfiguration provider | Data isolated from existing groups |
| Role permissions | Any group | Depends on use case | RoleClaims for role-based permissions |

**Consumer API**:

```csharp
// In Program.cs — Service registration
builder.Services.AddNuxtIdentitySeeding(builder.Configuration);

// In Program.cs — After database setup, before app.Run()
await app.SeedNuxtIdentityAsync();
```

The seeder discovers all immediate child groups under `IdentitySeeder`, binds each to the same seed options shape (including optional `Order`), and processes them in the defined order. It does not hard-code group names:

```csharp
var config = configuration.GetSection("IdentitySeeder");

// Conceptual flow; binding and validation are defined in the design document.
var groups = BindAndValidateSeedGroups(config);
foreach (var group in groups
    .OrderBy(group => group.Options.Order)
    .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase))
{
    await SeedFromOptions(group.Options);
}
```

**Upsert Semantics**:

The seeder uses non-destructive upsert behavior:

| Scenario | Behavior |
| --- | --- |
| Item in config, not in DB | Create it |
| Item in config and in DB, matching | No change |
| Item in config and in DB, different | Update DB to match config |
| Same item defined in multiple groups | Later groups take precedence for configured updates; associations remain additive |
| Seeded-category item in DB, not in combined desired data | Log warning after all groups finish, do not remove |

Group isolation prevents configuration-key collisions; it does not prevent intentional database-level updates when groups refer to the same role name, username, or invitation Code. Existing additive role and claim behavior remains unchanged. A later group omitting a user's password does not undo an earlier group's password operation.

**Seeding Order**:

Seeding processes every group in ascending `Order`, then group name as defined above. The recommended example orders produce `Application`, then `Environment`, then `Deployment`. Within each group:

1. Roles (no dependencies)
2. Users (no dependencies)
3. UserRoles (depends on Roles and Users)
4. UserClaims (depends on Users)
5. RoleClaims (depends on Roles)
6. Invitations (no dependencies on other seeded data; requires `IInvitationService`)

References to roles or users seeded in another group must target data from an earlier group, or data already present in the database. A group must not rely on a later group to satisfy a dependency. Drift warnings are evaluated only after all groups have been processed, against their combined desired data.

**Layers Affected**:

- [ ] Frontend (Vue/Nuxt)
- [ ] Controllers (API endpoints)
- [X] Application (Features/Business logic): Seeding service and configuration models
- [X] Entities (Domain models): Configuration option classes for seed data
- [ ] Database (Schema changes)

**Key Business Rules**:

1. **Idempotent Execution** — Running the seeder multiple times produces the same database state. No duplicates, no errors on re-runs.
2. **Non-Destructive** — The seeder never removes data. Items present in the database but absent from configuration trigger a warning log, not a deletion.
3. **Named Seed Groups** — The seeder supports any number of named groups under `IdentitySeeder`, ordered by ascending optional `Order` (default `0`) and then ordinal, case-insensitive group name. Every group uses the same non-destructive upsert semantics. Independent contributions use distinct group paths; later groups may intentionally update the same database items. Drift checks use the combined desired data after all groups finish.
4. **Configuration Layering** — Passwords and other secrets should be provided via environment variables or secret stores, not config files. The library leverages standard .NET configuration layering for this.
5. **Dependency Order** — Roles must be seeded before UserRoles and RoleClaims. Users must be seeded before UserRoles and UserClaims. Invitations have no dependencies on other seeded data.
6. **Identity API Usage** — All operations go through UserManager and RoleManager (for Identity tables) or `IInvitationService` (for Invitations), not direct database access, ensuring password hashing, validation, and Identity behaviors are preserved.
7. **Optional Invitation Seeding** — Invitation seeding requires `IInvitationService` to be registered. If the service is not available (e.g., the consuming app doesn't use invitations), invitation configuration is skipped with a warning log. This keeps the seeder usable for Identity-only scenarios.
8. **Invitation-First Onboarding** — Recommend seeding invitations and claiming them through the existing registration flow so passwords never need to be seeded. Direct user seeding with passwords remains a supported, optional capability.

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
- [X] **Multiple Configuration Contributions**: How should more than two independent sources contribute seed data? **A**: Use arbitrary named groups under `IdentitySeeder`, not an outer array or a fixed list of names. `Application`, `Environment`, and `Deployment` are recommended conventions with example orders `0`, `100`, and `200`. Optional `Order` defaults to `0`; ties use ordinal, case-insensitive group-name ordering. Shared group paths retain normal .NET provider precedence and key-level array merging.
- [X] **Preferred User Onboarding**: Should password-based user seeding remain supported? **A**: Yes, but prefer seeding invitations and claiming them through the existing registration flow. Recommended examples do not seed passwords; direct user/password seeding remains available when required.
- [ ] **Invitation Seeding API**: `IInvitationService.CreateAsync` currently generates a random GUID for the code. The seeder needs to provide a specific code from configuration. Should `CreateAsync` accept an optional `code` parameter, or should the seeder bypass the service and write directly via EF Core? This is a design-time decision.

---

## Success Metrics

- Playground and sample projects can replace their ad-hoc `DatabaseExtensions` seeding code with the library feature
- Seeded invitations have predictable codes that tests and manual workflows can reference
- Administrators and other users can onboard by claiming seeded invitations without any seeded password configuration
- Consumers can define their complete Identity seed data in configuration with zero custom C# seeding code
- Three or more independent configuration contributions coexist through distinct named groups without coordinating array indices
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

**Security Note**: Prefer invitation-only seeding followed by invitation claiming, so passwords are supplied during registration rather than seeded. When direct user/password seeding is explicitly needed, passwords in configuration files are a known risk: keep non-secret data in the appropriate group's configuration source and inject passwords via environment variables (for example, `IdentitySeeder__Deployment__Users__0__Password`), container secrets, or Azure Key Vault. The library documentation should include both the invitation-first recommendation and the optional password-seeding guidance prominently.

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
