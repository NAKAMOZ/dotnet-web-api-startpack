# v1 Close-Out Checklist

Do not archive `ROADMAP/` or label v1 complete until every item is checked with linked evidence.

## Scope and decisions

- [ ] Owner approves `Documentation/Scope.md`.
- [ ] Pending decisions required by v1 are resolved or explicitly accepted as release gaps.
- [ ] ADR index, architecture documents, OpenAPI, endpoint Markdown and `.http` examples agree.
- [ ] Future-work backlog is groomed: promoted items have owners; dropped items have rationale.

## Security and correctness

- [ ] Run the complete unit, integration and `Category=Security` suites on the release commit.
- [ ] Re-run the ASVS checklist and close/accept every gap with owner and date.
- [ ] Run dependency vulnerability, secret and container-image scans.
- [ ] Rehearse mass revocation and key-compromise runbooks against staging.
- [ ] Confirm production has no dev users, Scalar/OpenAPI or insecure cookies.

## Performance and resilience

- [ ] Run all §23 k6 scenarios on production-equivalent staging and record p50/p95/p99,
  throughput, errors, CPU, memory, DB pool and Argon2 duration.
- [ ] Re-tune budgets/rate limits from evidence and rerun the suite.
- [ ] Prove readiness fails while PostgreSQL is unavailable and recovers without a process
  restart; prove liveness stays healthy.
- [ ] Restore a production-shaped PostgreSQL backup and record RPO/RTO evidence.

## Delivery and operations

- [ ] All GitHub CI gates and branch rules are live on `main`.
- [ ] P14 deployment is defined as code, migration-first, health-gated and rollback-tested.
- [ ] P7/P14 key/secret protection is implemented or release-blocking risk is accepted.
- [ ] Every cataloged trace/metric reaches the P10 backend; dashboards and alerts are owned.
- [ ] Production checklist is signed for the release image digest and migration bundle.

## Close

- [ ] Tag the immutable release and retain SBOM/build/migration evidence.
- [ ] Publish API support and v1 deprecation dates.
- [ ] Archive the roadmap as the v1 planning record; ADRs remain the durable decisions.
- [ ] Schedule the first weekly, monthly, quarterly and semi-annual maintenance events.
