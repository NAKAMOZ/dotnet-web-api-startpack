# 24. Docker and Local Development

## Objective

One-command local environment identical in shape to production: API + PostgreSQL + mail catcher.

## Scope

Dockerfile, docker-compose, `.http` request files, developer quickstart.

## Architectural Decisions

- Multi-stage `Dockerfile`: digest-pinned Node 24 frontend plus .NET 10 SDK build/publish → digest-pinned ASP.NET 10 runtime, non-root user, `HEALTHCHECK` hitting `/health/live`.
- `docker-compose.yml`: `api` (built), digest-pinned `postgres:18-alpine` (PostgreSQL 18 volume layout, healthcheck `pg_isready`, named volume, `citext` available by default), digest-pinned `axllent/mailpit:v1.30.5` (SMTP :1025, UI :8025). API waits on postgres health; Dependabot monitors Dockerfile and Compose images weekly.
- `.env.example` documents every compose-injected variable; `.env` git-ignored.
- Two dev modes documented: full compose, or `dotnet watch` against compose-run postgres+mailpit only (fast inner loop).
- `.http` files per feature in `http/` (`auth.http`, `sessions.http`, `mfa.http`, …) with shared variables file — replaces the template's single `.http`.

## Technology Decisions Requiring Approval

None (Docker approved).

## Tasks

- [x] `Dockerfile` (multi-stage, non-root, healthcheck).
- [x] `docker-compose.yml` + `.env.example`.
- [x] `http/*.http` covering every inventory endpoint with example payloads (kept in sync by §19 review checklist).
- [x] `README.md` quickstart: prerequisites, `docker compose up`, first request walkthrough (register → verify via Mailpit UI → login), inner-loop mode.
- [x] Verify Testcontainers (§21) and compose coexist (no port collisions; document ports).

**Verified 2026-08-02:** the digest-pinned image built, ran as UID 1654, and the full stack reported
`/health/ready` healthy against PostgreSQL. The local machine already owned port 5432, so
Compose now defaults its host-side mapping to 55432 while PostgreSQL remains on 5432 inside
the network; Testcontainers independently uses a random host port. SPA deep links resolve to
the prerendered shell and a genuinely chunked oversized request returns the bounded `413`
problem. The §12 registration, Mailpit verification and login services complete the
documented walkthrough.

## Expected Deliverables

Dockerfile, compose file, env example, `.http` suite, completed README quickstart.

## Dependencies

§3 (layout), §8 (migrations on startup in dev).

## Security Considerations

Compose is dev-only: dev credentials obviously fake, ports bound to localhost, `.env` never committed. The image itself is production-grade (non-root, minimal base) — same artifact CI publishes (§26).

## Testing Requirements

CI job builds the image; smoke test: container starts, `/health/ready` green against compose postgres.

## Documentation Requirements

README quickstart; `Documentation/Operations/LocalDevelopment.md` for the longer version.

## Definition of Done

`git clone` → `docker compose up` → register/login flow works via `.http` files with zero manual setup.

## Questions for the Project Owner

None.
