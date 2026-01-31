---
status: Approved
prd: PRD-TOKEN-GUIDS.md
---

# Design Document: Token GUID Keys

## Overview

This document describes the changes needed to add a GUID key to refresh tokens for safe logging. The key will be prepended to the opaque token in the format `{Key}.{Secret}`, allowing distributed tracing without exposing credentials.

---

## Component Changes

### 1. RefreshTokenEntity

**File**: [`src/Core/Models/RefreshTokenEntity.cs`](../../src/Core/Models/RefreshTokenEntity.cs)

Add a new `Key` property of type `Guid` to store the token identifier. This GUID will be globally unique and used for both logging and database lookups.

```csharp
public Guid Key { get; set; }
```

---

### 2. ModelBuilderExtensions

**File**: [`src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs`](../../src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs)

Update [`ConfigureNuxtIdentityRefreshTokens`](../../src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs:32) to:
- Add a unique index on the `Key` column for fast lookups
- Mark `Key` as required

The existing `TokenHash` index can remain for backward compatibility during transition, but the primary lookup will shift to use `Key`.

---

### 3. InMemoryRefreshTokenService

**File**: [`src/Core/Services/InMemoryRefreshTokenService.cs`](../../src/Core/Services/InMemoryRefreshTokenService.cs)

#### GenerateRefreshTokenAsync (line 33)

Currently generates a token and returns it directly. Change to:
- Generate a new GUID for the key
- Store the key in the entity
- Return the composite token: `{Key}.{Secret}`

#### ValidateRefreshTokenAsync (line 62)

Currently hashes the entire token and searches by hash. Change to:
- Parse the incoming token to extract the key (first 36 characters, or split on first period)
- Look up the entity by key instead of by hash
- If no entity found with that key, return null (handles old-format tokens gracefully)
- Hash only the secret portion and compare against stored hash

#### RevokeRefreshTokenAsync (line 89)

Currently searches by hash. Change to:
- Parse the key from the token
- Look up by key
- If not found, do nothing (no error logging for old-format tokens)

---

### 4. EfRefreshTokenService

**File**: [`src/EntityFrameworkCore/Services/EfRefreshTokenService.cs`](../../src/EntityFrameworkCore/Services/EfRefreshTokenService.cs)

Same logical changes as InMemoryRefreshTokenService, plus logging enhancements.

#### GenerateRefreshTokenAsync (line 56)

Change to:
- Generate a GUID key
- Store key in entity
- Return composite token `{Key}.{Secret}`
- Update log messages to include the key (not the secret)

#### ValidateRefreshTokenAsync (line 94)

Change to:
- Parse key from token
- Query by key: `FirstOrDefaultAsync(t => t.Key == key)`
- If entity not found, return null without warning (old tokens)
- Validate the secret portion against stored hash
- Log using key instead of full token

#### RevokeRefreshTokenAsync (line 128)

Change to:
- Parse key from token
- Query by key
- If not found, return silently (old tokens)
- Log using key

#### Logger Messages (line 230+)

Update all logger messages that currently log `token` to instead log `key`. The parameter name should change from `token` to `key` to make it clear these are safe to log.

---

## Token Parsing Helper

Add a private helper method to both services for parsing the composite token:

```csharp
private static bool TryParseToken(string token, out Guid key, out string secret)
```

This method will:
- Attempt to parse a GUID from the first 36 characters
- If successful, extract the secret (everything after the period)
- Return false if the token doesn't match the expected format

This handles old-format tokens gracefully by returning false, allowing the caller to return null/fail validation without logging errors.

---

## Database Migration

Consumers of the library will need to create a migration to add the `Key` column. The design document should note:

1. Add `Key` column as `uniqueidentifier` (SQL Server) or equivalent
2. Create unique index on `Key`
3. Existing rows will have empty GUIDs - these tokens will fail validation (acceptable per PRD)

---

## Backward Compatibility

- **Old tokens in database**: Will fail validation silently (no key to look up)
- **Interface unchanged**: `IRefreshTokenService` methods remain the same
- **Token format change**: Consumers treating tokens as opaque strings are unaffected

---

## Testing Considerations

Existing tests should continue to pass with the new token format since they treat tokens as opaque strings. New tests should verify:

1. Token format includes GUID prefix
2. Lookup by key works correctly
3. Old-format tokens fail gracefully without errors
4. Key is logged instead of secret

---

## Files to Modify

| File | Type of Change |
|------|----------------|
| `src/Core/Models/RefreshTokenEntity.cs` | Add `Key` property |
| `src/Core/Services/InMemoryRefreshTokenService.cs` | Token generation/parsing |
| `src/EntityFrameworkCore/Services/EfRefreshTokenService.cs` | Token generation/parsing + logging |
| `src/EntityFrameworkCore/Extensions/ModelBuilderExtensions.cs` | Add Key index |

---

## Implementation Order

1. Add `Key` property to `RefreshTokenEntity`
2. Update `ModelBuilderExtensions` with index configuration
3. Add token parsing helper to both services
4. Update `GenerateRefreshTokenAsync` in both services
5. Update `ValidateRefreshTokenAsync` in both services
6. Update `RevokeRefreshTokenAsync` in both services
7. Update logger messages in `EfRefreshTokenService`
8. Update/add unit tests
