# Roadmap Status Board

This folder contains the implementation roadmap for the Better Auth–inspired authentication & authorization REST API, split into one file per workstream. Start with [00-overview.md](00-overview.md) — it holds the approved decisions, the pending decisions (P1–P18), the target directory structure, the entity model, and the full endpoint inventory that all workstreams reference.

## Current status

| | |
|---|---|
| **Planning** | ✅ Complete — all 29 workstreams specified |
| **Technology consultation** | ✅ Complete — core stack approved by the project owner (see the approved-decisions table in the overview) |
| **Open decisions** | ⏳ 18 items (P1–P18) await owner approval; P1–P4 block Phase A |
| **Implementation** | ⬜ Not started |

## How to use this board

- Each workstream file ends with a **Definition of Done**. A workstream is marked ✅ here only when its Definition of Done is met — not when its code merely exists.
- Mark 🔄 when a workstream has open PRs or partial task completion; keep the task checkboxes inside the workstream file itself as the fine-grained record.
- Do not start a workstream whose **Dependencies** or blocking **Pending Decisions** are unresolved.

Legend: ⬜ not started · 🔄 in progress · ✅ done (DoD met) · ⏳ blocked on a pending decision

## Workstreams

### Phase A — Foundation

| # | Workstream | Status |
|---|---|---|
| 1 | [Requirements and Architectural Decisions](01-requirements-and-architectural-decisions.md) | ⬜ |
| 2 | [Technology Selection](02-technology-selection.md) | ⬜ |
| 3 | [Solution and Project Structure](03-solution-and-project-structure.md) | ⏳ P2, P3, P4 |

### Phase B — Architecture

| # | Workstream | Status |
|---|---|---|
| 4 | [Authentication and Token Architecture](04-authentication-and-token-architecture.md) | ⏳ P1, P12, P13, P17 |
| 5 | [Authorization and Permissions](05-authorization-and-permissions.md) | ⬜ |

### Phase C — Data

| # | Workstream | Status |
|---|---|---|
| 6 | [Domain and Entity Modeling](06-domain-and-entity-modeling.md) | ⬜ |
| 7 | [Entity Framework Core Configuration](07-entity-framework-core-configuration.md) | ⬜ |
| 8 | [Database Migrations and Seed Data](08-database-migrations-and-seed-data.md) | ⬜ |

### Phase D — API Plumbing

| # | Workstream | Status |
|---|---|---|
| 9 | [DTO Organization](09-dto-organization.md) | ⬜ |
| 10 | [Validation](10-validation.md) | ⬜ |
| 11 | [Controller Architecture](11-controller-architecture.md) | ⬜ |
| 12 | [Service and Handler Architecture](12-service-and-handler-architecture.md) | ⬜ |
| 13 | [API Response and Error Standards](13-api-response-and-error-standards.md) | ⬜ |
| 14 | [Middleware and Filters](14-middleware-and-filters.md) | ⬜ |

### Phase E — Cross-Cutting Concerns

| # | Workstream | Status |
|---|---|---|
| 15 | [Logging and Audit Trails](15-logging-and-audit-trails.md) | ⬜ |
| 16 | [Security Hardening](16-security-hardening.md) | ⬜ |
| 17 | [Rate Limiting and Abuse Prevention](17-rate-limiting-and-abuse-prevention.md) | ⬜ |

### Phase F — Documentation

| # | Workstream | Status |
|---|---|---|
| 18 | [Scalar and OpenAPI Configuration](18-scalar-and-openapi-configuration.md) | ⬜ |
| 19 | [Endpoint-Level Markdown Documentation](19-endpoint-level-markdown-documentation.md) | ⬜ |

### Phase G — Testing

| # | Workstream | Status |
|---|---|---|
| 20 | [Unit Testing](20-unit-testing.md) | ⬜ |
| 21 | [Integration Testing](21-integration-testing.md) | ⬜ |
| 22 | [Security Testing](22-security-testing.md) | ⬜ |
| 23 | [Performance and Load Testing](23-performance-and-load-testing.md) | ⬜ |

### Phase H — Operations

| # | Workstream | Status |
|---|---|---|
| 24 | [Docker and Local Development](24-docker-and-local-development.md) | ⬜ |
| 25 | [Configuration and Secret Management](25-configuration-and-secret-management.md) | ⬜ |
| 26 | [CI/CD](26-ci-cd.md) | ⬜ |
| 27 | [Deployment and Production Readiness](27-deployment-and-production-readiness.md) | ⏳ P14 |
| 28 | [Monitoring, Metrics, Tracing, and Health Checks](28-monitoring-metrics-tracing-and-health-checks.md) | ⬜ |

### Phase I — Longevity

| # | Workstream | Status |
|---|---|---|
| 29 | [Maintenance and Future Extensibility](29-maintenance-and-future-extensibility.md) | ⬜ |

## What happens next

1. **Answer the blocking decisions.** P1 (session absolute cap), P2 (URL-segment versioning), P3 (additional directories), P4 (`src/` layout) unblock Phases A–B. Each pending decision already carries a recommendation in [00-overview.md](00-overview.md) — approving the recommendations as-is is a valid fast path.
2. **Execute Phase A** (§1–§3): record decisions as ADRs, pin the package manifest, `git init`, build the solution skeleton with the composition-root `Program.cs`, delete the template sample code.
3. **Execute Phase B** (§4–§5): write the authentication architecture document (token lifecycle, rotation, reuse detection, key ring, dual transport, CSRF) and the authorization model; owner reviews before any data work starts.
4. **Execute Phase C** (§6–§8): entities → EF configurations → initial migration + seeding. First runnable, migrated database.
5. **Build features as vertical slices** through Phases D–G: for each feature (auth core → sessions → verification/reset → users → MFA → social → passkeys → API keys → admin), the controller, DTOs, validators, services, tests, and its endpoint Markdown doc land **in the same PR** — that same-PR rule is what keeps `Documentation/` and the OpenAPI document from drifting.
6. **Operational close-out** (Phase H): compose environment early (§24 can start alongside Phase C), CI as soon as the first tests exist (§26), deployment after P14 is decided (§27), monitoring wiring (§28).
7. **v1 close** (Phase I): run the §29 close-out checklist, groom the future-work backlog (organizations/multi-tenancy, M2M, Redis scale-out, …).

Implementation has **not** started and does not start until the project owner explicitly requests it.
