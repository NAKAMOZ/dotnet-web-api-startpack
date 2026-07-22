# 1. Requirements and Architectural Decisions

## Objective

Freeze the functional scope and record every architectural decision (approved and pending) so no later workstream has to guess.

## Scope

Decision log, scope statement, resolution of the 18 `Pending Decision` items on their blocking schedule.

## Architectural Decisions

- All approved decisions in the table at the top of this document are final.
- Decision records live in `Documentation/Decisions/` as lightweight ADRs (one file per decision, numbered `ADR-0001-token-strategy.md`, …) so the rationale survives beyond this roadmap.

## Technology Decisions Requiring Approval

P1 (session cap), P3 (additional directories), P4 (solution layout) — needed before §3 starts.

## Tasks

- [ ] Write `Documentation/Decisions/ADR-0001-token-strategy.md` through `ADR-0012-…` covering each row of the approved-decisions table (decision, context, alternatives considered, consequences).
- [ ] Record owner's answers to P1, P3, P4 as ADRs and update this roadmap's tables.
- [ ] Write `Documentation/Scope.md`: v1 feature list, explicit out-of-scope list (organizations/multi-tenancy, M2M client credentials, message broker).

## Expected Deliverables

`Documentation/Decisions/ADR-*.md`, `Documentation/Scope.md`, updated `ROADMAP.md`.

## Dependencies

None — first workstream.

## Security Considerations

The decision log is itself a security artifact: revocation semantics, hashing parameters, and key-rotation policy must be traceable to an approved decision, not tribal knowledge.

## Testing Requirements

None (documentation workstream).

## Documentation Requirements

This workstream *is* documentation; ADR format kept to one page each.

## Definition of Done

Every approved decision has an ADR; P1/P3/P4 answered and recorded; scope document reviewed by owner.

## Questions for the Project Owner

1. Absolute session cap: is **7 days** acceptable? (P1)
2. Are the four proposed directories approved? (P3)
3. Is the `src/` + `tests/` layout approved? (P4)
