# ADR-0012: Scalar over the Built-in OpenAPI Generator, plus Per-Endpoint Markdown

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *API documentation*
- **Affects:** §18 (Scalar/OpenAPI), §19 (endpoint Markdown), §11 (same-PR rule)

## Context

The API is headless, so its documentation *is* its user interface. Two different needs follow, and one artefact cannot serve both.

Machine-readable structure — routes, methods, schemas, status codes — should be generated from code, because anything hand-maintained drifts. But an auth API also has to explain things no schema can express: why a refresh cookie is path-scoped, what happens to sibling sessions when a password changes, which endpoints require recent authentication and why. That is prose, and prose cannot be generated.

The failure mode to design against is not "no documentation". It is documentation that was accurate once.

## Decision

**Two layers, with an explicit authority boundary.**

**Generated:** the built-in .NET 10 OpenAPI generator (`AddOpenApi`/`MapOpenApi`) produces the document from code. **Scalar** (`Scalar.AspNetCore`) renders it as interactive documentation. OpenAPI is **authoritative for mechanical facts** — route, method, parameters, schemas, status codes.

**Hand-written:** one Markdown file per endpoint under `Documentation/`, mirroring the controller structure, with sixteen mandated sections in fixed order (§19). Markdown is authoritative for **narrative** — security considerations, worked examples, related-endpoint guidance.

**Three enforcement layers keep them aligned:**

1. **Process** — the same-PR rule: an endpoint's controller, DTOs, validators, services, tests, and Markdown doc land in one PR. A PR missing its doc is incomplete.
2. **Mechanical** — `DocumentationSyncTests` loads the generated OpenAPI document at test time and asserts set equality with the Markdown files: a missing doc fails, and an **orphaned doc fails too**. Front-matter `method`, `route`, and `auth` keys in each file are asserted against the corresponding operation, so load-bearing facts cannot rot silently. Runs in CI.
3. **Direction** — where the two overlap, OpenAPI wins and the test enforces it. Humans own only what the generator cannot express.

**Scalar is exposed in development and staging only, never in production.** This exposure
decision was finalized by [ADR-0031](ADR-0031-openapi-exposure.md).

## Alternatives considered

**Swashbuckle/NSwag with Swagger UI.** The long-standing default. Rejected in favour of the built-in generator, which ships with .NET 10 and removes a third-party dependency from the document-generation path, plus Scalar for a better-maintained modern UI.

**OpenAPI only, no Markdown.** Rejected: `description` fields cannot carry the security narrative an auth API needs, and cramming multi-paragraph rationale into schema annotations makes both the annotations and the code unreadable.

**Markdown only, hand-written OpenAPI.** Rejected — a hand-maintained OpenAPI document is drift waiting to happen, and it is the artefact clients generate code from.

**Docs as a follow-up PR after the feature.** Rejected explicitly. This is the default path to permanently stale documentation: the follow-up is deprioritised, and by the time anyone returns the author's context is gone. The same-PR rule exists because of this.

**Process discipline alone, without the sync test.** Rejected: review catches a missing file only when the reviewer thinks to look. A failing test catches it every time.

## Consequences

- Documentation drift becomes a **build failure**, not a discovery. That is the entire point of the mechanical layer.
- 43 Markdown files exist at v1 (§19). They are authored incrementally as each feature slice lands, never in one batch at the end.
- The sync test needs the generated OpenAPI document, so §19 depends on §18 and on the feature slices being complete.
- The front-matter block in each Markdown file is a machine-parsed contract, not decoration — its `method`/`route`/`auth` keys must match the operation exactly.
- Orphan detection means deleting an endpoint requires deleting its doc, so the tree cannot accumulate documentation for routes that no longer exist.
- Disabling Scalar/OpenAPI in production means the interactive UI and machine-readable
  document are unavailable there; both remain available in controlled staging.
- Each file's Security Considerations section is mandatory *content*, not a heading to fill with boilerplate — §19 makes copy-paste a review rejection, since a boilerplate security section is worse than an absent one for implying review that did not happen.
