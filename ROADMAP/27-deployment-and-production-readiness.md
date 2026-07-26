# 27. Deployment and Production Readiness

## Objective

A production go-live checklist and deploy mechanics — fully `Pending Decision` on target (P14), specified as target-agnostic requirements now.

## Scope

Production checklist, migration execution, key/secret handling, reverse-proxy correctness. Target-specific work starts after P14.

## Architectural Decisions

- The API runs behind TLS-terminating infrastructure; app trusts proxy headers only from configured networks (`ForwardedHeadersOptions.KnownNetworks` — never blanket-trust).
- Two DB roles: migration role (DDL, used by bundle at deploy time) and runtime role (DML only) — schema changes are impossible through the app's connection.
- Deploy sequence: run migration bundle → deploy new image → health-gate cutover. Rollback = previous image (migrations are additive/expand-contract by policy, documented).

## Technology Decisions Requiring Approval

P14 (target), P7 (vault). P17 is resolved by ADR-0020/0021; P7/P14 may supersede it.

## Tasks

- [x] `Documentation/Operations/ProductionChecklist.md`: TLS + HSTS; trusted forwarded headers; Scalar disabled; dev seeder inert; DB roles split; signing/Data Protection keys; secrets; log/telemetry destination; backup/restore; rate limits; CORS; migration-first health-gated rollout. Every item names verification evidence.
- [x] `Documentation/Operations/Runbooks/MassRevocation.md`: incident procedure with exact transactional SQL for security stamps, sessions, refresh tokens and audit evidence.
- [x] `Documentation/Operations/Runbooks/KeyCompromise.md`: immediate retirement, replacement generation, verification and full-database escalation procedure.
- [x] Expand-contract migration and rollback policy in `Documentation/Operations/Migrations.md`.
- [ ] After P14: target-specific deploy definition (infra-as-code or platform config) + enable CD stub (§26).

**Current status (2026-07-26):** target-agnostic production controls are implemented,
including fail-fast proxy trust configuration/tests and one-shot signing-key rotation
commands. P14 still blocks a real platform deployment, CD enablement and staging rehearsal;
P7/P14 still block Data Protection key-ring encryption at rest. The DoD is not claimed.

## Expected Deliverables

Production checklist, two incident runbooks, migration policy; post-P14: working deploy.

## Dependencies

§8, §16, §24, §26; P14/P7 decisions.

## Security Considerations

The runbooks are the payoff of the architecture: because sessions are DB rows and keys are a DB ring, "log everyone out now" and "rotate keys now" are documented, tested procedures — not improvisation during an incident.

## Testing Requirements

Checklist items each verifiable (command or test named per item); runbook procedures rehearsed once against staging when it exists.

## Documentation Requirements

This workstream is primarily documentation; kept under `Documentation/Operations/`.

## Definition of Done

Checklist complete with verification per item; runbooks rehearsed; P14 target deployed and health-gated (post-decision).

## Questions for the Project Owner

1. Deployment target (P14): container platform (Azure Container Apps / AWS ECS / Fly.io / k8s / bare VM)? This unblocks CD, vault (P7), and staging (§22 ZAP).
