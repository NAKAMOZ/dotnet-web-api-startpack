# ADR-0011: xUnit with Testcontainers, Docker Compose Locally, GitHub Actions in CI

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, rows *Testing* and *Containers / CI*
- **Affects:** §20–§23 (testing), §24 (Docker), §26 (CI/CD)

## Context

For an authentication system, tests are the primary evidence that the security design actually holds. §22 exists specifically to prove attacks fail — token reuse detection fires, `alg: none` is rejected, an expired session cannot refresh. Those assertions are only worth anything if the system under test behaves like production.

The usual shortcut is an in-memory or SQLite database for integration tests. That trades fidelity for speed in exactly the wrong place: `citext` uniqueness, `jsonb` columns, and Postgres concurrency semantics ([ADR-0008](ADR-0008-persistence-postgresql-efcore.md)) are load-bearing here, and a test suite that green-lights behaviour the production database would reject is worse than no suite at all.

Testing and containerisation are one decision because the test strategy is what makes Docker a hard dependency rather than a deployment convenience.

## Decision

**xUnit** (`xunit.v3`) as the test framework, across two projects: `tests/UnitTests/` and `tests/IntegrationTests/` ([ADR-0014](ADR-0014-solution-layout-and-directories.md)).

**Unit tests** cover validators, mapping extensions, token logic, and service behaviour in isolation. Time-dependent logic — session expiry, token TTLs, lockout windows — is tested through `TimeProvider` with `Microsoft.Extensions.TimeProvider.Testing`, never `Thread.Sleep`.

**Integration tests** run the real application through `WebApplicationFactory` against **a real PostgreSQL instance provisioned by Testcontainers**, with `Respawn` resetting state between tests.

**Docker + docker-compose** for local development: API, PostgreSQL, and Mailpit for email capture (§24).

**GitHub Actions** for CI (§26): build with warnings-as-errors, run both test projects, and audit dependencies for known vulnerabilities.

## Alternatives considered

**EF Core in-memory provider for integration tests.** Fast and zero-setup. Rejected: it is not a relational database. It does not enforce foreign keys or unique constraints the way Postgres does, has no `citext` and no `jsonb`, and would silently pass tests asserting behaviour production rejects.

**SQLite in-memory.** Closer to a real database, still not this one. Same rejection as above, for the same reason — the test would validate a schema the production system does not have.

**A shared developer/CI PostgreSQL instance.** No container overhead. Rejected: shared mutable state across concurrent test runs produces order-dependent flakiness, and CI runs cannot be isolated from each other. Testcontainers gives each run a private database.

**Mocking `DbContext` in integration tests.** Rejected outright — it would test the mock's behaviour, not the application's.

**Other CI providers** (Azure DevOps, GitLab CI). No technical objection; GitHub Actions was chosen for proximity to where the repository lives.

## Consequences

- **Docker is a hard prerequisite** for running the integration suite, locally and in CI. A developer without a container runtime can build and unit test but cannot run integration tests. This is documented in the quickstart (§24) rather than discovered.
- Integration tests are slower than in-memory equivalents. Accepted: they test the real thing, and unit tests carry the fast-feedback load.
- Testcontainers must use a Postgres image with `citext` available and apply migrations at fixture setup, or tests diverge from production schema.
- `Respawn` between tests means tests can share a container without sharing state — the setup cost is paid once per run, not once per test.
- Standardising on `TimeProvider` is a design constraint that reaches into production code: services must take `TimeProvider` rather than calling `DateTime.UtcNow`. Session and token expiry are otherwise untestable without real waiting.
- Mailpit in compose means email flows (verification, reset) are exercised end to end locally without an external provider, keeping P8 open without blocking development.
- CI running warnings-as-errors (§2) means a warning breaks the build — deliberate, and the reason the setting is solution-wide rather than per-project.
