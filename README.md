# dotnet-web-api-startpack

A headless authentication and authorization REST API on .NET 10 — email/password, sessions,
roles, email verification, password reset, TOTP MFA, social login, passkeys, and API keys.
Architecturally inspired by Better Auth; no Better Auth source is copied.

No user interface ships with it. Consumers build their own.

**Status:** API plumbing, token infrastructure, cross-cutting controls, OpenAPI/Scalar, and
all 43 endpoint contracts are present. The feature services are still being built, so 41
actions intentionally return `501 Not Implemented`. See
[`ROADMAP/README.md`](ROADMAP/README.md) for the workstream board.

## Quickstart

> Stub — the compose steps land in §24, which adds `docker-compose.yml` with PostgreSQL and Mailpit.

```bash
git clone <repo-url>
cd dotnet-web-api-startpack

# TODO §24: docker compose up -d      # PostgreSQL + Mailpit
# TODO §8:  dotnet ef database update

dotnet run
```

The API starts on `http://localhost:5035` (HTTPS profile on 7052).

```bash
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

## Common commands

| | |
|---|---|
| Build | `dotnet build` |
| Test | `dotnet test` |
| Run | `dotnet run` |
| Watch | `dotnet watch run` |

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
