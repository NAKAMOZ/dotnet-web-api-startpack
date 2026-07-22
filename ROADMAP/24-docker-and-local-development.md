# 24. Docker and Local Development

## Objective

One-command local environment identical in shape to production: API + PostgreSQL + mail catcher.

## Scope

Dockerfile, docker-compose, `.http` request files, developer quickstart.

## Architectural Decisions

- Multi-stage `Dockerfile`: `mcr.microsoft.com/dotnet/sdk:10.0` build/publish → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime, non-root user, `HEALTHCHECK` hitting `/health/live`.
- `docker-compose.yml`: `api` (built), `postgres` (`postgres:17-alpine`, healthcheck `pg_isready`, named volume, `citext` available by default), `mailpit` (SMTP :1025, UI :8025). API waits on postgres health.
- `.env.example` documents every compose-injected variable; `.env` git-ignored.
- Two dev modes documented: full compose, or `dotnet watch` against compose-run postgres+mailpit only (fast inner loop).
- `.http` files per feature in `src/Api/HttpRequests/` (`auth.http`, `sessions.http`, `mfa.http`, …) with shared variables file — replaces the template's single `.http`.

## Technology Decisions Requiring Approval

None (Docker approved).

## Tasks

- [ ] `Dockerfile` (multi-stage, non-root, healthcheck).
- [ ] `docker-compose.yml` + `.env.example`.
- [ ] `src/Api/HttpRequests/*.http` covering every inventory endpoint with example payloads (kept in sync by §19 review checklist).
- [ ] `README.md` quickstart: prerequisites, `docker compose up`, first request walkthrough (register → verify via Mailpit UI → login), inner-loop mode.
- [ ] Verify Testcontainers (§21) and compose coexist (no port collisions; document ports).

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
