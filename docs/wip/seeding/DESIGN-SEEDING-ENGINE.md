---
status: Draft
extends: DESIGN-SEEDING.md
---

# Design Document: Generalized Seeding Engine

## Problem Statement

[Identity Seeding](./DESIGN-SEEDING.md) gives NuxtIdentity consumers configuration-driven, create-only, failure-isolated seeding of Identity tables and invitations. Applications built on NuxtIdentity also need to seed their own data (for example, demo lists owned by a demo user), and they want the same guarantees: named groups that compose across configuration files, deterministic ordering, per-item failure isolation, structured secret-safe logging, and a single startup summary.

Today all of that infrastructure lives inside `IdentitySeeder<TUser>`, interleaved with Identity-specific logic. An application that wants to seed its own data must either duplicate the infrastructure or write ad-hoc code with weaker guarantees. Seeding Identity data and application data separately also makes related fixtures harder to reason about: a demo user and the demo user's data belong together, but today they must be configured and executed by unrelated mechanisms with no ordering guarantee between them.

## Goals & Non-Goals

### Goals

- [ ] Extract the domain-agnostic seeding infrastructure (group discovery, ordering, operation boundaries, outcome counting, logging, cancellation, scoped startup) into a reusable engine with no dependency on ASP.NET Core Identity
- [ ] Re-implement Identity seeding as a set of stages that plug into the engine, preserving all existing behavior and guarantees from [DESIGN-SEEDING.md](./DESIGN-SEEDING.md)
- [ ] Let applications add their own seed stages with minimal boilerplate and the same guarantees as Identity stages
- [ ] Let Identity data and application data be defined in the same named group, so related fixtures can live together
- [ ] Guarantee that application stages can depend on Identity entities created earlier in the same group
- [ ] Produce one ordered run and one completion summary across all stages
- [ ] Keep configuration readable when Identity and application data are combined

### Non-Goals

- Will NOT preserve the `IdentitySeeder` configuration section name or the current public seeding API; the project is early and a clean break is acceptable
- Will NOT add update, upsert, or delete semantics; create-only remains the contract for all stages, enforced by convention for application stages
- Will NOT discover stages by assembly scanning; stages are registered explicitly
- Will NOT manage migrations or database creation
- Will NOT provide cross-group dependency resolution (deferring an item until a later group creates its dependency)
- Will NOT redact secrets automatically; stages are responsible for never using secrets as identifying keys

## User Stories

### Story 1: Application Developer - Seed application data alongside Identity data

**As an** application developer using NuxtIdentity
**I want** to define a demo user and that user's application data in the same seed group
**So that** related fixtures are configured together and created in a dependable order

**Acceptance Criteria**:

- [ ] A single named group can contain Identity keys (`Roles`, `Users`, ...) and application keys (for example `Lists`)
- [ ] Within a group, Identity stages run before application stages by default
- [ ] An application stage can resolve a user created earlier in the same group by username
- [ ] A group can be split across configuration files (for example Identity keys in one file and application keys in another) and is merged by standard `IConfiguration` rules

### Story 2: Application Developer - Write a new seed stage with minimal boilerplate

**As an** application developer
**I want** to implement only the logic that finds and creates my entities
**So that** I get ordering, failure isolation, logging, and summaries without reimplementing them

**Acceptance Criteria**:

- [ ] A stage implements one interface and registers with one call
- [ ] The engine provides a helper that binds each configured item to a typed options object, retaining its configuration path and capturing binding errors per item
- [ ] The engine provides an operation boundary that records succeeded, failed, and skipped outcomes and isolates exceptions
- [ ] A failure in an application stage does not prevent other items, stages, or groups from running

### Story 3: Application Developer - Catch configuration mistakes

**As an** application developer
**I want** to be warned when a group contains a key that no registered stage handles
**So that** typos and missing stage registrations are visible rather than silently ignored

**Acceptance Criteria**:

- [ ] Each stage declares the group keys it owns
- [ ] Startup fails if two registered stages claim the same key
- [ ] An unclaimed key in a group produces a warning naming the group and key path

### Story 4: Library Maintainer - Ship Identity seeding as an engine plugin

**As a** NuxtIdentity maintainer
**I want** Identity seeding to be an ordinary consumer of the engine
**So that** the engine is exercised by real stages and Identity seeding stays focused on Identity rules

**Acceptance Criteria**:

- [ ] `AddNuxtIdentitySeeding<TUser>()` registers the engine (if needed) and the Identity stages
- [ ] Existing Identity seeding behavior and tests are preserved, apart from the configuration section rename
- [ ] The engine package does not reference ASP.NET Core Identity or NuxtIdentity

### Story 5: Operator - Read one trustworthy seeding summary

**As an** operator starting the application
**I want** a single completion summary covering all seeded data
**So that** I can tell whether seeding fully or partially completed without correlating multiple logs

**Acceptance Criteria**:

- [ ] One summary reports succeeded, failed, and skipped counts across all stages and groups
- [ ] Partial completion is logged distinctly from full completion
- [ ] No log entry contains a password, invitation code, or other secret

---

## Overview

Split seeding into two layers:

1. **Seeding engine** (new package `NuxtIdentity.Seeding`): discovers named groups under the `Seeding` configuration section, orders them, runs registered stages for each group in stage order, provides per-item binding and operation boundaries, and logs the summary. It knows nothing about Identity.
2. **Stages**: domain-specific implementations of `ISeedStage`. NuxtIdentity ships six Identity stages in `NuxtIdentity.AspNetCore`. Applications register their own stages.

```csharp
builder.Services
    .AddNuxtIdentitySeeding<ApplicationUser>(builder.Configuration) // engine + Identity stages
    .AddSeedStage<ListsSeedStage>();                                // application stage

// After database creation/migration.
await app.Services.SeedAsync();
```

```json
"Seeding": {
  "Demo": {
    "Order": 10,
    "Users": [ { "UserName": "demo", "Email": "demo@example.com", "Roles": [ "User" ] } ],
    "Lists": [ { "Owner": "demo", "Name": "Groceries", "Items": [ "Milk", "Eggs" ] } ]
  }
}
```

## Design Decisions

- **Package boundary:** The engine lives in a new `src/Seeding/NuxtIdentity.Seeding.csproj` that depends only on `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.DependencyInjection.Abstractions`, and `Microsoft.Extensions.Logging.Abstractions`. `NuxtIdentity.AspNetCore` references it. Applications reference it directly only if they seed without Identity. The package can move to its own repository later without API changes if a non-NuxtIdentity consumer appears.
- **Configuration section:** Rename the root section from `IdentitySeeder` to `Seeding`. Group semantics are unchanged: immediate children are named groups, ordered by `Order` ascending (default `0`) then by `StringComparer.OrdinalIgnoreCase` group name. `Order` is reserved at the group level and owned by the engine.
- **Execution order:** Groups are the outer loop; stages are the inner loop. For each group in order, each stage runs in ascending `ISeedStage.Order`, with ties broken by ordinal stage name. This keeps "a group is a coherent fixture" as the mental model and lets application stages rely on Identity entities created earlier in the same group or in earlier groups.
- **Stage ordering bands:** `SeedStageOrder` constants define well-known bands: `Identity = 0` (Identity stages use 0–599, spaced 100 apart), and `Application = 1000` (default for application stages). Applications can choose values in between or after these bands when they need finer ordering.
- **Key ownership:** Each stage declares the group keys it owns (`ISeedStage.Keys`). The engine validates at first run that no key is claimed by more than one stage and that no stage claims `Order`; a collision is a setup error and fails startup. Keys in a group that no stage claims produce one warning per key path and are not counted as item failures. This allows a group to combine Identity and application data without hidden overlap.
- **Readability:** Flat top-level keys are kept (no `Identity:` sub-namespace) to stay concise. Readability for combined groups comes from configuration composition: one group may be defined across multiple files (for example `seed.identity.json` and `seed.lists.json`), and standard `IConfiguration` merging reassembles it. Documentation recommends this layout for larger fixtures.
- **Stage lifetime:** Stages are registered as scoped `ISeedStage` services. The runner resolves all stages from one async scope per run, so stages can depend on scoped services such as `DbContext` and Identity managers and share one unit of work across groups.
- **Create-only contract:** The engine cannot enforce create-only semantics. The `ISeedStage` documentation states the contract: find by a natural key, create when absent, never update or delete, treat an existing item as a success. Identity stages follow the existing rules in [DESIGN-SEEDING.md](./DESIGN-SEEDING.md).
- **Secret-safe identifying keys:** The identifying key passed to each operation is logged and must never be a secret. This is a documented, mandatory stage contract. When no non-secret key exists (for example, before an invitation is loaded), the stage passes `SeedKey.None` and the configuration path identifies the item. The engine never logs configuration values, exception messages, or exception stack traces; it logs only exception type names.
- **Startup API:** `SeedAsync` is an extension on `IServiceProvider` (`app.Services.SeedAsync()`) rather than `IApplicationBuilder`, so the engine has no ASP.NET Core dependency and can run in worker services and test hosts.
- **No hosted service:** As before, the consumer controls when seeding runs relative to migrations and database creation.

## Engine API

**Files**: `src/Seeding/` (new project)

### `ISeedStage`

```csharp
public interface ISeedStage
{
    /// Stable name used in logs and for ordering ties, for example "Identity.Roles".
    string Name { get; }

    /// Execution order within a group. See SeedStageOrder.
    int Order { get; }

    /// Group keys owned by this stage, for example ["Roles"].
    IReadOnlyCollection<string> Keys { get; }

    /// Seeds this stage's items for one group.
    Task SeedAsync(SeedGroupContext group, CancellationToken cancellationToken);
}
```

A stage may own multiple keys (for example, the Identity user-claims stage processes nested `Users[].Claims` and group-level `UserClaims`). A stage may read keys it does not own (the user-roles stage reads `Users[].Roles`), but only the owner's key participates in collision and unclaimed-key validation.

### `SeedGroupContext`

Passed to each stage for each group. Exposes:

- `string GroupName` and `string Path`: the group's name and configuration path.
- `IConfigurationSection Section`: the raw group section, for stages that need custom parsing.
- `IEnumerable<SeedItem<T>> Items<T>(string key) where T : new()`: binds each child of `Section.GetSection(key)` to `T` with `ConfigurationBinder`. Each `SeedItem<T>` carries `Value`, `Path`, and `Error`. A binding exception is caught for that item only, converted to a safe error message that names the path and target type but never the configured value, and returned as `Error` with a default `Value`. Item errors do not stop enumeration.
- `Task RunAsync(string itemKind, string path, string identifyingKey, Func<Task<SeedResult>> operation)`: the operation boundary (see below).
- `void RecordSkipped(string itemKind, string path, string identifyingKey, string reason)` and `void RecordFailed(...)`: record an outcome without running an operation, for preflight cases such as a missing optional service.

`SeedItem<T>` provides `RunAsync` overloads so stages can write:

```csharp
foreach (var item in group.Items<ListSeedOptions>("Lists"))
{
    await group.RunAsync(item, "list", item.Value.Name ?? SeedKey.None, async () =>
    {
        // item.Error is converted to SeedResult.Failure by the overload before this runs.
        var owner = await _users.FindByNameAsync(item.Value.Owner!);
        if (owner is null)
            return SeedResult.Skipped("Owner was not found.");

        if (await _db.Lists.AnyAsync(l => l.OwnerId == owner.Id && l.Name == item.Value.Name))
            return SeedResult.Success();

        _db.Lists.Add(new TodoList(owner.Id, item.Value.Name!));
        await _db.SaveChangesAsync();
        return SeedResult.Success();
    });
}
```

### `SeedResult`

Public replacement for the private `SeedOperationResult`: `Success()`, `Failure(string reason)`, `Skipped(string reason)`. Reasons are logged and therefore must not contain secrets or unsanitized external error text.

### Operation boundary

`RunAsync` moves the current `RunOperationAsync` logic into the engine, unchanged in behavior:

- Throws if cancellation is requested before or after the operation.
- Converts the returned `SeedResult` into a counter increment and a structured log entry with group name, stage name, item kind, identifying key, and configuration path.
- Rethrows `OperationCanceledException` when the run's token is canceled.
- Catches all other exceptions, counts a failure, and logs only the exception type name.

The run token is captured by the context, so stages do not pass it to `RunAsync`.

### Registration and startup

**Files**: `src/Seeding/SeedingServiceCollectionExtensions.cs`, `src/Seeding/SeedingServiceProviderExtensions.cs`

- `services.AddSeeding(IConfiguration configuration)`: registers the scoped `SeedRunner` and stores the configuration root. Idempotent (`TryAdd`), so library and application registration can both call it.
- `services.AddSeedStage<TStage>() where TStage : class, ISeedStage`: adds a scoped `ISeedStage` registration (`TryAddEnumerable`, so duplicate registration of the same type is ignored).
- `serviceProvider.SeedAsync(CancellationToken)`: creates an async scope, resolves `SeedRunner`, and runs it.

`AddSeeding` returns a small `SeedingBuilder` wrapping `IServiceCollection` so `AddSeedStage` can chain, and an `IServiceCollection` overload of `AddSeedStage` is also provided.

### `SeedRunner`

1. Resolve `IEnumerable<ISeedStage>` from the scope. Validate key ownership (duplicate keys or a stage claiming `Order` throws `InvalidOperationException` naming the stages and key). Sort stages by `Order`, then ordinal `Name`.
2. Read groups from `Seeding`, parse `Order` (an invalid `Order` records one group configuration failure and uses `0`, matching current behavior), and sort.
3. For each group: warn for each unclaimed key, then run each stage's `SeedAsync` with a `SeedGroupContext`. An exception escaping a stage's `SeedAsync` (outside `RunAsync`) is counted as one failure for that stage and group, logged with the exception type name, and does not stop later stages or groups. Cancellation propagates.
4. Log the completion or partial-completion summary.

No stages registered, or no `Seeding` section, completes successfully with zero counts and a debug log.

## Identity Stages

**Files**: `src/AspNetCore/Seeding/` (replaces `src/AspNetCore/Services/IdentitySeeder.cs`)

`IdentitySeeder<TUser>` is split into one stage per current seeding step. Behavior, validation, matching keys, and logging content follow [DESIGN-SEEDING.md](./DESIGN-SEEDING.md) exactly.

| Stage | Name | Order | Owned keys |
|---|---|---|---|
| `RoleSeedStage` | `Identity.Roles` | 0 | `Roles` |
| `UserSeedStage<TUser>` | `Identity.Users` | 100 | `Users` |
| `UserRoleSeedStage<TUser>` | `Identity.UserRoles` | 200 | (none; reads `Users[].Roles`) |
| `UserClaimSeedStage<TUser>` | `Identity.UserClaims` | 300 | `UserClaims` |
| `RoleClaimSeedStage` | `Identity.RoleClaims` | 400 | `RoleClaims` |
| `InvitationSeedStage` | `Identity.Invitations` | 500 | `Invitations` |

- Identity stages keep their existing hand-written parsing where it retains nested configuration paths (for example per-role paths inside `Users[].Roles`) that `Items<T>` cannot provide, and use `Items<T>` for flat items.
- The `Seed*Options` models move to `src/AspNetCore/Seeding/Options/` and lose the `IdentitySeederGroupOptions` aggregate. `ConfigurationPath` and `ConfigurationError` properties are removed where `SeedItem<T>` now carries them.
- `InvitationSeedStage` resolves `IInvitationService` optionally. When it is not registered and the group has invitations, the stage records each invitation as skipped through `RecordSkipped` and logs the "invitation service not registered" warning once per run (tracked by stage state; the stage is scoped to the run).
- `AddNuxtIdentitySeeding<TUser>(IConfiguration)` calls `AddSeeding(configuration)` and registers the six stages, returning the `SeedingBuilder`.
- `SeedNuxtIdentityAsync<TUser>` and `IdentitySeeder<TUser>` are removed. Consumers call `app.Services.SeedAsync()`.

## Logging

Engine log messages use `LoggerMessage` source generation in the `NuxtIdentity.Seeding` category, with fields `GroupName`, `StageName`, `ItemKind`, `IdentifyingKey`, and `ConfigurationPath`. Message events:

- Operation succeeded (Debug), skipped (Warning), failed (Warning), exception (Error with exception type name only)
- Group configuration invalid (Warning)
- Unclaimed group key (Warning)
- Stage exception outside an operation (Error)
- Seeding completed (Information) / partially completed (Warning)

Stage-specific messages, such as the invitation-service warning, remain in the stage's own logger category.

## Testing

**Engine** (new `tests/NuxtIdentity.Seeding.Tests`, NUnit, Gherkin-style Given/When/Then comments), using fake stages:

- Groups are ordered by `Order` then case-insensitive name; invalid `Order` is a counted failure and defaults to `0`.
- Stages run per group in `Order` then `Name`; all stages finish group N before group N+1 starts.
- Duplicate key ownership and claiming `Order` fail startup; unclaimed keys warn and are not counted.
- `Items<T>` returns paths for every item, isolates binding errors per item, and never includes configured values in errors.
- `RunAsync` counts success, failure, and skip; isolates exceptions; logs only exception type names; propagates cancellation.
- A stage throwing outside `RunAsync` is counted once and does not stop later stages or groups.
- The summary distinguishes complete from partial runs; empty configuration and no stages complete cleanly.
- `SeedAsync` creates and disposes one async scope; stages share that scope.
- Registration is idempotent for `AddSeeding` and for duplicate `AddSeedStage<T>` calls.

**Identity stages** (`tests/NuxtIdentity.AspNetCore.Tests`): port the existing `IdentitySeederTests` to drive the engine through `SeedAsync` with the `Seeding` section. All existing scenarios must pass unchanged in intent. Add:

- An application fake stage in the same group can resolve a user created by `Identity.Users` in that group.
- A group split across two configuration sources seeds both Identity and application keys.

## Migration and Documentation

- Rename `IdentitySeeder` to `Seeding` in configuration across the playground, samples, tests, and docs.
- Replace `SeedNuxtIdentityAsync<TUser>()` with `app.Services.SeedAsync()` in consumer startup.
- Update `src/AspNetCore/README.md` to describe Identity stages and link to a new `src/Seeding/README.md` that covers writing an application stage, ordering bands, key ownership, the create-only contract, and the rule that identifying keys and reasons must never contain secrets.
- Update [DESIGN-SEEDING.md](./DESIGN-SEEDING.md) and [PRD-SEEDING.md](./PRD-SEEDING.md) to reference the `Seeding` section and the new startup API, noting that the infrastructure now lives in the engine.

## Implementation Scope

- New `NuxtIdentity.Seeding` project and test project, added to the solution and to packaging alongside the existing packages.
- Refactor `IdentitySeeder<TUser>` into six Identity stages; remove the old seeder and startup extension.
- Port and extend tests as described above.
- Configuration rename and documentation updates. No schema or frontend changes.
