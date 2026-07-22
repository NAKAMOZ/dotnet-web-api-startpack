# ADR-0010: Serilog for Structured Logging

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Logging*
- **Affects:** §14 (middleware), §15 (logging and audit), §28 (observability)

## Context

An authentication service's logs are an operational tool and a security artefact at once. "Who attempted what, from where, and what happened" needs to be answerable across thousands of requests, which means logs must be **queryable by field**, not greppable by substring.

Two related things must not be conflated. **Application logs** are diagnostic, sampled, and disposable. The **audit trail** (`AuditLogEntry`, §15) is a durable security record with its own retention policy (P18). This ADR covers the former; the latter is a database table, not a log sink.

## Decision

**Serilog** via `Serilog.AspNetCore`, configured in `Logging/` and wired from the composition root.

**Structured throughout.** Message templates with named properties — `logger.LogInformation("Login failed for {UserId} from {IpAddress}", …)` — never interpolated strings. Interpolation destroys the field structure that makes logs queryable and is treated as a review rejection.

**Enrichers** attach a correlation ID and, where authenticated, a user ID to every event in a request scope, so a single request is reconstructable end to end without threading identifiers through call signatures.

**Request logging** replaces ASP.NET Core's default per-request noise with one structured summary event per request.

**Never logged, under any level:** passwords, password hashes, access or refresh tokens, token hashes, API keys, TOTP secrets, recovery codes, signing-key material, or session cookies.

## Alternatives considered

**`Microsoft.Extensions.Logging` with the built-in console provider only.** No dependency, and structured logging is supported at the API level. Rejected on sinks and enrichment: routing to files, seq, or an OTLP collector, plus per-scope enrichment, is exactly what Serilog packages and what would otherwise be hand-rolled.

**NLog or log4net.** Both capable. Serilog was preferred for being structured-first by design rather than structured-capable by extension, and for the maturity of its ASP.NET Core integration.

**OpenTelemetry logging as the primary pipeline.** OpenTelemetry is adopted for traces and metrics (§28, P10). Using it as the sole logging pipeline was rejected for v1 — Serilog's sink ecosystem and enrichment model are more ergonomic for application logs, and the two coexist with Serilog exporting to OTLP where required.

## Consequences

- Logs are queryable by field: "all failed logins for this user in the last hour" is a filter, not a regex.
- The correlation ID is the join key across application logs, audit entries, and traces. It must be issued early in the pipeline (§14 middleware) and included in error responses so a user-reported failure is findable.
- **Log-injection risk is real**: user-controlled values (email, user agent) reach log events. Structured properties rather than string concatenation is the mitigation, since values stay data instead of becoming part of the message template.
- The never-log list is load-bearing, not advisory. A leaked token in a log file is a credential leak with a long tail — log aggregators retain, replicate, and back up. §15 and §16 must include a review checklist item for it, and §22 should assert that a login request's body never appears in emitted output.
- Log level and sink configuration belongs in `appsettings`, so verbosity changes in production do not need a redeploy.
- Serilog runs in the pipeline for every request, so its configuration is a startup-critical dependency: misconfiguration should fail fast at boot rather than silently drop events.
