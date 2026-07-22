# Architecture Decision Records

This directory is the **durable record** of every architectural decision made for this project.

`ROADMAP/` is a planning artifact — workstream §29 archives it once v1 closes. These ADRs outlive it. When the roadmap and an ADR disagree, the ADR wins; when neither covers a question, it is an open decision and needs a new ADR before code is written.

## Format

One decision per file, one page each, four mandated sections: **Context**, **Decision**, **Alternatives considered**, **Consequences**. Each file carries a metadata block naming its status, date, deciders, the roadmap source it was lifted from, and the workstreams it affects.

## Numbering rules

- Numbers are **monotonic and never reused**, including for withdrawn or superseded decisions.
- A decision that replaces an earlier one does **not** edit or delete it. The old file's status becomes `Superseded by ADR-XXXX`; the new file names what it supersedes.
- `ADR-0013` is **reserved** for workstream §2 (`ADR-0013-package-manifest.md`) and is intentionally absent until §2 lands. Do not fill the gap.

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
| — | *reserved — `ADR-0013-package-manifest.md`, workstream §2* | Not yet written | — |
| [0014](ADR-0014-solution-layout-and-directories.md) | Solution layout and directory structure | Accepted | Resolves **P3**, **P4** |
| [0015](ADR-0015-api-versioning.md) | URL-segment API versioning | Accepted | Resolves **P2** |

The remaining v1 feature scope — the approved-decisions row *v1 feature scope* — is a scope statement rather than an architectural decision and lives in [`../Scope.md`](../Scope.md).

## Decisions still open

Pending decisions **P5–P18** carry recommendations in `ROADMAP/00-overview.md` but are not approved. Each is answered on the blocking schedule of the workstream that needs it, and each gets an ADR here when it is. Do not treat a recommendation as a decision.
