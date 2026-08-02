# v1 Close-Out Checklist

Do not archive `ROADMAP/` or label v1 complete until every item is checked with linked evidence.

## Scope and decisions

- [ ] Owner approves `Documentation/Scope.md`.
- [x] Pending decisions required by v1 are resolved or explicitly accepted as release gaps.
- [x] ADR index, architecture documents, OpenAPI, endpoint Markdown and `.http` examples agree.
- [ ] Future-work backlog is groomed: promoted items have owners; dropped items have rationale.

## Security and correctness

- [x] Run the complete unit, integration and `Category=Security` suites on the release commit
  (2026-08-02: 379/379 green; security traits are part of the integration run).
- [x] Re-run the ASVS checklist and close/accept every gap with owner and date (2026-08-02).
- [x] Run dependency vulnerability, secret and container-image scans (2026-08-02: live
  transitive NuGet and pnpm audits plus the repository secret-pattern scan are clean;
  digest-pinned Trivy 0.72.0 reports zero HIGH/CRITICAL findings, including unfixed findings,
  in the production image).
- [ ] Rehearse mass revocation and key-compromise runbooks against staging.
- [ ] Confirm production has no dev users, Scalar/OpenAPI or insecure cookies.

## Performance and resilience

- [ ] Run all §23 k6 scenarios on production-equivalent staging and record p50/p95/p99,
  throughput, errors, CPU, memory, DB pool and Argon2 duration.
- [ ] Re-tune budgets/rate limits from evidence and rerun the suite.
- [x] Prove readiness fails while PostgreSQL is unavailable and recovers without a process
  restart; prove liveness stays healthy (`PostgresInfrastructureTests`).
- [ ] Restore a production-shaped PostgreSQL backup and record RPO/RTO evidence.

## Delivery and operations

- [ ] All GitHub CI gates and branch rules are live on `main`.
- [x] Azure deployment is defined as code, migration-first, health-gated and automatically
  restores the previous image on a failed readiness gate.
- [x] Key Vault secret references and Data Protection key wrapping are implemented in IaC.
- [ ] Every cataloged trace/metric reaches Azure Monitor; dashboards and alerts are owned.
- [ ] Production checklist is signed for the release image digest and migration-job execution.

## Close

- [ ] Tag the immutable release and retain SBOM/build/migration evidence.
- [ ] Publish API support and v1 deprecation dates.
- [ ] Archive the roadmap as the v1 planning record; ADRs remain the durable decisions.
- [ ] Schedule the first weekly, monthly, quarterly and semi-annual maintenance events.
