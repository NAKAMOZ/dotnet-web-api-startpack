# Monitoring and Alerting

The application emits structured logs through Serilog and traces/metrics through
OpenTelemetry. OTLP is the stable boundary: P10 chooses the backend, not a different
instrumentation path.

## Signal flow

```text
ASP.NET Core ─┐
HttpClient ───┤
Npgsql ───────┼─ OpenTelemetry SDK ─ OTLP ─ collector/backend (P10)
.NET runtime ─┤
AuthMetrics ──┘

request X-Correlation-Id ─ app.correlation_id span tag
                       └─ CorrelationId structured-log field
```

OTLP export is disabled unless both `Telemetry:OtlpExporterEnabled=true` and an absolute
`Telemetry:OtlpEndpoint` are configured. No public `/metrics` endpoint exists.

## Health contract

| Endpoint | Meaning | Dependencies | Success body |
|---|---|---|---|
| `/health/live` | process can answer HTTP | none | `Healthy` |
| `/health/ready` | safe to receive traffic | PostgreSQL reachable; no pending EF migration | `Healthy` |

Failures return `503 Unhealthy`. Anonymous responses never include check names, exception
messages, hosts or migration identifiers; those details remain in server diagnostics.
Orchestrators restart on liveness and gate traffic on readiness. A database outage must not
cause a restart loop.

## Trace catalog

- ASP.NET Core inbound requests.
- HttpClient outbound calls for provider/email integrations.
- Npgsql commands and physical connection opens.
- Resource attributes: service name, assembly version, instance id and deployment environment.
- `app.correlation_id` on the active request span, matching the response header and Serilog.

Never add passwords, tokens, cookies, email addresses, SQL parameters or user/session ids as
span attributes. Npgsql command text policy must be reviewed if custom tracing options are
added.

## Authentication metric catalog

Meter: `Api.Authentication`.

| Metric | Instrument | Unit | Tags | Current emitter |
|---|---|---|---|---|
| `auth.logins` | counter | attempts | `result` | API available; login call site waits on §12 |
| `auth.refreshes` | counter | attempts | `result` | `RefreshTokenService` |
| `auth.reuse_detections` | counter | detections | none | refresh replay path |
| `auth.lockouts` | counter | lockouts | none | `LockoutPolicy` threshold transition |
| `auth.mfa_challenges` | counter | challenges | `result` | API available; MFA call site waits on §12 |
| `auth.active_sessions` | observable gauge | sessions | none | one post-start DB sample, then exact updates after session mutation |
| `auth.password_hash_duration` | histogram | milliseconds | `operation=hash|verify` | password Argon2 path |

Tags are deliberately low-cardinality. User ids, emails, session ids, IPs and correlation ids
must never become metric labels.

Npgsql 10 also emits its `Npgsql` meter, including `db.client.operation.duration`,
connection-count and pool signals. ASP.NET Core, HttpClient and .NET runtime instrumentation
provide request duration, outbound duration, exceptions, GC and thread-pool signals.

## Alert catalog

Thresholds are initial budgets, not universal truths. Revisit after §23 has a production-like
baseline and require a minimum event count for ratios.

| Alert | Initial condition | Severity | Rationale / first response |
|---|---|---|---|
| Refresh-token reuse | `increase(auth.reuse_detections[5m]) > 0` | Critical | A stolen token or broken client can be active. Inspect audit/correlation data; revoke the session; escalate a spike. |
| Login-failure ratio | failures >25% with ≥50 attempts over 10m; critical above 50% | Warning/Critical | Credential stuffing or provider failure. Check source distribution, limiter saturation and successful-login baseline. |
| Lockout surge | >10 lockouts over 10m or >3× the same-hour baseline | High | Attackers are reaching real account identifiers or a client is retrying bad credentials. |
| Readiness flapping | 3 failures in 5m for any instance | High | Database reachability, exhausted pool or unapplied migration. Do not restart solely on readiness. |
| Argon2 drift | p95 >2× approved baseline or >500 ms for 15m | Warning | Hardware contention/config change can turn login into self-DoS. Compare runtime CPU/GC and deployed options. |

Also alert on sustained 5xx rate, request-latency budget breaches, OTLP export failures,
database pool saturation and missing telemetry from an expected instance.

## Dashboard sketch

1. **Service overview:** ready instances, request rate, error rate and p50/p95/p99 latency.
2. **Authentication funnel:** login outcomes, MFA outcomes, refresh outcomes and active sessions.
3. **Attack radar:** reuse detections, failure ratio, lockouts, rate-limit 429s and audit links.
4. **Crypto:** password hash p50/p95/p99 beside CPU, allocation and GC pause.
5. **PostgreSQL:** command duration, pool used/idle/max, connection errors and readiness.
6. **Telemetry health:** export failures, last-seen timestamp per instance and dropped spans.

Each trace panel links by trace id; operational logs link by correlation id. Audit rows remain
the durable security record and are not replaced by either.

## Local OTLP acceptance

Start the normal stack plus the backend-neutral debug collector:

```bash
docker compose \
  -f docker-compose.yml \
  -f docker-compose.observability.yml \
  up --build
```

Exercise `/health/ready`, JWKS and a token integration flow, wait at least five seconds, then:

```bash
docker compose \
  -f docker-compose.yml \
  -f docker-compose.observability.yml \
  logs otel-collector
```

Collector output must show resource attributes, an inbound request trace, an Npgsql span and
authentication/runtime metrics. This proves the OTLP contract only; P10 still owns backend,
retention, dashboard and on-call routing approval.
