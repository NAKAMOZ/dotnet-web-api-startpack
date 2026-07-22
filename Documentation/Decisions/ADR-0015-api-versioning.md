# ADR-0015: URL-Segment API Versioning

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decision P2** (`ROADMAP/00-overview.md`)
- **Affects:** §3 (route templates in the skeleton), §11 (controllers), §18 (OpenAPI), §19 (endpoint docs), §29 (deprecation policy)

## Context

This API is consumed by clients it does not control — browser SPAs, mobile apps that ship on app-store timelines, CLIs, and server integrations. A mobile client can remain in the field for years, so a breaking change cannot be coordinated; it has to be *versioned around*.

Versioning has to be chosen before the first route is written, because retrofitting it means changing every route template and every client that already depends on them.

## Decision

**URL-segment versioning: `/api/v1/…`**, implemented with `Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer`.

Every endpoint in the inventory carries the version segment, with two deliberate exceptions that are **unversioned** because they are infrastructure contracts rather than API surface:

- `/.well-known/jwks.json` — an RFC-defined well-known location; clients and libraries expect it at exactly that path.
- `/health/live` and `/health/ready` — consumed by orchestrators, not API clients.

**Evolution policy** (elaborated in §29): changes within `v1` are **additive only** — new optional fields and new endpoints. Anything breaking mints `/api/v2`, with a `Sunset` header on the old version, deprecation notices in Scalar and `Documentation/`, and a stated minimum overlap window during which both versions run.

## Alternatives considered

**Header-based versioning** (`X-Api-Version: 1`). Cleaner URLs, and arguably more correct in that the resource identity does not change between versions. Rejected on practical grounds: the version becomes invisible in logs, browser address bars, `curl` examples, and bug reports, so "which version were you calling?" stops being answerable from the request line alone. It would also require rewriting every route in the endpoint inventory, which is already written with the segment.

**Media-type versioning** (`Accept: application/vnd.api.v1+json`). The most RESTful option by the book. Rejected as the least approachable — it makes trying an endpoint from a browser or a plain `curl` awkward, and this is a developer-facing API where low-friction exploration matters.

**Query-string versioning** (`?api-version=1`). Easy to use, but trivial to omit, which pushes clients onto an implicit default and makes caching and routing messier. Rejected.

**No versioning at all.** Rejected: guaranteeing no breaking change ever is not a promise this project can keep, and discovering that after clients have shipped is the expensive way to learn it.

## Consequences

- The version is visible in every log line, trace, access log, and bug report — the practical argument that decided it.
- Route templates are `[Route("api/v{version:apiVersion}/…")]`, so the segment is declared once per controller rather than hard-coded per action.
- `Asp.Versioning.Mvc.ApiExplorer` feeds version metadata into OpenAPI, so Scalar can present versions distinctly (§18) and the §19 sync test can match documents to operations per version.
- The unversioned exceptions must be excluded from versioning conventions explicitly, or the framework will prefix them. §19's sync test must account for them too, since their route shape differs from every other endpoint.
- A `v2` means running two versions concurrently, with the operational cost that implies. The overlap window in §29's policy is what bounds it.
- Because `v1` is additive-only, response DTOs cannot drop or rename fields within the version. New fields are optional; removals wait for `v2`. This is a constraint on every feature slice, not just on breaking-change work.
