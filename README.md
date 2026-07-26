# dotnet-web-api-startpack

[![CI](actions/workflows/ci.yml/badge.svg)](actions/workflows/ci.yml)

A headless authentication and authorization REST API on .NET 10 — email/password, sessions,
roles, email verification, password reset, TOTP MFA, social login, passkeys, and API keys.
Architecturally inspired by Better Auth; no Better Auth source is copied.

No user interface ships with it. Consumers build their own.

**Status:** API plumbing, token infrastructure, cross-cutting controls, OpenAPI/Scalar, and
all 43 endpoint contracts are present. The feature services are still being built, so 41
actions intentionally return `501 Not Implemented`. See
[`ROADMAP/README.md`](ROADMAP/README.md) for the workstream board.

## Quickstart

```bash
git clone <repo-url>
cd dotnet-web-api-startpack
cp .env.example .env                  # optional local port overrides
docker compose up --build
```

This starts the API on `http://localhost:5035`, PostgreSQL on `localhost:55432`, and
Mailpit at `http://localhost:8025`. In Development the API applies EF migrations and
idempotently seeds the documented local accounts.

```bash
curl --fail http://localhost:5035/health/ready
curl http://localhost:5035/openapi/v1.json
```

In Development or Staging, browse the interactive Scalar UI at
`http://localhost:5035/scalar/v1`. Paste a JWT into the `bearer` scheme, use the
browser-managed `cookie` scheme, or supply an `apiKey`. Both the OpenAPI document and Scalar
are intentionally unavailable in Production.

## Requirements

- .NET SDK 10
- Docker — required for integration tests (Testcontainers spins up a real PostgreSQL)
- An IDE understanding `.slnx`: VS 2022 17.14+, Rider 2025.1+, or VS Code

The registration and login services are live. The complete request sequence is prepared in
[`http/Auth.http`](http/Auth.http); retrieve the verification link from Mailpit and
continue with the login request. See
[`Documentation/Operations/LocalDevelopment.md`](Documentation/Operations/LocalDevelopment.md)
for the fast inner loop and troubleshooting.

## Common commands

| | |
|---|---|
| Build | `dotnet build` |
| Test | `dotnet test` |
| Run | `dotnet run` |
| Watch | `dotnet watch run` |
| Local stack | `docker compose up --build` |
| Local stack + OTLP debug collector | `docker compose -f docker-compose.yml -f docker-compose.observability.yml up --build` |
| Infrastructure only | `docker compose up -d postgres mailpit` |

## Layout

```text
dotnet-web-api-startpack.csproj   the API project (at the root)
tests/              unit and integration tests
Documentation/      architecture decision records, scope, per-endpoint docs
http/               .http request files per controller
ROADMAP/            the 29 workstreams
```

## Where to start reading

- [`Documentation/Scope.md`](Documentation/Scope.md) — what v1 does and deliberately does not do.
- [`Documentation/Decisions/`](Documentation/Decisions/README.md) — why each architectural choice was made.
- [`ROADMAP/00-overview.md`](ROADMAP/00-overview.md) — entity model and full endpoint inventory.
- [`CLAUDE.md`](CLAUDE.md) — build enforcement rules that will fail your build if ignored.

## Licence

Not yet chosen — to be decided before any public release.
