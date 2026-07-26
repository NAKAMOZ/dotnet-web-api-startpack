# 29. Maintenance and Future Extensibility

## Objective

Keep the system healthy after v1: rotation cadences, upgrade procedures, and a groomed backlog of deliberately-deferred capabilities.

## Scope

Operational cadences, migration procedures for security parameters, future-work backlog with design notes.

## Architectural Decisions

- Signing-key rotation: quarterly cadence (plus on-demand via runbook §27); automated rotation job is a backlog item — v1 rotates via documented admin procedure.
- Argon2id parameter upgrades: bump options → `NeedsRehash` re-hashes on next successful login → metric `auth.password_hash_duration` confirms fleet migration; procedure documented.
- Dependency cadence: Dependabot/Renovate weekly; .NET SDK minor updates monthly; major .NET upgrades planned, not automatic.
- API evolution: additive within v1; breaking changes → `/api/v2` with `Sunset` header + deprecation notes in Scalar and `Documentation/`, minimum overlap window stated in policy doc.

## Technology Decisions Requiring Approval

None now; each backlog item re-enters the consultation process (per the project's technology-consultation rule) when picked up.

## Tasks

- [x] `Documentation/Operations/MaintenanceCadence.md`: rotation schedule, dependency cadence, parameter-upgrade procedure, version-deprecation policy.
- [x] `Documentation/FutureWork.md` — deferred items with design sketches, trigger conditions and security implications:
  - Organizations / multi-tenancy (owner-excluded from v1): org + membership entities, org-scoped roles, invitation flow.
  - M2M client-credentials flow (owner-excluded from v1): client registry, `client_credentials` grant, service tokens.
  - DB-driven permissions (replaces static map, §5) when roles need runtime editing.
  - Redis scale-out (P5/P6): distributed HybridCache backplane + rate-limit counters — trigger: second app node.
  - SPA-driven PKCE social flow (P13 deferred half).
  - `Idempotency-Key` support (§13) — trigger: any billing-like endpoint.
  - Automated key-rotation `BackgroundService`.
  - Webhooks / event notifications on auth events (Better Auth parity feature).
  - SCIM provisioning; WebAuthn conditional UI (passkey autofill) notes.
- [x] `Documentation/Operations/V1Closeout.md`: executable end-of-v1 review checklist covering §22, §23, ASVS, backup/restore, runbook rehearsal, delivery evidence and roadmap archive.

**Current status (2026-07-26):** all three maintenance/close-out artifacts are written and
the key manager has operator commands for healthy rotate/retire cadence. Approval, backlog
grooming and execution of the v1 close-out checklist necessarily wait for feature completion,
P7/P10/P14 and the project owner; the DoD is not yet claimed.

## Expected Deliverables

Maintenance cadence doc, future-work backlog, v1 close-out checklist.

## Dependencies

Everything prior; this workstream closes v1.

## Security Considerations

Deferred ≠ forgotten: every future item lists its security implication now (e.g. webhooks need signed payloads; M2M needs separate token audience) so later implementation starts from recorded intent.

## Testing Requirements

None beyond the close-out re-runs.

## Documentation Requirements

The two docs above; this roadmap archived as the v1 record once complete.

## Definition of Done

Cadence doc approved; backlog groomed with owner; v1 close-out checklist executed.

## Questions for the Project Owner

1. Review the future-work backlog: anything to promote into v1 or drop entirely?
