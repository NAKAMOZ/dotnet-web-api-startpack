# 28. Monitoring, Metrics, Tracing, and Health Checks

## Objective

The system tells you it's healthy, shows you what it's doing, and alerts on auth-specific attack signals.

## Scope

Health endpoints, OpenTelemetry traces + metrics, custom auth metrics, alert catalog.

## Architectural Decisions

- Health: `/health/live` (process up, no dependencies) and `/health/ready` (PostgreSQL reachable via `AspNetCore.HealthChecks.NpgSql`, pending-migrations check) — the split matters for orchestrator restarts vs traffic gating.
- OpenTelemetry: ASP.NET Core + HttpClient + Npgsql instrumentation; OTLP exporter, backend per P10; correlation ID attached as span attribute (bridges Serilog and traces).
- Custom metrics via `System.Diagnostics.Metrics` in `Services/` (a shared `AuthMetrics` meter class in `Logging/`): `auth.logins` (counter, tag `result`), `auth.refreshes` (tag `result`), `auth.reuse_detections`, `auth.lockouts`, `auth.mfa_challenges` (tag `result`), `auth.active_sessions` (observable gauge), `auth.password_hash_duration` (histogram — silent Argon2 drift detector).
- Alert catalog (backend-agnostic thresholds documented): reuse-detection spike (>0 sustained), login-failure ratio surge, lockout surge, readiness flapping, hash-duration p95 drift.

## Technology Decisions Requiring Approval

P10 (export backend).

## Tasks

- [ ] `Extensions/ServiceCollectionExtensions.HealthChecks.cs` + `ApplicationBuilderExtensions.HealthChecks.cs` (endpoints, response writer minimal — no dependency detail leakage to anonymous callers).
- [ ] `Extensions/ServiceCollectionExtensions.Telemetry.cs`: OTel wiring, resource attributes (service name/version), OTLP exporter config-gated.
- [ ] `Logging/AuthMetrics.cs` + instrumentation calls in §12 services.
- [ ] `Documentation/Operations/Monitoring.md`: metric catalog, alert catalog with rationale, dashboard sketch.

## Expected Deliverables

Health endpoints, OTel wiring, `AuthMetrics`, monitoring doc.

## Dependencies

§12 (instrumentation points), §14 (correlation), P10.

## Security Considerations

`/health/ready` output is minimal for anonymous callers (status only — dependency names/errors are log-only); metrics endpoint (if pull-based per P10) is network-restricted, not public. Reuse-detection and lockout metrics are the *attack radar* — they exist for alerting, not vanity dashboards.

## Testing Requirements

§21: health endpoints return correct status with DB up/down (container stop in-test); metrics emitted for a login flow (MeterListener assertion).

## Documentation Requirements

Monitoring doc as above.

## Definition of Done

Health gates work under dependency failure; all cataloged metrics observable in a local OTLP collector run; alert catalog owner-reviewed.

## Questions for the Project Owner

1. Observability backend preference (P10) — existing stack (Grafana/Datadog/other) to integrate with?
