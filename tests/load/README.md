# Load tests

The k6 suite encodes the approved initial budgets:

| Operation | Profile | Threshold |
|---|---:|---:|
| Login | 50 RPS sustained | p95 < 500 ms |
| Refresh | 200 RPS sustained | p95 < 100 ms |
| `GET /users/me` | 500 RPS sustained | p95 < 50 ms |

Start the Compose stack, then run one scenario. For a controlled local run, raise the two
single-IP limits through the explicit Compose knobs (they keep production-like defaults
when unset):

```bash
LOAD_TEST_AUTH_STRICT_PERMIT_LIMIT=10000 \
LOAD_TEST_GENERAL_PERMIT_LIMIT=100000 \
docker compose up --build -d
k6 run tests/load/login.js
k6 run tests/load/refresh.js
k6 run tests/load/me.js
k6 run tests/load/mixed.js
```

`config.js` owns everything the scenarios share: routes, the login request, bounded
per-VU token-pool creation, the per-operation thresholds, and the check floor.
A scenario file should hold its `options` block and its request, nothing else — the
bootstrap login is tagged `setup` so it never lands in the `login` scenario's thresholds.

Override `BASE_URL`, `TEST_EMAIL`, `TEST_PASSWORD`, `DURATION`, or the per-script RPS
variables for staging. The mixed script uses `MIXED_LOGIN_RPS`, `MIXED_REFRESH_RPS`,
`MIXED_ME_RPS` and matching `MIXED_*_MAX_VUS` overrides. Keep each maximum at or above its
configured pre-allocation (20, 40 and 50 respectively). Never point these scripts at
production without an approved test window. k6 exits non-zero when a latency, failure-rate,
or check threshold is breached.

The login, refresh, and profile actions are service-backed. The scenario checks therefore
measure real flows; a non-success response fails the run instead of being treated as a
latency sample.

The default authentication policies permit 10 login or refresh attempts per minute per IP,
which is intentionally below these single-source profiles. For a controlled load
environment, configure a test-only limit sized above the scenario rate; never disable the
general limiter in a deployed environment. Saturation testing separately verifies that the
normal policy returns 429 before Argon2 exhausts CPU.

The scheduled/manual staging workflow temporarily raises only the two single-source limits,
warms horizontal replicas, runs all four scripts, and restores normal limits in an
`always()` step. Its `LOAD_TEST_EMAIL` account must be a dedicated verified staging user.

Without a local k6 binary, use the pinned container image:

```bash
docker run --rm \
  --volume "$PWD/tests/load:/scripts:ro" \
  --env BASE_URL=http://host.docker.internal:5035 \
  grafana/k6:2.1.0 run /scripts/mixed.js
```
