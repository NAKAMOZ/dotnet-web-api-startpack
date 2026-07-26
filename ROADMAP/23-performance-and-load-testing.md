# 23. Performance and Load Testing

## Objective

Establish latency/throughput baselines for the auth hot paths and tune Argon2id cost against real hardware.

## Scope

k6 scenarios (P15), Argon2id tuning method, performance budgets.

## Architectural Decisions

- Argon2id tuning is a measured procedure, not a guess: target ~100 ms verification on production-class hardware; parameters recorded in `PasswordHashingOptions` defaults + ADR.
- Budgets (initial, owner-adjustable): login p95 < 500 ms at 50 RPS sustained (Argon2-bound); refresh p95 < 100 ms at 200 RPS; `GET /users/me` p95 < 50 ms at 500 RPS.
- Load tests run against the compose stack (§24) locally and optionally against staging; not in per-PR CI (too slow/noisy) — scheduled or pre-release.

## Technology Decisions Requiring Approval

P15 (k6); budget numbers.

## Tasks

- [x] `tests/load/` k6 scripts: `login.js`, `refresh.js`, `me.js`, `mixed.js` (realistic mix), with shared config + thresholds encoding the budgets.
- [x] `tests/load/README.md`: how to run against compose/staging, interpreting thresholds.
- [x] Argon2id tuning script (`tests/load/tune-argon2.md` procedure + a small harness in `UnitTests` benchmarking hash time on the current machine).
- [ ] Record baseline results in `Documentation/Operations/PerformanceBaseline.md`. Local Argon2 Release median recorded at 177.0 ms; endpoint baselines correctly remain blocked while the measured actions return 501 and P14 leaves production hardware unknown.
- [x] Verify rate limits (§17) vs load profiles don't strangle legitimate bursts (adjust or document). Controlled-test overrides and the separate normal-policy saturation run are documented; execution waits for the feature actions.

## Expected Deliverables

k6 suite, tuning procedure, baseline doc.

## Dependencies

§24 (compose stack), §17 (limits interact with load).

## Security Considerations

Argon2id cost is a security/performance trade — tuning it down below ~50 ms requires owner sign-off; login endpoint saturation behavior (429 before CPU exhaustion) verified under load.

## Testing Requirements

Thresholds encoded in scripts (k6 fails on breach) — objective pass/fail.

## Documentation Requirements

Baseline doc updated per release.

## Definition of Done

Baselines recorded; budgets met or renegotiated with owner; Argon2 parameters finalized by measurement.

## Questions for the Project Owner

1. Approve budget numbers / provide expected traffic profile (P15 confirmation included)?
