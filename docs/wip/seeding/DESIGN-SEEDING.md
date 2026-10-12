---
status: In Review
prd: PRD-SEEDING.md
issue: 29
---

# Design Document: Identity Seeding

## Overview

This document describes a configuration-driven startup seeder in `NuxtIdentity.AspNetCore`. It seeds roles, users, user-role associations, user claims, role claims, and invitations through ASP.NET Core Identity managers and `IInvitationService`. Configuration is discovered as arbitrary named groups under `IdentitySeeder`, merged into one desired state, and applied in deterministic group order. The seeder creates missing items but never changes or deletes existing items.

The preferred onboarding path is to seed roles and invitations, then let invitees choose their own passwords through invitation registration. Direct user and password seeding remains supported for fixtures and other explicit needs.

## Design Decisions

- **Package and ownership:** Implement the options, seeder, and registration/startup extensions in `NuxtIdentity.AspNetCore`. It already depends on ASP.NET Core Identity APIs through the ASP.NET Core shared framework. No database schema changes are required for core seeding.
- **Generic user type:** The seeder must use the consumer's `UserManager<TUser>`, so the registration and startup extensions are generic: `AddNuxtIdentitySeeding<TUser>(...)` and `SeedNuxtIdentityAsync<TUser>(...)`, where `TUser : IdentityUser`. A non-generic API cannot resolve custom user types reliably from DI. Role seeding uses `IdentityRole`, matching the existing role-claims provider.
- **Configuration binding:** Enumerate immediate children under `IdentitySeeder`, bind each group to the same options shape, and sort by `Order` ascending (default `0`) followed by `StringComparer.OrdinalIgnoreCase` group name. Do not model groups as an array or reserve group names.
- **Invitation code:** The current `IInvitationService.CreateAsync` already accepts optional `Guid? code`; `EfInvitationService` already uses it and creates invitations as Pending. The PRD's open question is therefore resolved in the current codebase; no code parameter change is needed.
- **Create-only behavior:** A matching role name, username, claim pair, role assignment, or invitation Code is already present and is a no-op. Never update its fields, reset a password, or change invitation lifecycle state. Missing roles, users, assignments, claims, and invitations may be created. Existing roles, users, and invitations do not prevent separate missing associations or claims from being added.
- **Passwords:** Never include configured passwords in logs or exception details. Set a configured password through `UserManager` only when creating a new user. Never reset the password of an existing user from configuration. Configuration guidance recommends secret providers and invitation-first onboarding.
- **No destructive reconciliation:** Existing roles, users, claims, assignments, and invitations are never updated or removed. Missing configured items can be created; configuration omissions do not revoke data.

## Configuration Models

**Files**: `src/AspNetCore/Configuration/IdentitySeederOptions.cs` and related option types (new)

Use mutable bindable options with nullable/reference properties and defaults suitable for configuration binding:

- `IdentitySeederGroupOptions`: `Order`, `Roles`, `Users`, `RoleClaims`, `Invitations`
- `SeedRoleOptions`: optional `Id`, required `Name`
- `SeedUserOptions`: required `UserName`; optional `Email`, `Password`; `EmailConfirmed` defaulting to `true`; `Roles` and `Claims`
- `SeedClaimOptions`: required `Type` and `Value`
- `SeedRoleClaimsOptions`: required `Role` and `Claims`
- `SeedInvitationOptions`: required `Code`; optional `Email`, `Roles`, `Claims`, `Metadata`, `ExpiresIn`

Parse and validate individual entries during execution instead of validating the entire options tree at registration time. This allows one malformed item to be reported and skipped while valid items continue. Preserve the configuration path with each bound item for useful diagnostics. `ExpiresIn` uses the standard .NET `TimeSpan` configuration representation; reject negative durations and invalid GUIDs. Reject `Guid.Empty` with a warning and skip that invitation.

Configuration keys remain provider-agnostic. Binding uses standard `IConfiguration` child keys and numeric indices inside each group's nested arrays; the library does not implement custom array merging.

## Service and Startup API

**Files**: `src/AspNetCore/Extensions/IdentitySeederServiceCollectionExtensions.cs` and `src/AspNetCore/Extensions/IdentitySeederApplicationBuilderExtensions.cs` (new)

Proposed consumer usage:

```csharp
builder.Services.AddNuxtIdentitySeeding<ApplicationUser>(builder.Configuration);

// After database creation/migration and Identity registration.
await app.SeedNuxtIdentityAsync<ApplicationUser>();
```

Registration adds the seeder as a scoped service and retains the `IConfiguration` root for named-group discovery. The startup extension creates an async service scope and resolves the scoped seeder. This matches the scoped lifetime of Identity managers and EF-backed invitation services. Do not run seeding as a hosted service: the consumer must control ordering after migrations/database creation.

The execution method accepts an optional `CancellationToken`. Check cancellation between operations and allow cancellation exceptions to propagate; do not count application shutdown as an item failure. Identity manager methods do not all accept cancellation tokens, so cancellation cannot interrupt an operation already in progress.

Missing `UserManager<TUser>` or `RoleManager<IdentityRole>` is a service-registration/setup failure and should fail startup clearly. Invitation configuration is optional: if configured but no `IInvitationService` is registered, emit one warning and count each configured invitation as skipped; continue all other stages.

## Seeding Flow

For each ordered group, execute these stages in sequence:

1. **Roles**: Validate each role. Find by name using `RoleManager<IdentityRole>`. Create when absent, using the configured string `Id` only for creation (parse a valid GUID and store its canonical string). If present, make no changes, even if a configured ID differs. Role names are the matching key.
2. **Users**: Validate each username and find by username using `UserManager<TUser>`. If absent, create a new `TUser` with the supported standard properties; if a non-empty password is configured, set it through Identity password APIs as part of user creation. If present, make no changes to its username, email, `EmailConfirmed`, or password, even if configured values differ. The username is the matching key; do not silently rename users or reset passwords.
3. **User roles**: For each configured assignment, resolve both user and role. If either is unavailable, skip and log only that assignment. Call `AddToRoleAsync` only when `GetRolesAsync` shows the user is not already in the role. An existing assignment is a no-op; do not remove unconfigured memberships.
4. **User claims**: Resolve the user and read existing claims via `GetClaimsAsync`. Add each missing exact `(Type, Value)` pair using `AddClaimAsync`. An existing claim is a no-op; same type with another value is a distinct claim and may be added.
5. **Role claims**: Resolve the role and read existing claims via `RoleManager.GetClaimsAsync`. Add each missing exact `(Type, Value)` pair with `AddClaimAsync`. Existing claims are no-ops.
6. **Invitations**: Validate code and fields. Look up by code with `IInvitationService.GetByCodeAsync`. Create a missing invitation with `CreateAsync`, supplying code and all configured fields. If an invitation with that code already exists, make no changes regardless of its configured-field differences or lifecycle state. On a uniqueness race during create, re-read by code and treat the now-existing invitation as a no-op.

A stage is not wrapped in a single catch. Each configured item and each association/claim is an independent operation. A failure resolving one entity does not suppress unrelated items. Before a dependent operation, use the current database/manager state rather than assuming an earlier create succeeded.

Groups are applied in order. References may use entities already present in the database or created by the current or earlier groups; the seeder does not defer a missing dependency until a later group. Existing database rows always win over configuration. When an item is absent, the first group in execution order that defines it supplies its initial fields; later groups cannot update those fields. Distinct missing role assignments and claims are separate items and may be added by any group.

## Invitation Handling

No `IInvitationService` change is needed. Its existing `CreateAsync` accepts the configured `Guid? code`; the seeder checks for that code first and calls `CreateAsync` only when absent. Do not use `CreateTestAsync`; seeded invitations are ordinary application invitations. Existing invitations, including Accepted, Revoked, and Expired invitations, remain untouched.

## Failure Handling and Logging

Use structured logs with group name, configuration path, item kind, and a safe identifying key (role name, username, assignment, or invitation database ID when known). Invitation codes and passwords are secrets: never log them. For invitation errors before an entity is loaded, identify the configuration path, not the code. Sanitize Identity error descriptions as needed to ensure password values are not echoed.

Catch and record exceptions at the smallest operation boundary, including failed `IdentityResult` and invitation-service outcomes. Continue later items, stages, and groups. Do not catch an `OperationCanceledException` when the supplied token is canceled. Fatal setup errors outside item processing (for example, missing required Identity services) propagate.

Maintain run counters for succeeded, failed, and skipped operations. A no-change/idempotent operation counts as succeeded. Log a completion summary that explicitly signals partial completion when failures or skips occurred; item failures do not fail application startup.

## Testing

Add focused NUnit tests in `tests/NuxtIdentity.AspNetCore.Tests` using the existing Identity test stores and Gherkin-style Given/When/Then comments:

- Configuration binds arbitrary named groups and sorts by `Order`, then case-insensitive group name; missing `Order` is zero.
- Existing role/user/invitation fields are never changed; later groups can add distinct missing role assignments and claims.
- Repeated runs do not duplicate roles, users, memberships, claims, or invitations.
- User creation uses UserManager; a configured password is applied only for a newly created user, honors Identity validation, and is never logged.
- A failed role/user/claim/assignment/invitation does not prevent later items or groups; dependent operations alone are skipped when entities are unavailable.
- Invalid group/item configuration, invalid or empty invitation code, missing invitation service, and invitation lifecycle states are isolated and counted correctly.
- Existing invitations in every lifecycle state remain unchanged; a configured code creates an invitation only when not already present.
- Cancellation propagates; completion logs distinguish success, failure, and skip.
- Startup extension creates/disposes an async scope and runs after consumer-controlled database initialization.

## Implementation Scope

- New configuration option models, scoped seeder, and service/startup extensions in `NuxtIdentity.AspNetCore`.
- Use the existing `IInvitationService.CreateAsync` code parameter; no invitation API or schema change is required.
- Focused tests for binding, ordered create-only seeding, failure isolation, invitation behavior, and startup integration.
- Update `src/AspNetCore/README.md` with registration, startup ordering, invitation-first guidance, and secret handling. No schema migration or frontend changes.

## PRD Items Requiring Reconciliation

1. **Invitation lifecycle:** Create-only semantics leave every existing invitation unchanged, including terminal states. The existing `CreateAsync` code parameter supports deterministic invitation creation without direct EF Core access.
2. **API genericity:** The PRD's non-generic example must become generic to support the repository's `TUser : IdentityUser` pattern.
