# Performance Baseline

## Budgets

The initial owner-adjustable budgets are encoded directly in `tests/load/config.js`:
login p95 < 500 ms at 50 RPS, refresh p95 < 100 ms at 200 RPS, and profile reads p95
< 50 ms at 500 RPS.

## Current result

Endpoint baselines are **not yet claimable** because a controlled compose/staging run and
production-class hardware profile have not been recorded. The three measured actions are
service-backed and the k6 scripts are ready to capture those measurements.

The Argon2 tuning harness measures the production `Argon2PasswordHasher` defaults
(64 MiB, 3 iterations, parallelism 1) and enforces the 50 ms security floor. A local
Release run measured a 177.0 ms median across seven warm samples. That clears the floor but
is above the ~100 ms target; it is a development reference only and cannot finalize the
parameters before production hardware is selected through P14.

| Date | Target | CPU / memory | Runtime | Scenario | Result |
|---|---|---|---|---|---|
| 2026-07-26 | Local Apple Silicon arm64 (memory not captured) | arm64 | .NET SDK 10.0.302 / runtime 10.0.10 | Argon2 verify median, 7 samples | 177.0 ms |
| Pending | Production-class (P14) | Pending | .NET 10 | Argon2 verify median | Pending |
| Pending | Compose/staging | Pending | .NET 10 | login / refresh / me / mixed | Awaiting controlled load run |

## Rate-limit interaction

The approved limits protect normal deployment traffic and intentionally reject the
single-IP default load profiles. Controlled load environments must raise limits above the
scenario rate through environment variables. A separate saturation run keeps normal limits
and verifies 429 responses occur before password hashing saturates the host.
