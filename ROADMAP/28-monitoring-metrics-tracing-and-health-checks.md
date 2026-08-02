# 28. Monitoring, Metrics, Tracing, and Health Checks

## Objective

The system tells you it's healthy, shows you what it's doing, and alerts on auth-specific attack signals.

## Scope

Health endpoints, OpenTelemetry traces + metrics, custom auth metrics, alert catalog.

## Architectural Decisions

- Health: `/health/live` (process up, no dependencies) and `/health/ready` (PostgreSQL,
  migrations and enabled Redis) — the split matters for orchestrator restarts vs traffic gating.
- OpenTelemetry: ASP.NET Core + HttpClient + Npgsql instrumentation; Azure Monitor production
  exporter plus optional OTLP; correlation ID attached as a span attribute.
- Custom metrics via `System.Diagnostics.Metrics` in `Services/` (a shared `AuthMetrics` meter class in `Logging/`): `auth.logins` (counter, tag `result`), `auth.refreshes` (tag `result`), `auth.reuse_detections`, `auth.lockouts`, `auth.mfa_challenges` (tag `result`), `auth.active_sessions` (observable gauge), `auth.password_hash_duration` (histogram — silent Argon2 drift detector).
- Alert catalog (backend-agnostic thresholds documented): reuse-detection spike (>0 sustained), login-failure ratio surge, lockout surge, readiness flapping, hash-duration p95 drift.

## Technology Decisions Requiring Approval

P10 resolved by ADR-0028.

## Tasks

- [x] `Extensions/ServiceCollectionExtensions.HealthChecks.cs` + `ApplicationBuilderExtensions.HealthChecks.cs`: live/ready split, PostgreSQL connectivity + pending-migration readiness, five-second timeout, minimal anonymous response.
- [x] `Extensions/ServiceCollectionExtensions.Telemetry.cs`: ASP.NET Core, HttpClient, runtime and Npgsql wiring; service resource attributes; validated, config-gated OTLP trace/metric export.
- [x] `Logging/AuthMetrics.cs` + instrumentation: refresh, reuse, lockout, active-session, Argon2, login and MFA points are wired with low-cardinality result tags.
- [x] `Documentation/Operations/Monitoring.md`: signal/metric catalog, health contract, alert thresholds, dashboard sketch and backend-neutral local collector procedure.

**Current status (2026-08-02):** PostgreSQL/Redis readiness, metric catalog and local OTLP
acceptance are covered. Azure Monitor resources/export are implemented; dashboard/alert
ownership and on-call routing remain first-deployment operational evidence.

## Expected Deliverables

Health endpoints, OTel wiring, `AuthMetrics`, monitoring doc.

## Dependencies

§12 (instrumentation points), §14 (correlation), P10.

## Security Considerations

`/health/ready` output is minimal for anonymous callers (status only — dependency names/errors are log-only); metrics endpoint (if pull-based per P10) is network-restricted, not public. Reuse-detection and lockout metrics are the *attack radar* — they exist for alerting, not vanity dashboards.

## Testing Requirements

§21: health endpoints return correct status with DB up/down (isolated container stop in-test);
the complete metric catalog has a `MeterListener` assertion. Login-flow emission remains
covered by the login and MFA services; current service points are instrumented.

## Documentation Requirements

Monitoring doc as above.

## Definition of Done

Health gates work under dependency failure; all cataloged metrics observable in a local OTLP collector run; alert catalog owner-reviewed.

## Questions for the Project Owner

1. ~~Observability backend?~~ Azure Monitor selected in ADR-0028; OTLP remains optional.
