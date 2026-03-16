---
status: Draft # Draft | In Review | Approved | Implemented
target_release: [Release Milestone when predominance of work is expected]
design_document: [Link to design document]
ado: [Link to ADO Item]
---

# Product Requirements Document: [Feature Name]

## Problem Statement

[1-3 sentences describing the problem this feature solves or the opportunity it addresses]

---

## Goals & Non-Goals

### Goals
- [ ] [Primary goal 1]
- [ ] [Primary goal 2]
- [ ] [Primary goal 3]

### Non-Goals
- [What this feature explicitly will NOT do]
- [Scope boundaries]

---

## User Stories

### Story 1: [Actor] - [Action]
**As a** [type of user]
**I want** [to perform some action]
**So that** [I can achieve some goal]

**Acceptance Criteria**:
- [ ] [Specific, testable criterion 1]
- [ ] [Specific, testable criterion 2]

### Story 2: [Actor] - [Action]
[Repeat pattern above]

---

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [ ] Frontend (Vue/Nuxt)
- [ ] Controllers (API endpoints)
- [ ] Application (Features/Business logic)
- [ ] Entities (Domain models)
- [ ] Database (Schema changes)

**High-Level Entity Concepts**:

**[EntityName] Entity** (new or modified):
- PropertyName (description - what it represents, required/optional)
- PropertyName (description)
- PropertyName (description)

[Add more entities as needed]

**Key Business Rules**:
1. **Rule Name** - Description of business rule that affects user experience
2. **Rule Name** - Description of business rule
3. [Add more business rules that belong in PRD scope]

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

- [ ] [Question 1 that needs resolution]
- [ ] [Question 2 that needs resolution]

---

## Success Metrics

[How will we know this feature is successful?]
- [Metric 1]
- [Metric 2]

---

## Dependencies & Constraints

**Dependencies**:
- [Other features or systems this depends on]

**Constraints**:
- [Technical, time, or resource constraints]

---

## Notes & Context

[Any additional context, links to related documents, or background information]

**Related Documents**:
- [Link to related PRDs]
- [Link to analysis documents]

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
