# 27. Deployment and Production Readiness

## Objective

A production go-live checklist and Azure Container Apps deploy mechanics.

## Scope

Production checklist, migration execution, key/secret handling, private networking,
horizontal scale and reverse-proxy correctness.

## Architectural Decisions

- The API runs behind TLS-terminating infrastructure; app trusts proxy headers only from configured networks (`ForwardedHeadersOptions.KnownNetworks` — never blanket-trust).
- Two DB roles: migration role (DDL, used by bundle at deploy time) and runtime role (DML only) — schema changes are impossible through the app's connection.
- Deploy sequence: keep the current API image active → run the new image as a one-shot
  migration job → promote the new image → health gate. A failed readiness gate restores the
  previous image; migrations remain additive/expand-contract.

## Technology Decisions Requiring Approval

P14/P7 resolved by ADR-0027.

## Tasks

- [x] `Documentation/Operations/ProductionChecklist.md`: TLS + HSTS; trusted forwarded headers; Scalar disabled; dev seeder inert; DB roles split; signing/Data Protection keys; secrets; log/telemetry destination; backup/restore; rate limits; CORS; migration-first health-gated rollout. Every item names verification evidence.
- [x] `Documentation/Operations/Runbooks/MassRevocation.md`: incident procedure with exact transactional SQL for security stamps, sessions, refresh tokens and audit evidence.
- [x] `Documentation/Operations/Runbooks/KeyCompromise.md`: immediate retirement, replacement generation, verification and full-database escalation procedure.
- [x] Expand-contract migration and rollback policy in `Documentation/Operations/Migrations.md`.
- [x] Azure target: Bicep provisions ACR, Container Apps, migration job, private PostgreSQL,
  Managed Redis, Key Vault, managed identity, Log Analytics and Application Insights; CD is
  migration-first, readiness-gated and staging-ZAP protected.

**Current status (2026-08-02):** platform, identity, least-privilege database provisioning,
secret/key protection, scalable distributed state and workflows are implemented and Bicep
compiler-validated. Actual subscription rollout, GitHub Environment approvals, backup restore
and runbook rehearsal remain release evidence rather than code gaps.

## Expected Deliverables

Production checklist, two incident runbooks, migration policy and Azure deployment workflow.

## Dependencies

§8, §16, §24, §26; P14/P7 decisions.

## Security Considerations

The runbooks are the payoff of the architecture: because sessions are DB rows and keys are a DB ring, "log everyone out now" and "rotate keys now" are documented, tested procedures — not improvisation during an incident.

## Testing Requirements

Checklist items each verifiable (command or test named per item); runbook procedures rehearsed once against staging when it exists.

## Documentation Requirements

This workstream is primarily documentation; kept under `Documentation/Operations/`.

## Definition of Done

Checklist complete with verification per item; runbooks rehearsed; Azure target deployed and
health-gated.

## Questions for the Project Owner

1. ~~Deployment target?~~ Azure Container Apps selected in ADR-0027.
