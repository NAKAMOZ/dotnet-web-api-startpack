# ADR-0028: Azure Monitor as the production observability backend

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** Project owner, through the explicit implementation directive
- **Source:** Resolves **P10**
- **Affects:** §25, §27, §28

## Context

ADR-0023 established vendor-neutral OpenTelemetry instrumentation but intentionally left the
production exporter open. The Azure deployment now needs one owned trace/metric destination
and a log workspace that can support deployment and incident queries.

## Decision

Export OpenTelemetry traces and metrics directly to workspace-based Application Insights by
using `Azure.Monitor.OpenTelemetry.Exporter`. Container and Serilog console logs flow to the
same Log Analytics workspace through the Container Apps environment. Keep OTLP export as an
independent optional path for local collectors or a future backend migration.

The Application Insights connection string is stored in Key Vault and referenced by the
Container App. It is never committed or logged. Log Analytics retains platform logs for 30
days; Application Insights retention is 90 days.

## Alternatives considered

- Self-hosted Grafana/Tempo/Prometheus: rejected because it adds an observability platform
  to operate before the application has requirements that Azure Monitor cannot meet.
- OTLP collector sidecar: rejected for the initial Azure topology because it duplicates
  buffering, health and scaling concerns without a second destination.
- Vendor-specific instrumentation: rejected; the OpenTelemetry signal model remains the
  source, and only the exporter is Azure-specific.

## Consequences

- Production enables the Azure exporter and can also enable OTLP without changing call sites.
- Dashboards, alert rules and an on-call recipient must still be provisioned/owned as release
  evidence; the application supplies signal names and initial thresholds.
- Serilog remains the sole application log pipeline so its redaction policy is not bypassed.
