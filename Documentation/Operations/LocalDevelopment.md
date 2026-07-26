# Local Development

## Full stack

Requirements: Docker Compose v2.20+ and ports 5035, 55432, 8025, and 1025 available
(each is overridable in `.env`).

```bash
cp .env.example .env
docker compose up --build
curl --fail http://localhost:5035/health/ready
```

The API waits for PostgreSQL, migrates the `auth` schema in Development, and seeds the
two local accounts. Mailpit accepts SMTP on 1025 and displays messages at
<http://localhost:8025>. All published host ports bind to `127.0.0.1`.

Stop the stack with `docker compose down`. Add `--volumes` only when intentionally
discarding the local database; the named volume otherwise survives restarts.

## Fast inner loop

Run only infrastructure in containers:

```bash
docker compose up -d postgres mailpit
dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=127.0.0.1;Port=55432;Database=startpack;Username=startpack;Password=local-development-only"
dotnet watch run
```

The host process uses `appsettings.Development.json`, user-secrets, and the SMTP defaults
from `appsettings.json`. HTTPS is available through the launch profile on port 7052.

## Request walkthrough

Open [`../../http/Auth.http`](../../http/Auth.http), run Register, inspect Mailpit for the
verification token, run Confirm in `EmailVerification.http`, then run Login. Store returned
tokens only in `http/http-client.private.env.json`, which is gitignored.

The feature actions are live. The walkthrough exercises the SMTP queue through Mailpit,
email verification, login, token transport, CSRF, authorization, and rate-limit pipelines.

## Tests and ports

`dotnet test` uses Testcontainers with a random PostgreSQL host port, so it can run while
Compose owns 55432. One container is shared by the PostgreSQL integration collection and
Respawn resets application tables between tests.

Run one suite:

```bash
dotnet test tests/IntegrationTests/IntegrationTests.csproj \
  --filter "FullyQualifiedName~TokenServiceIntegrationTests"
```
