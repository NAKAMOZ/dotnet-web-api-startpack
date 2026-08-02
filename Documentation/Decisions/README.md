# Architecture Decision Records

This directory is the **durable record** of every architectural decision made for this project.

`ROADMAP/` is a planning artifact — workstream §29 archives it once v1 closes. These ADRs outlive it. When the roadmap and an ADR disagree, the ADR wins; when neither covers a question, it is an open decision and needs a new ADR before code is written.

## Format

One decision per file, one page each, four mandated sections: **Context**, **Decision**, **Alternatives considered**, **Consequences**. Each file carries a metadata block naming its status, date, deciders, the roadmap source it was lifted from, and the workstreams it affects.

## Numbering rules

- Numbers are **monotonic and never reused**, including for withdrawn or superseded decisions.
- A decision that replaces an earlier one does **not** edit or delete it. The old file's status becomes `Superseded by ADR-XXXX`; the new file names what it supersedes.
- All former pending decisions **P5–P18** are recorded here. The convention was: a decision that *completed* an approved-decisions row folded into that row's ADR; one with no corresponding row received its own.

## Index

| ADR | Title | Status | Source |
|---|---|---|---|
| [0001](ADR-0001-token-strategy.md) | Token strategy — JWT access + opaque rotating refresh | Accepted | Approved-decisions table: *Token strategy* |
| [0002](ADR-0002-session-lifetime-and-model.md) | Session lifetime and multi-device session model | Accepted | Approved-decisions table: *Session lifetime*, *Session model*; resolves **P1** |
| [0003](ADR-0003-token-transport.md) | Dual token transport — cookies and bearer | Accepted | Approved-decisions table: *Token transport* |
| [0004](ADR-0004-signing-key-management.md) | ES256 signing-key ring, `kid` rotation, JWKS | Accepted | Approved-decisions table: *Signing keys* |
| [0005](ADR-0005-custom-user-store.md) | Custom user store instead of ASP.NET Core Identity | Accepted | Approved-decisions table: *User store* |
| [0006](ADR-0006-password-hashing.md) | Argon2id password hashing with versioned parameters | Accepted | Approved-decisions table: *Password hashing* |
| [0007](ADR-0007-runtime-and-api-style.md) | .NET 10 runtime, controllers-only, RFC 9457 errors | Accepted | Approved-decisions table: *Runtime*, *API style* |
| [0008](ADR-0008-persistence-postgresql-efcore.md) | PostgreSQL and EF Core as the persistence stack | Accepted | Approved-decisions table: *Database* |
| [0009](ADR-0009-validation-and-mapping.md) | FluentValidation and manual mapping extensions | Accepted | Approved-decisions table: *Validation*, *Mapping* |
| [0010](ADR-0010-logging-serilog.md) | Serilog structured logging | Accepted | Approved-decisions table: *Logging* |
| [0011](ADR-0011-testing-and-ci.md) | xUnit + Testcontainers testing stack, Docker + GitHub Actions | Accepted | Approved-decisions table: *Testing*, *Containers / CI* |
| [0012](ADR-0012-api-documentation.md) | Scalar over built-in OpenAPI plus per-endpoint Markdown | Accepted | Approved-decisions table: *API documentation* |
| [0013](ADR-0013-package-manifest.md) | Package manifest and Central Package Management | Accepted | Workstream §2 |
| [0014](ADR-0014-solution-layout-and-directories.md) | Solution layout and directory structure | **Partially superseded by 0018** — the P3 directory set stands; the P4 `src/` layout does not | Resolves **P3**, **P4** |
| [0015](ADR-0015-api-versioning.md) | URL-segment API versioning | Accepted | Resolves **P2** |
| [0016](ADR-0016-caching-strategy.md) | In-memory `HybridCache` as the v1 caching layer | Superseded for multi-node deployments by 0029 | Resolves **P5** |
| [0017](ADR-0017-load-testing-tool.md) | k6 as the load-testing tool | Accepted | Resolves **P15** |
| [0018](ADR-0018-flat-repository-layout.md) | Flat repository layout — API project at the root | Accepted | Supersedes the layout half of **0014** |
| [0019](ADR-0019-social-login.md) | Social login — Google + GitHub, API-driven redirect | Accepted | Resolves **P12**, **P13** |
| [0020](ADR-0020-signing-key-storage.md) | Signing keys protected by Data Protection | Superseded in production by 0027; abstraction remains | Resolves **P17**; completes **0004** |
| [0021](ADR-0021-data-protection-key-persistence.md) | Data Protection key ring persisted to the database | Superseded in production by 0027; persistence remains | Closes persistence; 0027 adds Key Vault wrapping |
| [0022](ADR-0022-coverlet-coverage-gate.md) | Coverlet for scoped CI coverage enforcement | Accepted | Workstreams §20 and §26 |
| [0023](ADR-0023-backend-neutral-opentelemetry.md) | Backend-neutral OpenTelemetry pipeline | Accepted | Workstream §28; 0028 later resolves **P10** |
| [0024](ADR-0024-smtp-email-delivery.md) | SMTP email delivery with Mailpit in development | Accepted | Resolves **P8** |
| [0025](ADR-0025-in-process-auth-cleanup.md) | In-process background cleanup for authentication artifacts | Accepted | Resolves **P9** |
| [0026](ADR-0026-preserve-current-session-on-password-change.md) | Preserve current session on deliberate password change | Accepted | Supersedes ADR-0002's password-change revocation rule |
| [0027](ADR-0027-azure-production-platform.md) | Azure Container Apps platform and Key Vault boundary | Accepted | Resolves **P7**, **P14** |
| [0028](ADR-0028-azure-monitor-backend.md) | Azure Monitor production observability | Accepted | Resolves **P10** |
| [0029](ADR-0029-distributed-redis-runtime-state.md) | Azure Managed Redis distributed runtime state | Accepted | Resolves **P6**; supersedes the in-memory-only boundary of 0016 |
| [0030](ADR-0030-no-message-broker.md) | No message broker in v1 | Accepted | Resolves **P11** |
| [0031](ADR-0031-openapi-exposure.md) | Scalar/OpenAPI only in development and staging | Accepted | Resolves **P16** |

The remaining v1 feature scope — the approved-decisions row *v1 feature scope* — is a scope statement rather than an architectural decision and lives in [`../Scope.md`](../Scope.md).

## Decisions still open

No v1 technology decision remains open. P1–P18 are resolved; explicitly out-of-scope
capabilities still re-enter consultation when their trigger occurs.

## Architecture documents

Longer-form design that a one-page ADR cannot carry:

- [`../Architecture/Authentication.md`](../Architecture/Authentication.md) — the complete token lifecycle: sequence diagrams, `Session` and `RefreshToken` state machines, cookie matrix, claims table, revocation paths (§4).
- [`../Architecture/Authorization.md`](../Architecture/Authorization.md) — roles, the permission catalog, step-up, API-key scopes, deny-by-default (§5).
- [`../Architecture/DataAccess.md`](../Architecture/DataAccess.md) — EF Core mapping conventions, per-index rationale, cascade map, execution-strategy and tracking patterns (§7).
