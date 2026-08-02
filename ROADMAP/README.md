# Roadmap Status Board

This folder contains the implementation roadmap for the Better Auth–inspired authentication & authorization REST API, split into one file per workstream. Start with [00-overview.md](00-overview.md) — it holds P1–P18 decision traceability, the target directory structure, the entity model, and the full endpoint inventory that all workstreams reference.

## Current status

| | |
|---|---|
| **Planning** | ✅ Complete — all 29 workstreams specified |
| **Technology consultation** | ✅ Complete — core stack approved by the project owner (see the approved-decisions table in the overview) |
| **Decision record** | ✅ 31 ADRs + authentication, authorization, data, service and operations architecture in [`Documentation/Decisions/`](../Documentation/Decisions/README.md) — the durable record; this roadmap is archived only at v1 close (§29) |
| **Open decisions** | ✅ None — P1–P18 are resolved |
| **Implementation** | ✅ **All 43 operations and required security/operations code are implemented** — 379 tests green (274 unit, 105 integration). Full email/cookie/MFA/social/WebAuthn/admin flows, replay defenses, 64 KiB input cap, log redaction, least-privilege DB deployment, shared Redis state, Azure Key Vault/Monitor/Bicep and migration-first CD are present. Local refresh/profile budgets pass; the 50 RPS single-node login failure is retained as honest capacity evidence. External Azure/GitHub rollout evidence, formal owner reviews, license selection and release sign-off remain. |

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
| 14 | [Middleware and Filters](14-middleware-and-filters.md) | ✅ ordered pipeline includes validated forwarded headers, correlation/logging, 64 KiB request cap, exception/security headers, rate limiting, CORS, authentication/authorization, CSRF, validation and audit filters |

### Phase E — Cross-Cutting Concerns

| # | Workstream | Status |
|---|---|---|
| 15 | [Logging and Audit Trails](15-logging-and-audit-trails.md) | ✅ complete catalog/producers, query API, safe failure logging, full-flow redaction and 90-day cleanup |
| 16 | [Security Hardening](16-security-hardening.md) | ✅ lockout/enumeration defenses, credential notifications, replay controls, admin step-up and Key Vault-wrapped shared Data Protection ring |
| 17 | [Rate Limiting and Abuse Prevention](17-rate-limiting-and-abuse-prevention.md) | ✅ all policies, account/IP partitioning, RFC 9457 rejection and atomic multi-replica Redis counters with concurrency tests |

### Phase F — Documentation

| # | Workstream | Status |
|---|---|---|
| 18 | [Scalar and OpenAPI Configuration](18-scalar-and-openapi-configuration.md) | ✅ code-derived v1 contract/security; Scalar/OpenAPI present in Development/Staging and tested absent in Production (ADR-0031) |
| 19 | [Endpoint-Level Markdown Documentation](19-endpoint-level-markdown-documentation.md) | 🔄 43/43 files, template and author guide complete; route/method/auth set equality and sixteen-section order enforced against generated OpenAPI; owner review of security narratives remains |

### Phase G — Testing

| # | Workstream | Status |
|---|---|---|
| 20 | [Unit Testing](20-unit-testing.md) | ✅ 274 tests including every-validator rejection matrix, options boundaries, crypto, policies, audit failures and architecture guards |
| 21 | [Integration Testing](21-integration-testing.md) | ✅ 105 PostgreSQL/Redis-backed tests covering all real feature ceremonies and deployment invariants |
| 22 | [Security Testing](22-security-testing.md) | ✅ attack matrix, replay/concurrency, input abuse, authorization, enumeration and captured full-flow secret scan |
| 23 | [Performance and Load Testing](23-performance-and-load-testing.md) | 🔄 local baselines recorded; refresh/profile pass, single-node 50 RPS login fails as expected; Azure staging workflow is the remaining capacity gate |

### Phase H — Operations

| # | Workstream | Status |
|---|---|---|
| 24 | [Docker and Local Development](24-docker-and-local-development.md) | ✅ image/Compose/HTTP/health and register→Mailpit→login workflow are service-backed |
| 25 | [Configuration and Secret Management](25-configuration-and-secret-management.md) | ✅ typed startup validation/reference/secret scan plus Key Vault references and managed identity (ADR-0027) |
| 26 | [CI/CD](26-ci-cd.md) | 🔄 CI/deploy/performance workflows implemented and locally validated; live GitHub runs/branch rules require repository-owner action |
| 27 | [Deployment and Production Readiness](27-deployment-and-production-readiness.md) | 🔄 Azure IaC and migration-first rollback-capable deploy are compiler-validated; real subscription rollout/restore/runbook evidence remains |
| 28 | [Monitoring, Metrics, Tracing, and Health Checks](28-monitoring-metrics-tracing-and-health-checks.md) | 🔄 PostgreSQL/Redis readiness, OTel and Azure Monitor export are implemented; live dashboard/alert ownership evidence remains |

### Phase I — Longevity

| # | Workstream | Status |
|---|---|---|
| 29 | [Maintenance and Future Extensibility](29-maintenance-and-future-extensibility.md) | 🔄 cadence, secured future backlog and close-out checklist written; owner review/execution remain |

## What happens next

1. Provision the `staging` GitHub Environment/Azure subscription, push a green CI commit to
   `main`, and observe the chained staging `deploy.yml` run.
2. Run the staging ZAP and all k6 scenarios; tune replica capacity from evidence without
   weakening Argon2, then repeat against production-equivalent sizing.
3. Rehearse backup restore, Redis/Key Vault dependency recovery, mass revocation, key
   compromise and automatic image rollback; record owners and RPO/RTO.
4. Apply branch protection/required checks, choose the repository software licence, complete
   the named architecture/security owner reviews and sign `V1Closeout.md`.
5. Tag the immutable v1 release and archive this roadmap; ADRs remain the durable record.
