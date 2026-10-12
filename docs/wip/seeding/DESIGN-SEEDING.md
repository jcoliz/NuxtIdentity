---
status: In Review
prd: PRD-SEEDING.md
issue: 29
---

# Design Document: Identity Seeding

## Overview

This document describes a configuration-driven startup seeder in `NuxtIdentity.AspNetCore`. It seeds roles, users, user-role associations, user claims, role claims, and invitations through ASP.NET Core Identity managers and `IInvitationService`. Configuration is discovered as arbitrary named groups under `IdentitySeeder`, merged into one desired state, and applied in deterministic group order without deleting database data.

The preferred onboarding path is to seed roles and invitations, then let invitees choose their own passwords through invitation registration. Direct user and password seeding remains supported for fixtures and other explicit needs.

## Design Decisions

- **Package and ownership:** Implement the options, seeder, and registration/startup extensions in `NuxtIdentity.AspNetCore`. It already depends on ASP.NET Core Identity APIs through the ASP.NET Core shared framework. No database schema changes are required for core seeding.
- **Generic user type:** The seeder must use the consumer's `UserManager<TUser>`, so the registration and startup extensions are generic: `AddNuxtIdentitySeeding<TUser>(...)` and `SeedNuxtIdentityAsync<TUser>(...)`, where `TUser : IdentityUser`. A non-generic API cannot resolve custom user types reliably from DI. Role seeding uses `IdentityRole`, matching the existing role-claims provider.
- **Configuration binding:** Enumerate immediate children under `IdentitySeeder`, bind each group to the same options shape, and sort by `Order` ascending (default `0`) followed by `StringComparer.OrdinalIgnoreCase` group name. Do not model groups as an array or reserve group names.
- **Invitation code:** The current `IInvitationService.CreateAsync` already accepts optional `Guid? code`; `EfInvitationService` already uses it and creates invitations as Pending. The PRD's open question is therefore resolved in the current codebase; no code parameter change is needed.
- **Invitation updates:** `IInvitationService` currently supports creation and reads but has no update operation. Add a narrowly scoped `UpdatePendingAsync` operation (or equivalent explicitly pending-only update contract) so the seeder can update an existing invitation without accessing EF Core directly. Do not reset Accepted, Revoked, or Expired invitations to Pending. Preserve their lifecycle state and report that the configured invitation could not be fully applied.
- **Drift ownership limitation:** Standard Identity rows have no marker identifying which rows were created by configuration seeding. Without a ledger/schema change or a marker in Identity data, the library cannot reliably distinguish a seeded role/user from a runtime-created one after configuration is removed. Phase 1 uses advisory drift warnings: compare all persisted roles against the combined configured role names, and compare current role assignments for configured users against the combined desired assignments. Role warnings may include runtime-created roles. Exact detection of removed seeded users and their assignments is deferred; do not claim these warnings prove ownership. If strict seeded-only drift becomes mandatory, it requires an explicit ownership ledger design and schema change.
- **Passwords:** Never include configured passwords in logs or exception details. If a password is present, set it on every run through `UserManager`; if absent, leave the existing password unchanged. Configuration guidance recommends secret providers and invitation-first onboarding.
- **No destructive reconciliation:** Roles, users, claims, assignments, and invitations absent from configuration are never removed. Claims and associations are additive; configuration omissions do not revoke them.

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

1. **Roles**: Validate each role. Find by name using `RoleManager<IdentityRole>`. Create when absent, using the configured string `Id` only for creation (parse a valid GUID and store its canonical string). If present, retain its immutable Identity ID; a configured ID mismatch is an item-level failure. Role names are the matching key. No role properties other than the supported name/ID are mutated.
2. **Users**: Validate each username, find by username using `UserManager<TUser>`, or create a new `TUser` with the supported standard properties. For an existing user, update configured email and `EmailConfirmed` values and persist with `UpdateAsync` only when changed. The username is the lookup key; do not silently rename users. If a configured password is non-empty, use Identity password APIs: `AddPasswordAsync` for a user without a password and `GeneratePasswordResetTokenAsync` plus `ResetPasswordAsync` for an existing password. Never bypass password hashing or policy validation.
3. **User roles**: For each configured assignment, resolve both user and role. If either is unavailable, skip and log only that assignment. Call `AddToRoleAsync` only when `GetRolesAsync` shows the user is not already in the role. Do not remove unconfigured memberships.
4. **User claims**: Resolve the user and read existing claims via `GetClaimsAsync`. Add each missing exact `(Type, Value)` pair using `AddClaimAsync`. Existing claims are retained; same type with another value remains a separate claim.
5. **Role claims**: Resolve the role and read existing claims via `RoleManager.GetClaimsAsync`. Add each missing exact `(Type, Value)` pair with `AddClaimAsync`. Existing claims are retained.
6. **Invitations**: Validate code and fields. Look up by code with `IInvitationService.GetByCodeAsync`. Create missing invitations with `CreateAsync`, supplying code and all configured fields. For an existing Pending invitation, update only when configured values differ using the new pending-only service operation. If the existing invitation is Accepted, Revoked, or Expired, do not change lifecycle state; log the unapplied configuration as a failed item. A uniqueness race during create is caught as an item failure; re-read by code and attempt the pending update once if appropriate.

A stage is not wrapped in a single catch. Each configured item and each association/claim is an independent operation. A failure resolving one entity does not suppress unrelated items. Before a dependent operation, use the current database/manager state rather than assuming an earlier create succeeded.

Groups are applied in order. References may use entities already present in the database or created by the current or earlier groups; the seeder does not defer a missing dependency until a later group. Later groups may update supported fields for the same role/user/invitation. Role assignments and claims accumulate across groups. Drift evaluation happens once, after all groups finish.

## Invitation Service Change

**Files**: `src/Core/Abstractions/IInvitationService.cs` and `src/EntityFrameworkCore/Services/EfInvitationService.cs`

Add a pending-only update contract for an invitation identified by code, covering `Email`, `Roles`, `Claims`, `Metadata`, and expiration. The EF implementation must load the entity, return a clear not-found/non-pending outcome (or a typed result), update only those fields for Pending state, preserve `Code`, `Id`, `CreatedAt`, acceptance fields, and `IsTest`, then save. Keep it on `IInvitationService` so the seeder remains independent of the storage implementation. Do not use `CreateTestAsync`; seeded invitations are ordinary application invitations.

If the interface contract is instead designed to throw on not found/non-pending, document those outcomes and handle them as item results in the seeder. Do not treat an accepted/revoked invitation as an upsertable pending invitation.

## Drift Reporting

After all groups, construct combined desired role names and desired role assignments keyed by configured username. Read persisted roles through `RoleManager.Roles` only when the underlying role store supports `IQueryableRoleStore<IdentityRole>`; if it does not, skip role inventory drift checks with a warning rather than failing the seed run. Warn for persisted role names absent from combined configuration. For each configured user that resolves, compare current role names with the combined configured role list and warn for extra assignments; never remove them.

This is intentionally advisory and has known limits: warnings for roles may include roles created at runtime, and removed users cannot be reliably identified after their configuration disappears. User-claim and role-claim drift are not reported in Phase 1 because omissions are additive and there is no ownership history. These limits should be documented alongside consumer guidance. Strict ownership-aware drift requires revising the PRD's no-schema-change constraint.

## Failure Handling and Logging

Use structured logs with group name, configuration path, item kind, and a safe identifying key (role name, username, assignment, or invitation database ID when known). Invitation codes and passwords are secrets: never log them. For invitation errors before an entity is loaded, identify the configuration path, not the code. Sanitize Identity error descriptions as needed to ensure password values are not echoed.

Catch and record exceptions at the smallest operation boundary, including failed `IdentityResult` and invitation-service outcomes. Continue later items, stages, and groups. Do not catch an `OperationCanceledException` when the supplied token is canceled. Fatal setup errors outside item processing (for example, missing required Identity services) propagate.

Maintain run counters for succeeded, failed, and skipped operations. A no-change/idempotent operation counts as succeeded. Log a completion summary that explicitly signals partial completion when failures or skips occurred; item failures do not fail application startup. Drift warnings are reported separately and do not count as seed failures.

## Testing

Add focused NUnit tests in `tests/NuxtIdentity.AspNetCore.Tests` using the existing Identity test stores and Gherkin-style Given/When/Then comments:

- Configuration binds arbitrary named groups and sorts by `Order`, then case-insensitive group name; missing `Order` is zero.
- A later group updates a matching role/user/invitation while role assignments and claims remain additive.
- Repeated runs do not duplicate roles, users, memberships, claims, or invitations.
- User creation/update uses UserManager; password add/reset honors Identity validation and logs do not contain passwords.
- A failed role/user/claim/assignment/invitation does not prevent later items or groups; dependent operations alone are skipped when entities are unavailable.
- Invalid group/item configuration, invalid or empty invitation code, missing invitation service, and invitation lifecycle states are isolated and counted correctly.
- Pending invitations update through `IInvitationService`; accepted/revoked/expired invitations are not reset.
- Cancellation propagates; completion logs distinguish success, failure, and skip.
- Drift warnings use combined group configuration and do not remove database data; tests document known ownership limitations.
- Startup extension creates/disposes an async scope and runs after consumer-controlled database initialization.

## Implementation Scope

- New configuration option models, scoped seeder, and service/startup extensions in `NuxtIdentity.AspNetCore`.
- Extend `IInvitationService` and `EfInvitationService` with pending-only update semantics.
- Focused tests for binding, ordered upserts, failure isolation, invitation behavior, drift, and startup integration.
- Update `src/AspNetCore/README.md` with registration, startup ordering, invitation-first guidance, and secret handling. No schema migration or frontend changes.

## PRD Items Requiring Reconciliation

1. **Drift scope:** Exact seeded-only drift detection is not achievable from standard Identity tables without ownership metadata. This design proposes advisory table/config comparisons and explicitly defers removed-seed provenance. Decide whether that limitation is acceptable or revise the no-schema-change constraint to add an ownership ledger.
2. **Invitation upsert lifecycle:** A requirement to reset every existing invitation to Pending conflicts with invitation lifecycle safety. This design preserves terminal states and only updates Pending invitations; confirm this is the intended behavior.
3. **API genericity:** The PRD's non-generic example must become generic to support the repository's `TUser : IdentityUser` pattern.
