---
status: Implemented
prd: PRD-REGISTRATION.md
story: "13"
---

# Design Document: Registration — Story 13 (Test Invitation Support)

## Overview

This document describes the changes needed to implement Story 13 of the Registration feature: enabling consuming applications to create, manage, and clean up test invitations through `IInvitationService`.

Story 13 adds:
- An `IsTest` boolean property on `InvitationEntity` (replaces the `__TEST__` email prefix convention)
- A `CreateTestAsync` method on `IInvitationService` for creating test invitations with full property control
- A `DeleteTestInvitationsAsync` method on `IInvitationService` for bulk cleanup of test data
- Removal of the `status` parameter from the production `CreateAsync` method
- Migration of the email-match enforcement from `__TEST__` prefix to `IsTest` flag

---

## Motivation

Functional tests run on a separate machine and communicate with the backend over HTTP only. To set up invitation scenarios — pending, accepted, expired, revoked — the test runner needs an API that can:

1. Create invitations in any lifecycle state (not just Pending)
2. Set predictable codes for deterministic test references
3. Enforce email matching so test accounts don't leak into the wrong invitation
4. Clean up all test data after a test run without affecting production invitations

The current approach uses a `__TEST__` email prefix as a convention, but this is fragile — it relies on string matching and entangles production code with test concerns. A dedicated `IsTest` flag and separate creation method provide a cleaner, more reliable mechanism.

---

## Component Changes

### 1. InvitationEntity — Add IsTest Property

**File**: [`src/Core/Models/InvitationEntity.cs`](../../src/Core/Models/InvitationEntity.cs)

Add a new boolean property:

```csharp
/// <summary>
/// Gets or sets whether this invitation was created for testing purposes.
/// </summary>
/// <remarks>
/// Test invitations enforce email matching on signup and can be bulk-deleted
/// via <see cref="IInvitationService.DeleteTestInvitationsAsync"/>.
/// This flag is set automatically by <see cref="IInvitationService.CreateTestAsync"/>
/// and cannot be overridden by the caller.
/// </remarks>
public bool IsTest { get; set; }
```

This property:
- Defaults to `false` for production invitations
- Is auto-set to `true` by `CreateTestAsync` (the caller cannot control it)
- Powers `DeleteTestInvitationsAsync` (`WHERE IsTest = true`)
- Replaces the `__TEST__` email prefix for email-match enforcement in the controller

---

### 2. IInvitationService — Add Test Methods, Simplify CreateAsync

**File**: [`src/Core/Abstractions/IInvitationService.cs`](../../src/Core/Abstractions/IInvitationService.cs)

#### 2a. Remove `status` parameter from CreateAsync

The `status` parameter on `CreateAsync` was added only for testing. Now that test concerns move to `CreateTestAsync`, remove it:

```csharp
// Before
Task<InvitationEntity> CreateAsync(string? email = null, IReadOnlyList<string>? roles = null,
    IReadOnlyList<ClaimInfo>? claims = null, TimeSpan? expiresIn = null, string? metadata = null,
    InvitationStatus? status = null);

// After
Task<InvitationEntity> CreateAsync(string? email = null, IReadOnlyList<string>? roles = null,
    IReadOnlyList<ClaimInfo>? claims = null, TimeSpan? expiresIn = null, string? metadata = null);
```

Production invitations are always created with `Pending` status.

#### 2b. Add CreateTestAsync

```csharp
/// <summary>
/// Creates an invitation for testing purposes with full control over storable properties.
/// </summary>
/// <param name="invitation">
/// The invitation entity to persist. The caller may set any storable property including
/// <see cref="InvitationEntity.Code"/>, <see cref="InvitationEntity.Status"/>,
/// <see cref="InvitationEntity.Email"/>, roles, claims, metadata, and timestamps.
/// The <see cref="InvitationEntity.Id"/> and <see cref="InvitationEntity.IsTest"/> properties
/// are ignored — Id is auto-generated and IsTest is always set to true.
/// </param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="invitation"/> is null.</exception>
/// <exception cref="ArgumentException">Thrown when <see cref="InvitationEntity.Email"/> is null or empty.</exception>
Task<InvitationEntity> CreateTestAsync(InvitationEntity invitation);
```

The implementation:
- Forces `IsTest = true` regardless of what the caller passes
- Resets `Id` to 0 so the database auto-generates it
- Requires `Email` to be non-null/non-empty (throws `ArgumentException` otherwise)
- If `Code` is `Guid.Empty`, generates a new GUID; otherwise uses the caller's value
- If `CreatedAt` or `ExpiresAt` are `default(DateTime)`, fills them with sensible defaults (current UTC time and current + 30 days respectively)
- Persists all other properties as-is (Status, Roles, Claims, Metadata, AcceptedAt, AcceptedByUserId)

#### 2c. Add DeleteTestInvitationsAsync

```csharp
/// <summary>
/// Deletes all invitations marked as test invitations.
/// </summary>
/// <returns>The number of invitations deleted.</returns>
/// <remarks>
/// Only invitations with <see cref="InvitationEntity.IsTest"/> set to true are affected.
/// Production invitations are never deleted by this method.
/// </remarks>
Task<int> DeleteTestInvitationsAsync();
```

---

### 3. EfInvitationService — Implement New Methods

**File**: [`src/EntityFrameworkCore/Services/EfInvitationService.cs`](../../src/EntityFrameworkCore/Services/EfInvitationService.cs)

#### 3a. Update CreateAsync

Remove the `InvitationStatus? status = null` parameter. The entity always gets `Status = InvitationStatus.Pending`.

#### 3b. Implement CreateTestAsync

Validates the input per the rules in section 2b above, forces `IsTest = true` and `Id = 0`, applies defaults for empty Code/timestamps, then adds and saves the entity.

#### 3c. Implement DeleteTestInvitationsAsync

Queries all entities where `IsTest == true`, removes them, and returns the count. If using EF Core 7+, `ExecuteDeleteAsync` can be used for better performance (single SQL statement, no entity loading). The choice depends on the minimum EF Core version targeted by the project.

---

### 4. ModelBuilderExtensions — Configure IsTest Column

**File**: [`src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs`](../../src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs)

Add configuration for the new property in `ConfigureNuxtIdentityInvitations`:

```csharp
entity.Property(e => e.IsTest).IsRequired().HasDefaultValue(false);
entity.HasIndex(e => e.IsTest);
```

The index on `IsTest` supports efficient bulk deletion of test invitations.

---

### 5. Controller — Replace __TEST__ Prefix with IsTest Flag

**File**: [`src/AspNetCore/Controllers/NuxtAuthControllerBase.cs`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs)

In [`ValidateInvitationForSignup`](../../src/AspNetCore/Controllers/NuxtAuthControllerBase.cs:275), replace the `__TEST__` prefix check with an `IsTest` flag check. Since `CreateTestAsync` requires email to be non-null, `invitation.Email` is guaranteed non-null when `IsTest` is true.

---

### 6. Test Updates

#### 7a. Unit tests for EfInvitationService

**File**: New or extended tests in `tests/NuxtIdentity.EntityFrameworkCore.Tests/`

- `CreateTestAsync` with all properties set — verifies entity persisted correctly with `IsTest = true`
- `CreateTestAsync` with `Code = Guid.Empty` — verifies auto-generation
- `CreateTestAsync` with `Email = null` — verifies `ArgumentException`
- `CreateTestAsync` with default timestamps — verifies defaults applied
- `CreateTestAsync` ignores Id — verifies auto-generated
- `CreateTestAsync` forces IsTest — verifies IsTest is true even if caller sets false
- `DeleteTestInvitationsAsync` — deletes test invitations, leaves production invitations intact
- `DeleteTestInvitationsAsync` — returns correct count
- `DeleteTestInvitationsAsync` — returns 0 when no test invitations exist

#### 7b. Update existing integration tests

**File**: [`tests/NuxtIdentity.AspNetCore.Tests/Controllers/InvitationSignUpTests.cs`](../../tests/NuxtIdentity.AspNetCore.Tests/Controllers/InvitationSignUpTests.cs)

All existing tests that use `__TEST__` prefix emails need to be updated to use `CreateTestAsync` instead. The email-mismatch test (`SignUp_WithTestPrefixEmail_MismatchedEmail_Returns400`) should be renamed to reflect the new `IsTest` flag semantics (e.g., `SignUp_WithTestInvitation_MismatchedEmail_Returns400`).

#### 7c. Update existing CreateAsync calls that use status parameter

All calls to `CreateAsync(..., status: InvitationStatus.Accepted)` etc. in tests need to switch to `CreateTestAsync` with the status set on the entity.

---

## Decision: CreateTestAsync Takes InvitationEntity

Rather than having `CreateTestAsync` accept many individual parameters (code, email, status, roles, claims, metadata, expiresIn, createdAt, expiresAt, acceptedAt, acceptedByUserId), it accepts a pre-populated `InvitationEntity`. This:

- Avoids a method with 10+ parameters
- Lets the caller set exactly the properties they care about and leave the rest as defaults
- Makes it obvious which properties are "caller-settable" vs. "system-controlled" (Id, IsTest)
- Follows the pattern of EF Core seeding where you construct entities and hand them off

---

## Summary of Changes by File

| File | Change |
|------|--------|
| `src/Core/Models/InvitationEntity.cs` | Add `IsTest` property |
| `src/Core/Abstractions/IInvitationService.cs` | Remove `status` from `CreateAsync`; add `CreateTestAsync`, `DeleteTestInvitationsAsync` |
| `src/EntityFrameworkCore/Services/EfInvitationService.cs` | Implement new methods; update `CreateAsync` |
| `src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs` | Configure `IsTest` column and index |
| `src/AspNetCore/Controllers/NuxtAuthControllerBase.cs` | Replace `__TEST__` prefix with `IsTest` flag |
| `tests/NuxtIdentity.EntityFrameworkCore.Tests/` | Add unit tests for new methods |
| `tests/NuxtIdentity.AspNetCore.Tests/` | Update integration tests to use `CreateTestAsync` |
