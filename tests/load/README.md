# Load tests

The k6 suite encodes the approved initial budgets:

| Operation | Profile | Threshold |
|---|---:|---:|
| Login | 50 RPS sustained | p95 < 500 ms |
| Refresh | 200 RPS sustained | p95 < 100 ms |
| `GET /users/me` | 500 RPS sustained | p95 < 50 ms |

Start the Compose stack, then run one scenario:

```bash
docker compose up --build -d
k6 run tests/load/login.js
k6 run tests/load/refresh.js
k6 run tests/load/me.js
k6 run tests/load/mixed.js
```

Override `BASE_URL`, `TEST_EMAIL`, `TEST_PASSWORD`, `DURATION`, or the per-script RPS
variables for staging. Never point these scripts at production without an approved test
window. k6 exits non-zero when a latency, failure-rate, or check threshold is breached.

The current feature actions return 501, so the scenario checks intentionally fail until
§12 supplies login, refresh, and profile services. This is a visible dependency, not a
reason to weaken the checks.

The default authentication policies permit 10 login or refresh attempts per minute per IP,
which is intentionally below these single-source profiles. For a controlled load
environment, configure a test-only limit sized above the scenario rate; never disable the
general limiter in a deployed environment. Saturation testing separately verifies that the
normal policy returns 429 before Argon2 exhausts CPU.
