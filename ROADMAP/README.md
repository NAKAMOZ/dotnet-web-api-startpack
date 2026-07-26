# Roadmap Status Board

This folder contains the implementation roadmap for the Better Auth–inspired authentication & authorization REST API, split into one file per workstream. Start with [00-overview.md](00-overview.md) — it holds the approved decisions, the pending decisions (P1–P18), the target directory structure, the entity model, and the full endpoint inventory that all workstreams reference.

## Current status

| | |
|---|---|
| **Planning** | ✅ Complete — all 29 workstreams specified |
| **Technology consultation** | ✅ Complete — core stack approved by the project owner (see the approved-decisions table in the overview) |
| **Decision record** | ✅ 26 ADRs + the authentication, authorization and service architecture documents written in [`Documentation/Decisions/`](../Documentation/Decisions/README.md) — the durable record; this roadmap is archived at v1 close (§29) |
| **Open decisions** | ⏳ 6 items (P6, P7, P10, P11, P14, P16); P8/P9 resolved by ADR-0024/0025 |
| **Implementation** | 🔄 **Phase A–D done; feature services complete; cross-cutting, documentation, tests and local operations advanced** — 296 tests green (210 unit, 86 integration; 50 security-tagged). All 43 operations are backed by real services; registration/login/MFA/refresh/reset/session/user/social/passkey/API-key/admin flows, SMTP queue, cleanup worker and HybridCache key resolution are wired. Owner reviews, external secret/KMS choices, formal approvals, load baselines and GitHub controls remain. |

## How to use this board

- Each workstream file ends with a **Definition of Done**. A workstream is marked ✅ here only when its Definition of Done is met — not when its code merely exists.
- Mark 🔄 when a workstream has open PRs or partial task completion; keep the task checkboxes inside the workstream file itself as the fine-grained record.
- Do not start a workstream whose **Dependencies** or blocking **Pending Decisions** are unresolved.

Legend: ⬜ not started · 🔄 in progress · ✅ done (DoD met) · ⏳ blocked on a pending decision

## Workstreams

### Phase A — Foundation

| # | Workstream | Status |
|---|---|---|
| 1 | [Requirements and Architectural Decisions](01-requirements-and-architectural-decisions.md) | 🔄 ADRs + Scope written; awaiting owner review of `Documentation/Scope.md` (last DoD item) |
| 2 | [Technology Selection](02-technology-selection.md) | ✅ manifest pinned, builds clean, `ADR-0013` written; P5/P15 resolved |
| 3 | [Solution and Project Structure](03-solution-and-project-structure.md) | 🔄 skeleton built, builds + tests green; awaiting owner commit |

### Phase B — Architecture

| # | Workstream | Status |
|---|---|---|
| 4 | [Authentication and Token Architecture](04-authentication-and-token-architecture.md) | 🔄 architecture doc + interfaces landed, builds clean; awaiting owner review of the doc |
| 5 | [Authorization and Permissions](05-authorization-and-permissions.md) | ✅ model + handlers + tests; deny-by-default fallback **activated in §12**; `[RequirePermission]` applied across the admin controllers in §11 |

### Phase C — Data

| # | Workstream | Status |
|---|---|---|
| 6 | [Domain and Entity Modeling](06-domain-and-entity-modeling.md) | 🔄 19 entity/enum files landed, builds clean, entity table + ER diagram synced; awaiting owner sign-off on the three recorded deviations |
| 7 | [Entity Framework Core Configuration](07-entity-framework-core-configuration.md) | ✅ `AppDbContext` + 13 configurations + interceptor; generated SQL reviewed against the design; 10 model-shape tests green |
| 8 | [Database Migrations and Seed Data](08-database-migrations-and-seed-data.md) | ✅ `InitialCreate` applied to PostgreSQL; roles and dev users seed idempotently, including repair of legacy null password hashes |

### Phase D — API Plumbing

| # | Workstream | Status |
|---|---|---|
| 9 | [DTO Organization](09-dto-organization.md) | ✅ 47 records across 12 feature namespaces; 5 reflection guard tests green |
| 10 | [Validation](10-validation.md) | ✅ 20 validators + shared rules + filter; RFC 9457 400 with `errorCodes` verified over HTTP once §11 landed |
| 11 | [Controller Architecture](11-controller-architecture.md) | ✅ 14 thin controllers; all 43 inventory operations live in OpenAPI and all 41 former stubs call real services |
| 12 | [Service and Handler Architecture](12-service-and-handler-architecture.md) | ✅ token pipeline and all feature services complete; every inventory action is live; SMTP queue, cleanup worker and HybridCache resolution wired |
| 13 | [API Response and Error Standards](13-api-response-and-error-standards.md) | ✅ one RFC 9457 envelope everywhere, typed feature errors included, `Documentation/Errors.md` catalogue + guard tests |
| 14 | [Middleware and Filters](14-middleware-and-filters.md) | ✅ pipeline assembled in order: correlation id, request logging, exception handler, security headers, rate limiting, CORS, authentication/authorization, CSRF filter + session-bound token service; `Pipeline.md` written. `AuditActionFilter` landed with §15; rate limiting with §17. Forwarded headers (§27) remains reserved ahead of IP-sensitive stages |

### Phase E — Cross-Cutting Concerns

| # | Workstream | Status |
|---|---|---|
| 15 | [Logging and Audit Trails](15-logging-and-audit-trails.md) | 🔄 Serilog, durable audit producers/querying and 90-day cleanup are wired; `admin_user_updated` remains an owner catalog decision |
| 16 | [Security Hardening](16-security-hardening.md) | 🔄 lockout and anti-enumeration are wired through real login/registration/reset paths; P14 still blocks external key-ring protection |
| 17 | [Rate Limiting and Abuse Prevention](17-rate-limiting-and-abuse-prevention.md) | 🔄 all four policies, 429 envelope, account/IP partitioning, documentation and matrix tests implemented; awaiting formal P6 approval and an owner decision on expanding the closed audit catalog for rate-limit events |

### Phase F — Documentation

| # | Workstream | Status |
|---|---|---|
| 18 | [Scalar and OpenAPI Configuration](18-scalar-and-openapi-configuration.md) | 🔄 v1 document transformers + bearer/cookie/API-key schemes + code-derived operation security; Scalar and JSON exposed in Development/Staging and tested absent in Production; awaiting formal P16 approval |
| 19 | [Endpoint-Level Markdown Documentation](19-endpoint-level-markdown-documentation.md) | 🔄 43/43 files, template and author guide complete; route/method/auth set equality and sixteen-section order enforced against generated OpenAPI; owner review of security narratives remains |

### Phase G — Testing

| # | Workstream | Status |
|---|---|---|
| 20 | [Unit Testing](20-unit-testing.md) | 🔄 206 tests; remaining per-rule validator rejection and dedicated TOTP replay suites are open |
| 21 | [Integration Testing](21-integration-testing.md) | 🔄 86 PostgreSQL-backed tests including registration parity/race, login/refresh/replay, reset/change revocation, sessions, TOTP, API keys, passkey/social contracts, admin flows and cleanup |
| 22 | [Security Testing](22-security-testing.md) | 🔄 attack suite and feature-flow coverage are live; oversized-body and captured-log scanning remain |
| 23 | [Performance and Load Testing](23-performance-and-load-testing.md) | 🔄 k6 + Argon2 harness ready; production-like endpoint baselines wait on P14 |

### Phase H — Operations

| # | Workstream | Status |
|---|---|---|
| 24 | [Docker and Local Development](24-docker-and-local-development.md) | ✅ image/Compose/HTTP/health and register→Mailpit→login workflow are service-backed |
| 25 | [Configuration and Secret Management](25-configuration-and-secret-management.md) | 🔄 typed validation + reference + secret gate complete; formal P7 decision open |
| 26 | [CI/CD](26-ci-cd.md) | 🔄 workflow and local gates verified; GitHub run/branch protection require owner push |
| 27 | [Deployment and Production Readiness](27-deployment-and-production-readiness.md) | 🔄 target-neutral checklist/runbooks/proxy trust done; P7/P14 block platform deploy and key-ring protector |
| 28 | [Monitoring, Metrics, Tracing, and Health Checks](28-monitoring-metrics-tracing-and-health-checks.md) | 🔄 health/OTel/auth metrics/alerts and login/MFA call sites done; P10 backend acceptance remains |

### Phase I — Longevity

| # | Workstream | Status |
|---|---|---|
| 29 | [Maintenance and Future Extensibility](29-maintenance-and-future-extensibility.md) | 🔄 cadence, secured future backlog and close-out checklist written; owner review/execution remain |

## What happens next

1. ~~**Answer the blocking decisions.**~~ ✅ **Done 2026-07-22.** P1 (7-day absolute cap), P2 (URL-segment versioning), P3 (all four directories), P4 (layout — later revised to flat by ADR-0018) were all approved as recommended and recorded in [`Documentation/Decisions/`](../Documentation/Decisions/README.md). Phases A–B are unblocked. Remaining open items are P5–P18, each answered on its own workstream's schedule.
2. ~~**Execute Phase A** (§1–§3).~~ ✅ **Done 2026-07-22.** §1: 17 ADRs + `Documentation/Scope.md` (owner review of the scope doc still outstanding). §2: versions pinned centrally, build clean under warnings-as-errors, transitive security pin for `Microsoft.OpenApi`. §3: `.slnx` solution, flat root layout (ADR-0018), 16-line composition root, both test projects green, template sample deleted. §4 is written: `Documentation/Architecture/Authentication.md`, five token interfaces, three options classes; P12/P13/P17 resolved. **Next up: §5** — the authorization and permission model.
3. **Execute Phase B** (§4–§5): both written. §4 — token lifecycle, rotation, reuse detection, key ring, dual transport, CSRF, step-up. §5 — permission catalog, role map, policy provider, step-up handler, 20 unit tests. Owner review of both architecture documents is outstanding. **Next: Phase C (§6)** — the 13 entities, which §4's design now fully specifies.
4. **Execute Phase C** (§6–§8): entities → EF configurations → initial migration + seeding. First runnable, migrated database.
5. **Build features as vertical slices** through Phases D–G: for each feature (auth core → sessions → verification/reset → users → MFA → social → passkeys → API keys → admin), the controller, DTOs, validators, services, tests, and its endpoint Markdown doc land **in the same PR** — that same-PR rule is what keeps `Documentation/` and the OpenAPI document from drifting.
6. **Operational close-out** (Phase H): compose environment early (§24 can start alongside Phase C), CI as soon as the first tests exist (§26), deployment after P14 is decided (§27), monitoring wiring (§28).
7. **v1 close** (Phase I): run the §29 close-out checklist, groom the future-work backlog (organizations/multi-tenancy, M2M, Redis scale-out, …).

Implementation is active. Pending decisions remain pending unless the project owner explicitly approves them; code that follows a recommendation is marked 🔄 rather than ✅ until that approval is recorded.
