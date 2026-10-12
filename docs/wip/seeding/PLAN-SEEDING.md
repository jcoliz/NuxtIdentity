---
status: Approved
prd: PRD-SEEDING.md
design: DESIGN-SEEDING.md
issue: 29
---

# Implementation Plan: Identity Seeding

## Objective

Implement the approved configuration-driven seeding feature in
`NuxtIdentity.AspNetCore`, following [DESIGN-SEEDING.md](./DESIGN-SEEDING.md) and
[PRD-SEEDING.md](./PRD-SEEDING.md). The seeder will create missing roles, users,
user-role assignments, user claims, role claims, and invitations while leaving
existing records unchanged.

## Scope and Guardrails

- Discover arbitrary named groups under `IdentitySeeder`; apply them by
  ascending `Order` (default `0`), then group name using ordinal,
  case-insensitive ordering.
- Support `TUser : IdentityUser`, standard user properties, and Identity
  `UserManager<TUser>` / `RoleManager<IdentityRole>` APIs.
- Use `IInvitationService` for invitations. Its current API already accepts an
  explicit code, so no invitation API or schema change is planned.
- Isolate invalid configuration, unsuccessful operation results, and
  exceptions at individual-item boundaries. Continue unrelated work and report
  successful, failed, and skipped operations.
- Honor cancellation; do not expose configured passwords or invitation codes
  through logs or exception details.
- Keep the library independent of configuration providers and leave database
  creation/migration ordering to the consuming application.
- Do not add a hosted service, destructive reconciliation, frontend work, or
  new dependencies unless implementation reveals a concrete requirement.

## Work Plan

### 1. Add bindable configuration models

**Target:** `src/AspNetCore/Configuration/`

- Add options for seed groups, roles, users, claims, role claims, and
  invitations, using mutable properties and binding-friendly defaults.
- Keep each arbitrary group name and each item's configuration path available
  during execution for deterministic ordering and actionable diagnostics.
- Model the documented fields: optional role ID; username, email, optional
  password, default-true `EmailConfirmed`, roles and claims; role claims; and
  invitation code, email, roles, claims, metadata, and expiry duration.
- Avoid validating the full options tree at service registration. Validate
  entries as they are processed so malformed data does not block valid items.
- Preserve standard .NET configuration binding and nested numeric array keys;
  do not implement custom provider-merging behavior.

**Exit criteria:** All documented fields bind from ordinary `IConfiguration`,
including multiple independent named groups.

### 2. Implement ordered, create-only seeding

**Target:** New scoped seeder under `src/AspNetCore/Services/` (or the
repository's established service namespace).

- Enumerate the immediate children beneath `IdentitySeeder`, bind each group,
  and sort by `Order`, then ordinal-ignore-case group name.
- Execute each group in dependency order: roles, users, user-role assignments,
  user claims, role claims, then invitations.
- Match roles by name, users by username, and invitations by code. Create only
  missing entities; never update existing scalar fields, passwords, lifecycle
  states, assignments, or claims.
- Apply a configured role GUID only when creating a missing role; validate it
  and store its canonical string representation.
- Create users through `UserManager<TUser>`. Apply a non-empty configured
  password only during creation and rely on Identity password validation.
- Independently add missing role assignments and exact `(Type, Value)` claim
  pairs. Resolve dependencies against current manager/database state and skip
  only operations whose required user or role is unavailable.
- Create ordinary Pending invitations through `IInvitationService.CreateAsync`
  only when the configured non-empty GUID code is absent. Validate code and
  expiry, and re-read after a uniqueness race before classifying creation as a
  failure.
- If invitation configuration exists without `IInvitationService`, emit one
  warning and count configured invitation operations as skipped; continue
  Identity stages.
- Catch and record per-operation failures, including unsuccessful
  `IdentityResult` values. Let cancellation propagate and fail clearly when
  required Identity services are unavailable.
- Emit structured item diagnostics and a completion summary with succeeded,
  failed, and skipped counts; mark partial completion whenever failures or
  skips occurred. Exclude passwords and invitation codes from diagnostic data.

**Exit criteria:** Repeated runs are idempotent, earlier groups can satisfy
later dependencies, later definitions never overwrite existing rows, and
failure/cancellation behavior matches the design.

### 3. Add registration and startup extensions

**Targets:**

- `src/AspNetCore/Extensions/IdentitySeederServiceCollectionExtensions.cs`
- `src/AspNetCore/Extensions/IdentitySeederApplicationBuilderExtensions.cs`

- Add `AddNuxtIdentitySeeding<TUser>(IServiceCollection, IConfiguration)` and
  register the seeder with a scoped lifetime while retaining configuration for
  named-group discovery.
- Add `SeedNuxtIdentityAsync<TUser>(IApplicationBuilder, CancellationToken)`
  that creates an asynchronous DI scope, resolves the seeder, and runs it.
- Keep seeding explicitly invoked by the consumer after database setup and
  migrations; do not run it as a hosted service.
- Follow existing extension naming, namespaces, and XML documentation patterns.

**Exit criteria:** A consumer can use the documented registration and startup
calls, including a custom `IdentityUser` type and cancellation token.

### 4. Add focused behavioral and integration tests

**Target:** `tests/NuxtIdentity.AspNetCore.Tests/`

- Add NUnit tests using the existing Identity test stores/helpers and
  Given/When/Then comments.
- Cover arbitrary group binding, default order, order/group-name tie-breaking,
  and deterministic processing.
- Cover creation and idempotence for each entity/association/claim, including
  later groups adding distinct claims or memberships without modifying
  existing values.
- Verify new-user password validation and that existing-user passwords are
  never reset or logged.
- Verify invitation validation, creation with supplied code and fields,
  empty/invalid GUID handling, missing service behavior, uniqueness-race
  re-read, and unchanged invitations across lifecycle states.
- Inject failures at individual operation boundaries and assert subsequent
  independent work continues while only unavailable dependencies are skipped.
- Verify summary counts/log severity, safe diagnostics, cancellation
  propagation, missing required Identity services, and startup scope disposal.

**Exit criteria:** Focused tests assert the documented outcomes rather than
only successful method calls; test logs do not contain seeded secrets.

### 5. Document consumer setup and evaluate existing seeders

**Targets:**

- `src/AspNetCore/README.md`
- Existing playground/sample `DatabaseExtensions.cs` seeders, if they are in
  the intended delivery scope.

- Document package registration, required Identity registration, and the
  requirement to call the startup extension after database initialization.
- Include representative named-group configuration, ordering behavior,
  create-only semantics, and the standard provider/array-merging caveat.
- Recommend invitation-first onboarding. Explain optional direct-user
  seeding, secret-provider/environment-variable password configuration, and
  that existing users' passwords are never reset.
- Review the playground and sample ad-hoc seeders against the new API. If
  migrating them is part of this release, preserve their intended local
  behavior and ensure their database setup still precedes seeding; do not
  introduce application-specific seed data into the library.

**Exit criteria:** README usage compiles conceptually against the shipped API;
any migrated consumer invokes seeding in the correct startup order and retains
its intended development experience.

### 6. Verify the completed feature

- Run the focused `NuxtIdentity.AspNetCore.Tests` test suite.
- Build the ASP.NET Core project and, if consumer examples are migrated, build
  the affected playground/sample projects.
- Review the diff for API naming consistency, unintended package/schema
  changes, swallowed cancellation, nondeterministic ordering, duplicate
  operations, and accidental logging of secrets.

**Definition of done:** The accepted behavior is covered by tests, documented
consumer calls match the implementation, targeted builds/tests pass, and no
database migration or unrelated product changes are introduced.

## Dependency Order

1. Configuration models
2. Seeder implementation
3. Registration/startup extensions
4. Behavioral tests (test scaffolding can be prepared alongside steps 1–3)
5. README and any agreed consumer adoption
6. Targeted verification and final review

## Acceptance Checklist

- [ ] Any number of named groups are supported and ordered deterministically.
- [ ] Roles, users, associations, claims, and invitations use the specified
  match keys and create-only semantics.
- [ ] Missing dependencies, invalid entries, failed results, and exceptions
  are isolated and counted; independent operations continue.
- [ ] Cancellation propagates; missing required services fail clearly.
- [ ] Passwords and invitation codes are excluded from logs and exception
  diagnostics.
- [ ] The startup extension uses and disposes an async scope and remains under
  consumer control after migrations.
- [ ] Focused tests, project build, README, and any agreed consumer migration
  are complete.
