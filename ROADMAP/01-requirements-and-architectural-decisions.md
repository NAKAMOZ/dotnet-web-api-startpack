# 1. Requirements and Architectural Decisions

## Objective

Freeze the functional scope and record every architectural decision (approved and pending) so no later workstream has to guess.

## Scope

Decision log, scope statement, resolution of the 18 `Pending Decision` items on their blocking schedule.

## Architectural Decisions

- All approved decisions in the table at the top of this document are final.
- Decision records live in `Documentation/Decisions/` as lightweight ADRs (one file per decision, numbered `ADR-0001-token-strategy.md`, …) so the rationale survives beyond this roadmap.

## Technology Decisions Requiring Approval

P1 (session cap), P3 (additional directories), P4 (solution layout) — needed before §3 starts. **P2 (versioning) was answered in the same round**, since it blocks §3 on the same schedule.

**All four resolved 2026-07-22** — owner approved every recommendation as written:

| # | Decision | Recorded in |
|---|---|---|
| P1 | Absolute session cap = **7 days** | `ADR-0002` |
| P2 | **URL-segment versioning** `/api/v1/…` via `Asp.Versioning.Mvc` | `ADR-0015` |
| P3 | **All four** directories approved: `Validators/`, `Extensions/`, `Exceptions/`, `BackgroundServices/` | `ADR-0014` |
| P4 | **`src/Api/` + `tests/`**; root namespace becomes `Api` | `ADR-0014` |

## Tasks

- [x] Write `Documentation/Decisions/ADR-0001-token-strategy.md` through `ADR-0012-…` covering each row of the approved-decisions table (decision, context, alternatives considered, consequences).
- [x] Record owner's answers to P1, P3, P4 as ADRs and update this roadmap's tables.
- [x] Write `Documentation/Scope.md`: v1 feature list, explicit out-of-scope list (organizations/multi-tenancy, M2M client credentials, message broker).

### Mapping of approved-decision rows to ADRs

The approved-decisions table has **17 rows** but §1 allocates **12 ADR slots**, so related rows are grouped. Rule applied: a pending decision that *completes* an approved row folds into that row's ADR (P1 → `ADR-0002`); a pending decision with no corresponding row gets its own (P2, P3, P4).

| ADR | Covers |
|---|---|
| 0001 | Token strategy |
| 0002 | Session lifetime + Session model (**P1**) |
| 0003 | Token transport |
| 0004 | Signing keys |
| 0005 | User store |
| 0006 | Password hashing |
| 0007 | Runtime + API style |
| 0008 | Database |
| 0009 | Validation + Mapping |
| 0010 | Logging |
| 0011 | Testing + Containers/CI |
| 0012 | API documentation |
| *0013* | **reserved for §2** (package manifest) — deliberately skipped here |
| 0014 | **P3** + **P4** |
| 0015 | **P2** |

The 17th row, *v1 feature scope*, is a scope statement rather than an architectural decision and is covered by `Documentation/Scope.md`.

## Expected Deliverables

`Documentation/Decisions/ADR-*.md`, `Documentation/Scope.md`, updated `ROADMAP.md`.

**Delivered 2026-07-22:**

- `Documentation/Decisions/README.md` — ADR index and numbering rules (records that `ADR-0013` is reserved for §2).
- `Documentation/Decisions/ADR-0001` … `ADR-0012`, `ADR-0014`, `ADR-0015` — 14 files.
- `Documentation/Scope.md` — v1 in-scope capabilities with endpoint references, plus the deferred list with reasons.
- Roadmap tables updated: `README.md` status board, `00-overview.md` pending-decisions and directory-structure sections, this file.
- `CLAUDE.md` updated to point at `Documentation/Decisions/` as the durable decision record.

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

- [x] Every approved decision has an ADR — all 17 rows of the approved-decisions table map to `ADR-0001`–`ADR-0012` or to `Documentation/Scope.md`.
- [x] P1, P3, P4 answered and recorded (plus P2, answered in the same round).
- [ ] **`Documentation/Scope.md` reviewed by the owner** — the one outstanding item. §1 stays 🔄 in the status board until this is signed off.

## Questions for the Project Owner

1. ~~Absolute session cap: is **7 days** acceptable? (P1)~~ ✅ **Yes — 7 days**, approved 2026-07-22.
2. ~~Are the four proposed directories approved? (P3)~~ ✅ **Yes — all four.**
3. ~~Is the `src/` + `tests/` layout approved? (P4)~~ ✅ **Yes.**
4. ~~URL-segment versioning? (P2, asked alongside the above since it blocks §3 too)~~ ✅ **Yes — `/api/v1/…`.**

**Remaining:** review `Documentation/Scope.md` and confirm the in-scope / deferred split before §1 is marked ✅.
