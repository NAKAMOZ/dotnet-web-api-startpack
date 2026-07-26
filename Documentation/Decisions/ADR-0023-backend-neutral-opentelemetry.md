# ADR-0023: Backend-neutral OpenTelemetry pipeline

- **Status:** Accepted
- **Date:** 2026-07-26
- **Workstreams:** §28
- **Does not resolve:** P10 (observability backend)

## Context

The API needs request, outbound HTTP, PostgreSQL, runtime and authentication telemetry
before a vendor or self-hosted backend has been selected. Binding instrumentation directly
to a vendor would make P10 a code migration rather than a deployment decision. Npgsql
tracing also requires its small provider-specific OpenTelemetry bridge; metrics are emitted
through `System.Diagnostics.Metrics` directly.

## Decision

Register the OpenTelemetry SDK with ASP.NET Core, HttpClient, runtime and Npgsql
instrumentation. Export traces and metrics through OTLP only when validated configuration
enables it. Add `Npgsql.OpenTelemetry` at the same `10.0.3` version as the Npgsql provider.
No public pull/scrape endpoint is exposed.

Custom authentication signals use one `System.Diagnostics.Metrics` meter and low-cardinality
result/operation tags. Correlation ids are attached to the active request span. A local
collector with a debug exporter is an acceptance tool, not the production backend decision.

## Alternatives considered

- Wait for P10: rejected because it leaves no traces or metrics to evaluate a backend with.
- Vendor SDK: rejected because it chooses the backend by dependency.
- Public Prometheus endpoint: rejected while network placement is unknown; OTLP push keeps
  the public HTTP surface unchanged.
- Hand-written Npgsql `ActivityListener`: rejected in favor of the provider-maintained
  bridge that follows Npgsql's diagnostic source changes.

## Consequences

- P10 selects only the collector/backend destination and retention policy.
- Disabled export has no network destination and still permits local `MeterListener` tests.
- Database semantic conventions can evolve with Npgsql/OpenTelemetry versions; dashboards
  must be reviewed during upgrades.
- The OTLP endpoint is configuration, not a secret, but any exporter headers added later
  are secrets and must use the §25 secret channel.
