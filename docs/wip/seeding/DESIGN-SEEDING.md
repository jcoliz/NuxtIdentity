---
status: Approved
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

- `IdentitySeederGroupOptions`: `Order`, `Roles`, `Users`, `UserClaims`, `RoleClaims`, `Invitations`
- `SeedRoleOptions`: optional `Id`, required `Name`
- `SeedUserOptions`: required `UserName`; optional `Email`, `Password`; `EmailConfirmed` defaulting to `true`; `Roles` and `Claims`
- `SeedClaimOptions`: required `Type` and `Value`
- `SeedUserClaimsOptions`: required `UserName` and `Claims`
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
4. **User claims**: Process nested `Users[].Claims`, then group-level `UserClaims` entries through the same operation logic. Resolve each target by `UserName` using `FindByNameAsync` and read existing claims via `GetClaimsAsync`. A `UserClaims` entry does not require a `Users` entry and never creates or changes the user; an unavailable user is logged and skipped. Add each missing exact `(Type, Value)` pair using `AddClaimAsync`. An existing claim is a no-op across both configuration forms and groups; same type with another value is a distinct claim and may be added. Invalid usernames or claim fields fail only the affected claim operation, retaining its configuration path.
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

## Custom Seed Stages

Supports PRD Story 11. This is an interim extension point on the existing seeder. It keeps the current configuration section, seeding flow, and startup API, and lets applications seed their own data through the same infrastructure. The long-term direction is the standalone engine in [DESIGN-SEEDING-ENGINE.md](./DESIGN-SEEDING-ENGINE.md). The types below are a deliberate subset of that design, so stages written now can be moved to it with minimal changes.

**Files**: `src/Core/Abstractions/ISeedStage.cs`, `src/Core/Abstractions/SeedGroupContext.cs`, `src/Core/Abstractions/SeedItem.cs`, `src/Core/Abstractions/SeedResult.cs` (new); `src/AspNetCore/Services/IdentitySeeder.cs` and `src/AspNetCore/Extensions/IdentitySeederServiceCollectionExtensions.cs` (modified)

### Package placement

Stages are often implemented in application libraries that do not reference ASP.NET Core. The stage-facing types therefore live in `NuxtIdentity.Core`, namespace `NuxtIdentity.Core.Abstractions`, alongside the other consumer-implemented abstractions:

- `ISeedStage`, `SeedGroupContext`, `SeedItem<T>`, `SeedResult`, and the `SeedOutcome` enum that `SeedResult` exposes.
- These types use only the base class library. `NuxtIdentity.Core` gains no package references; in particular, it does not reference `Microsoft.Extensions.Configuration`.

The runtime pieces stay in `NuxtIdentity.AspNetCore`: the concrete `SeedGroupContext` implementation, configuration binding, the seeder, `AddSeedStage<TStage>`, and the startup extension. A stage library therefore references `NuxtIdentity.Core` only; the host application references `NuxtIdentity.AspNetCore` and registers the stage.

### Consumer usage

```csharp
builder.Services.AddNuxtIdentitySeeding<ApplicationUser>(builder.Configuration);
builder.Services.AddSeedStage<ListsSeedStage>();

// Unchanged.
await app.SeedNuxtIdentityAsync<ApplicationUser>();
```

```json
"IdentitySeeder": {
  "Demo": {
    "Users": [ { "UserName": "demo", "Email": "demo@example.com" } ],
    "Lists": [ { "Owner": "demo", "Name": "Groceries" } ]
  }
}
```

### Public types

```csharp
public interface ISeedStage
{
    /// Stable name used in logs and as the ordering tie-breaker, for example "Lists".
    string Name { get; }

    /// Execution order among custom stages within a group.
    int Order { get; }

    /// Seeds this stage's items for one group.
    Task SeedAsync(SeedGroupContext group, CancellationToken cancellationToken);
}
```

`SeedGroupContext` is a public abstract class, so Core has no dependency on configuration or on the seeder:

```csharp
public abstract class SeedGroupContext
{
    public abstract string GroupName { get; }
    public abstract string Path { get; }

    public abstract IEnumerable<SeedItem<T>> Items<T>(string key) where T : new();

    public abstract Task RunAsync(
        string itemKind, string path, string identifyingKey, Func<Task<SeedResult>> operation);

    public Task RunAsync<T>(
        SeedItem<T> item, string itemKind, string identifyingKey, Func<Task<SeedResult>> operation);

    protected static SeedItem<T> CreateItem<T>(T value, string path, string? error);
}
```

- `GroupName` and `Path`: the group's name and configuration path.
- `Items<T>(key)`: returns one `SeedItem<T>` for each child of the group's `key` subsection, bound to `T`. Each `SeedItem<T>` carries `Value`, `Path`, and `Error`. A binding exception affects only that item: `Error` is set to a safe message naming the configuration path and target type (never the configured value or the exception message), `Value` is a default `T`, and enumeration continues.
- `RunAsync(itemKind, path, identifyingKey, operation)`: runs one item operation with failure isolation, outcome counting, and structured logging.
- `RunAsync<T>(item, ...)`: non-virtual convenience overload implemented in Core. It uses `item.Path`, and calls the abstract `RunAsync` with an operation that returns `SeedResult.Failure(item.Error)` without invoking the stage's operation when the item has a binding error.
- `CreateItem<T>(value, path, error)`: protected factory that lets implementations in other assemblies, such as the seeder's, create `SeedItem<T>` instances.

The seeder supplies a private sealed implementation in `NuxtIdentity.AspNetCore`. It holds the group's `IConfigurationSection`, implements `Items<T>` with `ConfigurationBinder.Bind` and `CreateItem<T>`, and implements `RunAsync` by delegating to the existing private `RunOperationAsync` with the current group name, run summary, and run cancellation token, so outcomes are counted and logged exactly like built-in items.

`SeedItem<T>` is a public sealed class with public read-only `Value`, `Path`, and `Error` properties and an `internal` constructor `(T value, string path, string? error)`. Stages consume items but never construct them; outside Core, items are created only through `SeedGroupContext.CreateItem<T>`. No member is made public solely for testing.

`NuxtIdentity.Core.csproj` grants the test projects access to internals:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="NuxtIdentity.Core.Tests" />
  <InternalsVisibleTo Include="NuxtIdentity.AspNetCore.Tests" />
</ItemGroup>
```

Production assemblies, including `NuxtIdentity.AspNetCore`, are not granted access to Core internals.

`SeedResult` is the public counterpart of the private `SeedOperationResult`, with the same outcomes and counting rules: factories `Success()`, `AlreadyExists(reason)`, `Failure(reason)`, and `Skipped(reason)`, and read-only `Outcome` (`SeedOutcome`) and `Reason` properties so the seeder in another assembly can count and log the result. The seeder maps `SeedResult` to its internal outcome, or replaces `SeedOperationResult` with it; either is acceptable if behavior is unchanged.

Example stage, in an application library that references `NuxtIdentity.Core` but not ASP.NET Core:

```csharp
using NuxtIdentity.Core.Abstractions;

public sealed class ListsSeedStage(AppDbContext db) : ISeedStage
{
    public string Name => "Lists";
    public int Order => 0;

    public async Task SeedAsync(SeedGroupContext group, CancellationToken cancellationToken)
    {
        foreach (var item in group.Items<ListSeedOptions>("Lists"))
        {
            await group.RunAsync(item, "list", item.Value.Name ?? "(none)", async () =>
            {
                var owner = await db.Users.SingleOrDefaultAsync(
                    u => u.NormalizedUserName == item.Value.Owner!.ToUpperInvariant(), cancellationToken);
                if (owner is null)
                    return SeedResult.Skipped("Owner was not found.");

                if (await db.Lists.AnyAsync(l => l.OwnerId == owner.Id && l.Name == item.Value.Name, cancellationToken))
                    return SeedResult.AlreadyExists("List already exists.");

                db.Lists.Add(new TodoList(owner.Id, item.Value.Name!));
                await db.SaveChangesAsync(cancellationToken);
                return SeedResult.Success();
            });
        }
    }
}
```

### Seeder changes

- **Constructor:** `IdentitySeeder<TUser>` also receives `IEnumerable<ISeedStage>`. Stages resolve from the same scope as the seeder, so they share its `DbContext` and Identity managers.
- **Group record:** the private `SeedGroup` record gains the `IConfigurationSection` already available in `ReadGroups`. The seeder's `SeedGroupContext` implementation keeps it privately for `Items<T>` and does not expose it.
- **Flow:** after the six built-in stages in [Seeding Flow](#seeding-flow), each group runs every custom stage sorted by `Order`, then ordinal `Name`. Built-in stages always run first within a group; custom stages cannot be placed between them.
- **Stage exceptions:** an exception escaping a custom stage's `SeedAsync` (outside `RunAsync`) is counted as one failure for that stage and group. It is logged with group name, stage name, and exception type name only, and later stages and groups continue. `OperationCanceledException` propagates when the run token is canceled.
- **Summary:** custom stage outcomes share the existing counters and completion or partial-completion log.

### Registration

`AddSeedStage<TStage>(this IServiceCollection services) where TStage : class, ISeedStage` lives in `NuxtIdentity.AspNetCore` and is called by the host application. It registers `TStage` as a scoped `ISeedStage` with `TryAddEnumerable`, so registering the same type twice is ignored. It may be called before or after `AddNuxtIdentitySeeding<TUser>`. With no custom stages registered, seeding behaves exactly as before.

### Stage contract

These rules are documented on `ISeedStage`. The seeder cannot enforce them.

- **Create-only:** match by a natural key, create when absent, return `AlreadyExists` when present, and never update or delete.
- **No secrets in logs:** `identifyingKey`, `itemKind`, and `SeedResult` reasons are logged. They must never contain a password, invitation code, token, or other secret, or unsanitized external error text. When an item has no non-secret key, pass a placeholder; the configuration path identifies the item.
- **Reserved keys:** custom stages must not read or define their own items under `Order`, `Roles`, `Users`, `UserClaims`, `RoleClaims`, or `Invitations`. In this interim design, key collisions and unrecognized keys are not detected.
- **Own operations:** use `RunAsync` for every item so its failures are isolated and counted.

### Deferred to the engine design

The configuration section rename to `Seeding`, exposing the raw group `IConfigurationSection` for configuration shapes `Items<T>` does not cover (add it when a stage first needs one), declared key ownership with collision and unclaimed-key validation, ordering custom stages relative to built-in stages, splitting the built-in steps into stages, a separate package, and the `IServiceProvider` startup API.

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
- Custom stages: a stage runs after built-in stages in the same group and can resolve a user created there; multiple stages run by `Order` then `Name` within each group, before the next group; `Items<T>` returns paths and isolates binding errors without exposing configured values; `RunAsync` outcomes appear in the run summary; an exception inside `RunAsync` or escaping `SeedAsync` is isolated and logged by type name only; cancellation propagates; duplicate `AddSeedStage<T>` registration runs the stage once; with no stages registered, behavior is unchanged; `RunAsync<T>` with a binding-error item records a failure without invoking the operation (tested in `NuxtIdentity.Core.Tests` with a `SeedGroupContext` test double, using the internal `SeedItem<T>` constructor through `InternalsVisibleTo`).

## Implementation Scope

- New configuration option models, scoped seeder, and service/startup extensions in `NuxtIdentity.AspNetCore`.
- Use the existing `IInvitationService.CreateAsync` code parameter; no invitation API or schema change is required.
- Focused tests for binding, ordered create-only seeding, failure isolation, invitation behavior, and startup integration.
- Update `src/AspNetCore/README.md` with registration, startup ordering, invitation-first guidance, and secret handling. No schema migration or frontend changes.
- Custom seed stages: new public `ISeedStage`, `SeedGroupContext`, `SeedItem<T>`, `SeedResult`, and `SeedOutcome` types in `NuxtIdentity.Core.Abstractions` with no new Core package references, and `InternalsVisibleTo` entries for the Core and AspNetCore test projects in `NuxtIdentity.Core.csproj`; the seeder's `SeedGroupContext` implementation, `AddSeedStage<TStage>`, and seeder changes to run custom stages in `NuxtIdentity.AspNetCore`; tests as listed above; and a README section on writing a stage, including the stage contract.
