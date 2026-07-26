# ADR-0025: In-process background cleanup for authentication artifacts

- **Status:** Accepted
- **Date:** 2026-07-26
- **Workstreams:** §12, §15
- **Resolves:** P9

## Context

Expired sessions, tokens, retired signing keys and audit rows need periodic maintenance.
The work is small, idempotent and belongs to this database; v1 has no distributed job
orchestration requirement.

## Decision

Run `ExpiredAuthArtifactCleanupService` as a plain `BackgroundService`. The validated
`CleanupOptions` controls interval, 90-day audit retention and batch size. Each pass creates
a fresh scope, uses bounded index-shaped deletes and asks `ISigningKeyManager` to retire
elapsed keys.

## Alternatives considered

- Hangfire or Quartz: rejected as persistent scheduling infrastructure for one periodic,
  idempotent task.
- External cron job: rejected because it would duplicate application data-access rules and
  deployment configuration.
- No cleanup: rejected because security and audit tables would grow without bound.

## Consequences

- Every application instance may run the idempotent worker; database predicates make
  concurrent passes safe.
- There is no job dashboard or retry history. Failures are logged and the next interval
  retries.
- A future multi-node scale requirement may move this work to a leased worker without
  changing the cleanup queries.

