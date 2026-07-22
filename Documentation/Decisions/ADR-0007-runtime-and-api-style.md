# ADR-0007: .NET 10 Runtime, Controllers-Only API Style, RFC 9457 Errors

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, rows *Runtime* and *API style*
- **Affects:** §3 (skeleton), §11 (controllers), §13 (response standards), §14 (middleware), §18 (OpenAPI)

## Context

ASP.NET Core offers two ways to define HTTP endpoints — minimal APIs and MVC controllers — and mixing them in one codebase produces two conventions for routing, filters, model binding, and error handling, with no rule for which applies where.

Error responses have the same problem in miniature. Without a mandated shape, each endpoint invents its own error body, and clients end up parsing several formats from one API.

The project template starts as a minimal-API sample (`/weatherforecast`), so the default path is the one being ruled out.

## Decision

**.NET 10 / ASP.NET Core 10**, `net10.0` target, nullable reference types and implicit usings enabled.

**Attribute-routed MVC controllers only. No minimal API endpoints** — no `app.MapGet`, no `app.MapPost`, no exceptions. This is a hard rule enforced at code review.

**`Program.cs` is strictly a composition root**: builder creation, `Add*` extension-method calls, `Use*`/`Map*` pipeline calls, `app.Run()`. No business logic, no inline handlers, no service registrations written out longhand. The extension methods live in `Extensions/` ([ADR-0014](ADR-0014-solution-layout-and-directories.md)).

**Every error response is an RFC 9457 Problem Details document** (`application/problem+json`), produced centrally by exception-handling middleware rather than constructed per endpoint.

The template's `/weatherforecast` endpoint and its `WeatherForecast` record are deleted in §3.

## Alternatives considered

**Minimal APIs.** Less ceremony, good performance, and the direction the framework has been investing in. Rejected for a codebase of this size: with 40+ endpoints across a dozen resource groups, controllers give a clearer file-per-resource organisation, and attribute routing plus filters map more directly onto the per-endpoint concerns this project has (validation, audit, CSRF, authorization policies).

**Mixing both** — minimal APIs for trivial endpoints such as JWKS and health, controllers for the rest. Rejected: "trivial" is not a stable category, and two parallel conventions for filters and error handling is exactly the ambiguity the controllers-only rule exists to prevent. JWKS gets a controller like everything else.

**Ad-hoc error shapes per endpoint.** Rejected — clients would need per-endpoint error parsing, and there would be no single place to guarantee that internal exception details never leak.

**Older LTS target (.NET 8).** Rejected: the project starts greenfield on the current release, with no legacy dependency forcing an older runtime.

## Consequences

- One consistent endpoint style across the whole API; new contributors have exactly one pattern to learn.
- Controllers stay thin — routing, model binding, and delegation to a service (§12). Business logic in a controller is a review rejection.
- Centralised Problem Details means unhandled exceptions have exactly one path to the client, which is also the single place to guarantee stack traces and internal messages never escape in production (§16).
- `Program.cs` staying under ~40 lines is a measurable review gate (§3's Definition of Done), not a stylistic preference.
- Nullable reference types are enabled solution-wide with warnings-as-errors (§2), so null-related defects surface at compile time rather than as runtime 500s.
- The template sample is deleted rather than kept as a reference; leaving one minimal-API handler in place would undercut a rule that only works if it is absolute.
