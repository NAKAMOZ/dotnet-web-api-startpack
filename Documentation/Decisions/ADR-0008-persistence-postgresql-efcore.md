# ADR-0008: PostgreSQL and EF Core as the Persistence Stack

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Database*
- **Affects:** §6 (entities), §7 (EF configuration), §8 (migrations/seed), §21 (integration tests), §24 (compose)

## Context

The domain is thirteen relational entities with foreign keys, unique constraints, and lookup-heavy access patterns — sessions by user, refresh tokens by hash, accounts by provider pair, API keys by prefix. It needs transactional integrity: issuing a rotated refresh token while marking its predecessor used must be atomic, or reuse detection becomes unreliable.

Two decisions follow: which database, and who owns the schema.

## Decision

**PostgreSQL**, accessed through **EF Core** with the `Npgsql.EntityFrameworkCore.PostgreSQL` provider.

**EF Core owns the schema** — entities, relationships, indexes, and migrations. The database is never modified by hand; every schema change is a migration in `Data/Migrations/`, reviewed as code.

**One `IEntityTypeConfiguration<T>` per entity** in `Data/Configurations/`, rather than data annotations on the entity classes or a monolithic `OnModelCreating`. Entities stay POCOs; persistence concerns stay in the configuration layer.

**Email columns use `citext`.** Case-insensitive comparison belongs in the column type, not in every query. `SELECT ... WHERE lower(email) = lower(@p)` is easy to write correctly once and easy to get wrong the fourth time — and getting it wrong on a uniqueness check means two accounts for the same human.

Postgres-specific features are used where they earn their place: `citext` for emails, `jsonb` for `AuditLogEntry.Metadata`.

## Alternatives considered

**SQL Server.** Fully supported by EF Core and a perfectly good fit. Rejected on licensing and container ergonomics for local development and CI — Postgres runs free anywhere, which matters when every integration test spins up a real database (§21).

**SQLite.** Attractive for test speed. Rejected as a production store, and deliberately rejected for tests too: it lacks `citext`, `jsonb`, and Postgres's concurrency semantics, so tests would pass against a database that behaves differently from production. Testcontainers with real Postgres ([ADR-0011](ADR-0011-testing-and-ci.md)) is the answer instead.

**Dapper or raw ADO.NET.** Faster and more explicit per query. Rejected: this schema needs migrations, relationship mapping, and change tracking far more than it needs micro-optimised reads. EF Core's `IQueryable` composition also carries the paging and filtering work in the admin endpoints. Raw SQL remains available for specific hot paths if profiling ever demands it.

**Database-first schema management** (hand-written SQL, EF scaffolding from the database). Rejected: it splits schema authority between SQL scripts and code, and makes review of a schema change harder rather than easier.

**A document store.** Rejected — the domain is relational, and the integrity guarantees are the point.

## Consequences

- Migrations are reviewable artefacts, and every schema change has an author, a diff, and a rollback path.
- Uniqueness and foreign keys are enforced by the database, not by application code hoping to have checked first. Refresh-token hash uniqueness and the composite `UserRole` key are database constraints.
- Postgres-specific types mean **the API is not portable to another provider without work**. Accepted deliberately: `citext` and `jsonb` are worth more than provider neutrality nobody has asked for.
- `citext` requires the extension to be enabled in the initial migration (§8) — including in the Testcontainers image used by integration tests, or those tests fail on a database that does not match production.
- Integration tests need a real Postgres instance, which is why Testcontainers and Docker are part of the approved stack rather than optional extras.
- EF Core's default tracking behaviour is a performance footgun on read-heavy admin queries; `AsNoTracking` on read paths is a §12 convention, not an optimisation to discover later.
