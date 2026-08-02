# Performance Baseline

## Budgets

The initial owner-adjustable budgets are encoded directly in `tests/load/config.js`:
login p95 < 500 ms at 50 RPS, refresh p95 < 100 ms at 200 RPS, and profile reads p95
< 50 ms at 500 RPS.

## Current result

A controlled local Compose run now provides a development reference. It is not
production-equivalent: Docker Desktop CPU/memory allocation and the Apple Silicon model were
not captured, and only the Azure staging workflow can validate the horizontally scaled
production topology.

The Argon2 tuning harness measures the production `Argon2PasswordHasher` defaults
(64 MiB, 3 iterations, parallelism 1) and enforces the 50 ms security floor. A local
Release run measured a 177.0 ms median across seven warm samples. That clears the floor but
is above the ~100 ms target; it is a development reference only and cannot finalize the
parameters before the selected Azure topology is measured on production-equivalent staging.

| Date | Target | CPU / memory | Runtime | Scenario | Result |
|---|---|---|---|---|---|
| 2026-07-26 | Local Apple Silicon arm64 (memory not captured) | arm64 | .NET SDK 10.0.302 / runtime 10.0.10 | Argon2 verify median, 7 samples | 177.0 ms |
| Pending | Production-equivalent Azure staging | 2 vCPU / 4 GiB per replica | .NET 10 | Argon2 verify median | Automated workflow awaits environment rollout |
| 2026-08-02 | Local Compose, arm64 | Docker allocation not captured | .NET 10.0.10 / k6 2.1.0 | login, 2 RPS for 20s | 41/41; p95 288.65 ms; 0 errors; pass |
| 2026-08-02 | Local Compose, arm64 | Docker allocation not captured | .NET 10.0.10 / k6 2.1.0 | refresh, 200 RPS for 20s | 4,001/4,001; operation p95 6.78 ms; 0 errors; pass |
| 2026-08-02 | Local Compose, arm64 | Docker allocation not captured | .NET 10.0.10 / k6 2.1.0 | `GET /users/me`, 500 RPS for 20s | 10,001/10,001; operation p95 1.86 ms; 0 errors; pass |
| 2026-08-02 | Local Compose, arm64 | Docker allocation not captured | .NET 10.0.10 / k6 2.1.0 | mixed login/refresh/profile smoke, 1 RPS each for 30s | 91/91 checks; login p95 348.49 ms, refresh 46.61 ms, profile 24.05 ms; 0 errors; pass |
| 2026-08-02 | Local Compose, arm64 | Docker allocation not captured | .NET 10.0.10 / k6 2.1.0 | login, 50 RPS for 20s | **Fail:** 271 complete, 730 dropped; p95 20.88 s; 11.63 completed RPS |
| Pending | Azure staging | 2 vCPU / 4 GiB per replica, 1–10 staging scale | .NET 10 | login / refresh / me / mixed | Automated workflow added; requires deployed environment and dedicated account |

The 50 RPS failure is a capacity result, not permission to weaken Argon2. At the measured
177 ms median, that arrival rate demands roughly nine CPU-seconds per wall-clock second and
about 0.6 GiB of Argon2 working memory at average steady-state concurrency, before burst,
runtime and database headroom. The Azure
topology therefore uses shared Redis state and 2 vCPU/4 GiB replicas with HTTP-concurrency
autoscaling; production starts at five replicas and can grow to ten. The budget remains a
staging release gate until that topology produces a passing record.

An earlier profile run that logged in independently from every measured VU produced an
artificial Argon2 storm. That harness bug was removed: profile reads now share a setup token,
and every refresh VU receives an independent setup session so rotation cannot race.
The mixed smoke uses enough samples to exclude one-time warm-up latency from the percentile;
a three-second diagnostic run completed every request but was intentionally not retained as a
latency baseline because four samples made p95 equivalent to a cold outlier.

## Rate-limit interaction

The approved limits protect normal deployment traffic and intentionally reject the
single-IP default load profiles. Controlled load environments must raise limits above the
scenario rate through environment variables. A separate saturation run keeps normal limits
and verifies 429 responses occur before password hashing saturates the host.
