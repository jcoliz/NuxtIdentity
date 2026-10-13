# Identity Seeding

NuxtIdentity.AspNetCore provides a configuration-driven startup seeder for
ASP.NET Core Identity roles, users, user-role assignments, user claims, role
claims, and NuxtIdentity invitations. Define seed data in any
`IConfiguration` provider, then invoke the seeder after the database is ready.

The seeder is create-only and idempotent: it creates missing records and
associations, but never updates or deletes existing data.

## Requirements

- Register ASP.NET Core Identity for the application's user type and
  `IdentityRole`.
- Make the database schema available before seeding. The application owns
  database creation and migrations.
- Register `AddNuxtIdentitySeeding<TUser>` and invoke
  `SeedNuxtIdentityAsync<TUser>` explicitly during startup.
- Register `IInvitationService` if invitation seeding is required. For the
  Entity Framework Core integration, `AddNuxtIdentityEntityFramework<TContext>`
  registers it, provided the context is configured for invitations.
- Optionally, register custom seed stages with `AddSeedStage<TStage>` to seed
  application data from the same groups. See
  [Writing a custom seed stage](#writing-a-custom-seed-stage).

## Register and run the seeder

Register the seeder alongside Identity and the other NuxtIdentity services:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
builder.Services.AddNuxtIdentitySeeding<ApplicationUser>(builder.Configuration);

// Optional: seed application data alongside Identity data.
builder.Services.AddSeedStage<ListsSeedStage>();
var app = builder.Build();

// Complete migrations/database creation before running the seeder.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

await app.SeedNuxtIdentityAsync<ApplicationUser>();

app.MapControllers();
app.Run();
```

If the application uses `EnsureCreatedAsync` instead of migrations, complete
that operation before calling `SeedNuxtIdentityAsync`. Do not use
`EnsureCreatedAsync` for a database managed by EF Core migrations.

The seeder is scoped to match Identity managers and EF-backed services. The
startup extension creates and disposes an asynchronous scope. Pass a
`CancellationToken` to `SeedNuxtIdentityAsync` when startup cancellation needs
to be observed.

## Configuration structure

`IdentitySeeder` is a section containing any number of named groups. The group
names below are conventions, not reserved names. Each group accepts an optional
integer `Order` and the same seed sections:

| Property | Purpose |
| --- | --- |
| `Order` | Optional group priority; lower numbers run first and the default is `0`. |
| `Roles` | Roles to create, optionally with fixed GUID IDs. |
| `Users` | Users to create, with optional password, role assignments, and claims. |
| `UserClaims` | Claims to add to users identified by `UserName`, without redefining them. |
| `RoleClaims` | Claims to add to named roles. |
| `Invitations` | Invitations to create with a supplied GUID code. |

Groups are processed by ascending `Order`, then by group name using ordinal,
case-insensitive ordering. The seeder processes each group in this order:
roles, users, user-role assignments, user claims, role claims, invitations,
and then any registered custom seed stages.
A group can reference data already in the database or created by an earlier
group; it does not wait for dependencies defined only in a later group.

The following JSON illustrates independent application-wide, environment, and
deployment contributions:

```json
{
  "IdentitySeeder": {
    "Application": {
      "Order": 0,
      "Roles": [
        {
          "Name": "User",
          "Id": "f7260efe-a5f8-4a5a-850f-a1f2f8725c78"
        },
        {
          "Name": "Administrator",
          "Id": "2722e8dc-2aae-4563-aafd-5320b807ea91"
        }
      ],
      "Users": [],
      "RoleClaims": [],
      "Invitations": []
    },
    "Environment": {
      "Order": 100,
      "Roles": [
        {
          "Name": "Functional Tests"
        }
      ],
      "Users": [],
      "RoleClaims": [
        {
          "Role": "Functional Tests",
          "Claims": [
            {
              "Type": "permission",
              "Value": "manage-users"
            }
          ]
        }
      ],
      "Invitations": [
        {
          "Code": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Email": "tester@example.com",
          "Roles": [ "Functional Tests" ],
          "Claims": [
            {
              "Type": "department",
              "Value": "testing"
            }
          ],
          "ExpiresIn": "30.00:00:00"
        }
      ]
    },
    "Deployment": {
      "Order": 200,
      "Roles": [],
      "Users": [],
      "RoleClaims": [],
      "Invitations": [
        {
          "Code": "7fc2ff35-e15d-42c1-98fa-1b0a729cfef2",
          "Email": "operator@example.com",
          "Roles": [ "Administrator" ],
          "ExpiresIn": "7.00:00:00"
        }
      ]
    }
  }
}
```

The same values can come from JSON, TOML, environment variables, or other
configuration providers. For example, if a TOML provider is added by the
application, a deployment contribution can be written as:

```toml
[IdentitySeeder.Deployment]
Order = 200

[[IdentitySeeder.Deployment.Invitations]]
Code = "7fc2ff35-e15d-42c1-98fa-1b0a729cfef2"
Email = "operator@example.com"
Roles = ["Administrator"]
ExpiresIn = "7.00:00:00"
```

The library does not add a TOML provider. The consuming application is
responsible for registering any non-default configuration providers.

### Supported fields

- **Roles:** `Name` is required. `Id` is optional and must be a non-empty GUID if
  provided. The configured ID is used only when the role is created and is
  stored in canonical GUID format.
- **Users:** `UserName` is required. `Email` and `Password` are optional.
  `EmailConfirmed` defaults to `true`. `Roles` is a list of role names;
  `Claims` is a list of objects with required `Type` and `Value`.
- **Role claims:** `Role` names the target role. `Claims` contains objects with
  required `Type` and `Value`.
- **Invitations:** `Code` is required and must be a non-empty GUID. `Email`,
  `Roles`, `Claims`, and `Metadata` are optional. `ExpiresIn` is an optional
  non-negative .NET `TimeSpan`, for example `"30.00:00:00"` for 30 days.
  Newly created invitations are ordinary Pending invitations.

### Independent user claims

Each group can supply `UserClaims` independently of `Users`. `UserName` is
required and resolves the target through ASP.NET Identity's username lookup.
`Claims` contains objects with required `Type` and `Value`. For example, place
this inside any seed group:

```json
{
  "UserClaims": [
    {
      "UserName": "Tester",
      "Claims": [
        {
          "Type": "entitlement",
          "Value": "f6a1c48d-959b-4cc1-ac0c-e92e4cf49133:Read:Write:Manage"
        }
      ]
    }
  ]
}
```

The user must already exist or be created in the same or an earlier group.
A missing user is logged and skipped; this section never creates accounts or
changes user properties. Nested `Users[].Claims` remains supported and is
processed first. Both forms add only missing exact `(Type, Value)` pairs,
including when the same claim appears in both forms or in multiple groups.
Invalid usernames or claim fields are logged individually without preventing
other claims from being seeded.

## Create-only behavior

| Configured data | Existing database data | Result |
| --- | --- | --- |
| Role by name | Role with that name | No changes, including when the configured ID differs. |
| User by username | User with that username | No changes to email, confirmation state, or password. |
| User-role assignment | Same user already has the role | No-op; existing memberships are not removed. |
| Claim by exact type and value | Same claim exists for the user or role | No-op. A different value for the same type is a separate claim. |
| Invitation by code | Invitation exists in any lifecycle state | No changes to fields or status. |

When an entity is missing, the first group in execution order that defines it
provides the initial fields. Later groups do not update it. A later group may
still contribute a distinct missing role assignment or claim. Re-running the
seeder does not duplicate successful operations.

## Onboarding with invitations

Prefer seeding roles and invitations rather than passwords. An invitation can
carry the intended roles and claims; the invitee supplies their own password
through the existing invitation-claim registration flow. Invitation codes are
predictable GUIDs supplied by configuration, so tests or manual workflows can
refer to them.

Invitation seeding uses the configured code to look up an existing invitation
and calls `IInvitationService.CreateAsync` only when that code is absent. It
does not use the test-invitation API. Existing Pending, Accepted, Expired, and
Revoked invitations remain unchanged. Empty GUID role IDs and invitation codes
are placeholder values: they are reported as failures before any lookup or
creation, even if the role already exists or the invitation service is absent.
Replace these placeholders with non-empty GUIDs before seeding.

If invitation entries are configured but `IInvitationService` is not
registered, the seeder emits one warning and counts entries with valid codes as
skipped; invalid or empty GUID codes still fail validation.
Role and user seeding continues.

## Optional direct user seeding and secrets

Direct user seeding remains available for fixtures or other cases that need a
pre-created account:

```json
{
  "IdentitySeeder": {
    "Application": {
      "Users": [
        {
          "UserName": "FixtureUser",
          "Email": "fixture@example.com",
          "Roles": [ "Functional Tests" ]
        }
      ]
    }
  }
}
```

If a password is explicitly needed, inject it from a secret provider rather
than committing it in configuration. .NET environment variables use double
underscores to represent configuration path separators:

```text
IdentitySeeder__Application__Users__0__Password=<password-from-secret-provider>
```

The seeder applies a configured password only when creating a user and passes
it through ASP.NET Core Identity password validation. It never resets an
existing user's password. Passwords and invitation codes are not included in
seeder diagnostics.

## Configuration layering

Standard .NET configuration precedence applies: later providers override
matching keys. The seeder does not append arrays or implement custom array
replacement. Arrays use numeric child keys; providers that target the same
array path can collide by index or leave earlier entries in place. Use distinct
group names for independent contributions, and target the same group only when
key-level overrides are intended (for example, injecting a password for an
existing user entry).

## Failures and diagnostics

Invalid entries, failed Identity results, and exceptions during individual
operations are reported and do not stop unrelated seed operations. If a
required user or role is unavailable, only dependent assignments or claims
are skipped with a warning. Existing roles, users, role assignments, claims,
and invitations are logged as `Exists` at Information level with an
already-exists reason, and are not counted as succeeded. A completion summary
reports succeeded, failed, and skipped operations. Failures and warning-level
skips signal partial completion; skips for existing data do not.

`UserManager<TUser>` and `RoleManager<IdentityRole>` must be registered; missing
required Identity services fail setup clearly. Cancellation is honored and
propagated rather than counted as an item failure. Seeding does not roll back
successful operations when another item fails, and individual item failures do
not prevent application startup.

Structured diagnostics include the group, configuration path, operation type,
and safe identifying data. Passwords and invitation codes are excluded. For
troubleshooting, use the configuration path and operation type shown in the
log rather than adding secret values to logs.

Custom seed stage items use the same diagnostics and summary counts as
built-in items. An exception that escapes a stage's `SeedAsync` is counted as
one failure and logged as a `Stage error` with the group, stage name, and
exception type only; the seeder then continues with the next stage.

## Writing a custom seed stage

A seed stage seeds application data, such as lists or sample records, from the
same named groups as Identity data. Stages run inside each group after the
built-in Identity steps, so a stage can rely on users and roles created earlier
in the same group, or in an earlier group. Stages get the same ordering,
create-only semantics, failure isolation, and diagnostics as built-in items.

### Referencing the stage types

The stage types live in `NuxtIdentity.Core.Abstractions`, so a stage can be
implemented in a project that references only `NuxtIdentity.Core` and does not
depend on ASP.NET Core:

| Type | Purpose |
| --- | --- |
| `ISeedStage` | The stage: `Name`, `Order`, and `SeedAsync(ISeedGroupContext, CancellationToken)`. |
| `ISeedGroupContext` | The current group: `GroupName`, `Path`, `Items<T>(key)`, and `RunAsync(...)`. |
| `SeedItem<T>` | One bound configuration item (a record): `Value`, `Path`, and `Error`. `ToString()` omits `Value`. |
| `SeedResult` | An item outcome: `Success()`, `AlreadyExists(reason)`, `Skipped(reason)`, or `Failure(reason)`. |

Only the application's startup project needs `NuxtIdentity.AspNetCore`, to
register the stage with `AddSeedStage<TStage>()`.

### Example

The following stage seeds lists owned by seeded users. Each group may contain
a `Lists` key:

```json
{
  "IdentitySeeder": {
    "Demo": {
      "Order": 100,
      "Users": [
        { "UserName": "demo", "Email": "demo@example.com" }
      ],
      "Lists": [
        { "Name": "Groceries", "Owner": "demo" },
        { "Name": "Chores", "Owner": "demo" }
      ]
    }
  }
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Abstractions;

public sealed class ListSeedOptions
{
    public string? Name { get; set; }
    public string? Owner { get; set; }
}

public sealed class ListsSeedStage(ApplicationDbContext db) : ISeedStage
{
    public string Name => "Lists";
    public int Order => 0;

    public async Task SeedAsync(ISeedGroupContext context, CancellationToken cancellationToken)
    {
        foreach (var item in context.Items<ListSeedOptions>("Lists"))
        {
            var options = item.Value;
            await context.RunAsync(item, "list", options.Name ?? "(unnamed)", async () =>
            {
                if (string.IsNullOrWhiteSpace(options.Name))
                    return SeedResult.Failure("Name is required.");

                var owner = await db.Users.SingleOrDefaultAsync(
                    user => user.UserName == options.Owner, cancellationToken);
                if (owner is null)
                    return SeedResult.Skipped("Owner was not found.");

                var exists = await db.Lists.AnyAsync(
                    list => list.OwnerId == owner.Id && list.Name == options.Name, cancellationToken);
                if (exists)
                    return SeedResult.AlreadyExists("List already exists.");

                db.Lists.Add(new TodoList { OwnerId = owner.Id, Name = options.Name });
                await db.SaveChangesAsync(cancellationToken);
                return SeedResult.Success();
            });
        }
    }
}
```

Register it in the startup project:

```csharp
builder.Services.AddNuxtIdentitySeeding<ApplicationUser>(builder.Configuration);
builder.Services.AddSeedStage<ListsSeedStage>();
```

Stages are registered as scoped services, so they can inject a `DbContext` or
other scoped services. Registering the same stage type more than once has no
effect.

### Binding and running items

`Items<T>(key)` binds each child of the group's `key` subsection to `T`. A
missing key yields no items. If an item can't be bound, for example because a
number field contains text, the item is returned with a default `Value` and
`Error` set; enumeration continues with the next item. Binding errors never
include the configured value.

`RunAsync(item, itemKind, identifyingKey, operation)` runs one item's
operation and records its outcome. If the item has a binding error, the
operation is not invoked and the item is recorded as failed. For data that
doesn't come from `Items<T>`, use the
`RunAsync(itemKind, path, identifyingKey, operation)` overload with your own
configuration path.

Choose the result that describes what happened:

| Result | Use when | Counted as |
| --- | --- | --- |
| `SeedResult.Success()` | The item was created. | Succeeded |
| `SeedResult.AlreadyExists(reason)` | The item already exists; nothing changed. | Skipped (not a partial run) |
| `SeedResult.Skipped(reason)` | A dependency is missing, so the item can't be created. | Skipped (partial run) |
| `SeedResult.Failure(reason)` | The configuration is invalid or creation failed. | Failed |

An exception thrown by `operation` is counted as a failure for that item and
logged by exception type only. Seeding then continues.

### Run order

Within each group, built-in Identity steps run first, then stages run by
ascending `Order`, then by `Name` using ordinal comparison. Every stage runs
for every group, including groups that have no data for it. A group's stages
finish before the next group begins.

### Stage contract

- **Create-only and idempotent.** Create missing data; never update or delete
  existing data. Return `AlreadyExists` when the item is already present, so
  re-running the seeder makes no changes.
- **Wrap each item in `RunAsync`.** This isolates failures, counts outcomes,
  and logs diagnostics consistently with built-in items.
- **Never log or return secrets.** `itemKind`, `identifyingKey`, and reasons
  are logged. Use a non-secret key, such as a name, never a password, token,
  or invitation code.
- **Use your own keys.** Don't read the built-in keys: `Order`, `Roles`,
  `Users`, `UserClaims`, `RoleClaims`, and `Invitations`. Pick key names that
  won't collide with other stages.
- **Honor cancellation.** Pass the `CancellationToken` to asynchronous calls.
  Cancellation stops seeding and is not counted as a failure.
- **Handle a missing key.** A group without the stage's key has no items for
  it; the stage should do nothing.
