---
status: In Review # Draft | In Review | Approved | Implemented
design_document: DESIGN-TOKEN-GUIDS.md
ado: [Link to ADO Item]
---

# Product Requirements Document: Token GUID keys

## Problem Statement

I need to debug the lifetime of refresh tokens across the frontend and backend. In order to do so, I need to add extensive logging. But I
shouldn't log the refresh tokens themselves because they are credentials.

---

## Goals & Non-Goals

### Goals
- [ ] Enable distributed trace logging of refresh tokens without compromising user security

### Non-Goals
- Will NOT encrypt or obfuscate the GUID key (it's intentionally visible for logging)
- Will NOT provide backward compatibility for old-format tokens (migration invalidates them)
- Will NOT add token tracking/analytics beyond logging (this is purely for debugging)
- Will NOT change the token secret generation algorithm

---

## User Stories

### Story 1: Developer - Logs Refresh token
**As an** Application Developer
**I want** to log refresh token on frontend and backend
**So that** I can debug cross-system refresh token problems

**Acceptance Criteria**:
- [ ] Tokens contain a safe GUID key for logging
- [ ] NuxtIdentity logs its interactions with refresh tokens using these keys

---

## Technical Approach

My plan is to add a GUID key to the opaque refresh token returned to the frontend. The frontend doesn't parse the refresh token, so it's not
affected.

After this change, a token will take the form: `{Key}.{Secret}`, where the
Secret is the **current** base64 token, and the Key is a new GUID.

I can safely log this GUID everywhere to trace token flow.

The GUID will be added to the stored token.

As a side benefit, we can use this key as a database lookup when validating refresh tokens.

Unit tests will need updating!

**Layers Affected**:
- [X] Frontend (Vue/Nuxt): Can now safely log the key portion of the token
- [ ] Controllers (API endpoints)
- [X] Application (Features/Business logic): Implemented in classes which implement IRefreshTokenService
- [X] Entities (Domain models)
- [X] Database (Schema changes): Add Key:guid field, and index. Users will  need a migration.

**High-Level Entity Concepts**:

**Refresh Token Entity** (modified):
- Key:guid (token identifier)

---

## Open Questions

- [X] Token Format Parsing: The new format {Key}.{Secret} uses a period as delimiter. Have you considered edge cases where the secret itself might contain a period? Should the parsing logic split on the first period only? **A** Secrets are base64, which do not user period. But yes, the parsing logic can split on first period, In fact, GUID size is fixed, so could simple take first characters and expect a GUID.

- [X] Migration Strategy: For existing tokens in the database that don't have a Key field: Will they be invalidated (forcing users to re-authenticate)? **A** YES, current refresh tokens will be invalidated, however, we should not log a error or warning for these cases.

- [X] Key Uniqueness: Should the Key be globally unique across all users, or unique per user? This affects the database index strategy. **A** Has to be unique across users, because we don't have the user ID at the time of lookup.

- [X] Logging Scope: The PRD mentions NuxtIdentity will log interactions. Should specific log levels be defined (e.g., Debug for routine operations, Information for token creation/rotation)? **A** We can follow current patterns. Typically we only log one information per method, which is the "OK" log at the end, describing what was completed by the method.

---

## Success Metrics

- Developers quickly solve refresh token bugs
- No security incidents caused by logged token information
- Token validation performance improves due to GUID-based lookup (measurable in database query time)

---

## Dependencies & Constraints

**Constraints**:
- Must maintain backward compatibility with the service interface (or version bump)
- GUID parsing must be performant (use Guid.TryParse with span-based parsing if available)
- Database index on Key column is required for performance

---

## Notes & Context

Current implementation in InMemoryRefreshTokenService
Current implementation in EfRefreshTokenService

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
