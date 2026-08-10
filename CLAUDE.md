# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

A Better Auth–inspired authentication & authorization REST API on .NET 10, plus a React
"API Workbench" SPA that ships inside the API artifact. **All 43 documented operations and
every cross-cutting workstream are implemented**; 379 tests green (274 unit, 105
integration), 31 ADRs, P1–P18 all resolved. What remains is owner action, not code: a
software licence, live Azure/GitHub rollout evidence, named security/architecture reviews,
and `V1Closeout.md` sign-off. `ROADMAP/README.md` is the status board and names exactly what
is left; where it disagrees with `Documentation/Decisions/`, the ADR wins.

Because everything is built, the useful failure mode is no longer "the stub is empty" — it
is "the invariant below was simplified away." The rest of this file is that list.

## Layout

**Flat — the API project is at the repository root** (ADR-0018 supersedes the `src/` layout
ADR-0014 originally approved).

```text
dotnet-web-api-startpack.csproj   the API project — RootNamespace is `Api`
dotnet-web-api-startpack.slnx     solution (XML format — see note under Commands)
Directory.Packages.props          all NuGet versions (CPM)
Directory.Build.props             solution-wide build settings
Program.cs                        composition root, 34 lines, zero logic
Extensions/                       the Add*/Use* methods Program.cs calls (22 files)
Controllers/ DTOs/ Validators/ Services/ Models/ Data/ Handlers/ Filters/ Middleware/ …
Configuration/                    strongly-typed, startup-validated options
Health/ BackgroundServices/ Logging/ Templates/
playground-ui/                    the Workbench — separate pnpm/Vite/TanStack Start project
wwwroot/playground/               its built output, COMMITTED (see the frontend section)
tests/UnitTests/ tests/IntegrationTests/ tests/load/
Documentation/                    ADRs, architecture/security/operations docs, 43 endpoint files
http/                             per-controller .http files
infra/azure/ deploy/ scripts/     Bicep, OTel collector config, secret-scan gate
ROADMAP/                          the 29 workstreams (planning artifact, archived at v1)
```

⚠️ **The project globs from the repository root**, so any new top-level directory containing
C# that is not application source **must** be added to `<DefaultItemExcludes>` in the csproj.
`tests/**`, `playground-ui/**`, `Documentation/**`, `ROADMAP/**`, `http/**` and `.github/**`
are already excluded — without that, test files compile into the API assembly and every type
is defined twice.

## Commands

Run from the repo root.

- **Run the API**: `dotnet run` — `http://localhost:5035` (`https` profile on 7052).
- **Build**: `dotnet build` · **Watch**: `dotnet watch run`
- **Test (all)**: `dotnet test` · **One suite**: `dotnet test tests/UnitTests/UnitTests.csproj`
- **One test class**: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~TokenServiceIntegrationTests"`
- **Adversarial suite only**: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter "Category=Security"`
- **Local infra**: `docker compose up --detach postgres mailpit` (Mailpit UI on 8025, Postgres on 55432)
- **Whole stack**: `docker compose up --build --detach` — API, Postgres, Mailpit
- **With OTLP export**: add `--file docker-compose.observability.yml`
- **OpenAPI / Scalar / Workbench**: `/openapi/v1.json`, `/scalar/v1`, `/playground/` (Development + Staging only)
- **Migrations**: `dotnet tool restore` once per clone, then `dotnet ef …`
- **Operational CLI** (deployed image): `dotnet dotnet-web-api-startpack.dll operations rotate-signing-key` / `retire-signing-keys` — `RunOperationalCommandAsync` returns before the pipeline is built.

**Reproduce CI locally** — these are the gates, and several fail on things `dotnet build`
does not check:

```bash
dotnet format dotnet-web-api-startpack.slnx --verify-no-changes --no-restore
dotnet list package --vulnerable --include-transitive --no-restore
bash scripts/check-secrets.sh
dotnet ef migrations has-pending-model-changes    # model/migration drift gate
cd playground-ui && pnpm check && pnpm typecheck && pnpm test && pnpm build
```

Integration tests need a reachable Docker daemon: Testcontainers starts PostgreSQL 18 and
Redis on random host ports (so they run beside the Compose stack), applies the real
migrations, and Respawn resets state between tests.

The solution file is **`.slnx`**, not `.sln` — the .NET 10 SDK's default format. Requires
SDK 10, VS 2022 17.14+, or Rider 2025.1+. The roadmap text says `.sln`; recorded deviation.

## Build enforcement — read before writing code

These will fail your build, by design:

- **No `Version` on a `PackageReference`.** Central Package Management is on; versions live in `Directory.Packages.props` and a local `Version` is error `NU1008`. Adding a package requires an ADR.
- **`TreatWarningsAsErrors`** — every compiler warning is an error.
- **File-scoped namespaces** (`IDE0161`) and **explicit accessibility modifiers** (`IDE0040`) are errors, enforced at build via `EnforceCodeStyleInBuild` + `.editorconfig`.
- **Unused usings** (`IDE0005`) are errors in application code, disabled under `tests/` — the rule only reports when `GenerateDocumentationFile` is true, which test projects switch off.
- **NuGet audit** runs at `mode=all`, `level=low`. A newly published advisory against any dependency, direct or transitive, **fails the build**. Fix by pinning the patched version, not by suppressing.
- **`dotnet format --verify-no-changes`** is a CI gate — whitespace CI failures are real.
- CI additionally gates: scoped **85% line coverage** on `Api.Services.Crypto.*` + `Api.Validators.*` (unit) and `Api.Services.Tokens.*` (integration); Trivy HIGH/CRITICAL on the container image; `pnpm audit --audit-level high`; a Compose readiness + oversized-body smoke test.
- `CS1591` (missing XML comment) is suppressed — doc comments are written where they carry meaning, not on every DTO property.

`Microsoft.OpenApi` is pinned to the **2.x** line. Do not "upgrade" it to 3.x —
`Microsoft.AspNetCore.OpenApi` 10.0.10 targets 2.x and its source generator breaks against
3.x. See `ADR-0013`.

## The frontend is part of the .NET build

`playground-ui/` is an independent pnpm project (React 19, TanStack Start/Router/Query,
Vite, Effect, Tailwind v4, shadcn/ui, Biome, Vitest, Paraglide i18n) that is nonetheless
built by MSBuild: the `BuildPlayground` target in the csproj runs `pnpm install
--frozen-lockfile && pnpm build` before `PrepareForBuild`. Consequences:

- **`dotnet build` needs Node 24 + pnpm 11 on PATH.** Pass `/p:SkipPlaygroundBuild=true` only when the static output is already present (CI does this in every .NET job; the Dockerfile supplies a prebuilt `wwwroot`).
- **`wwwroot/playground/` is committed**, and CI fails if rebuilding it produces a diff. `scripts/sync-static.mjs` exists to make that possible — it zeroes the prerender timestamp TanStack bakes into `index.html` so identical sources produce identical bytes. **If you change frontend source, rebuild and commit `wwwroot/playground/` in the same commit.**
- Frontend-only loop: `cd playground-ui && pnpm dev` (port 3000). Vite is pinned to `base: "/playground/"` and the router to `basepath: "/playground"`; the API serves it via `MapFallbackToFile`, so deep links like `/playground/endpoints/health-live` must return the same document as `/playground/`.
- `pnpm typecheck` and `pnpm test` run `pnpm paraglide` first — the generated `src/paraglide/` messages are not committed, and skipping that step is why a bare `tsc --noEmit` fails.
- `playground-ui/AGENTS.md` is a machine-generated TanStack "intent skills" index — commands to fetch guidance for a TanStack subsystem, not repo rules.

## Distributed runtime: Redis is optional locally, mandatory when scaled

`AddDistributedRuntimeServices` branches on `Redis:Enabled` (ADR-0029). Disabled →
`LocalRateLimiterPartitionFactory` + in-memory `HybridCache`. Enabled → a shared
`IConnectionMultiplexer`, Redis-backed `HybridCache`, and `RedisRateLimiterPartitionFactory`
over `RedisRateLimitStore`, whose windows are atomic across replicas (proved by
`RedisRateLimitStoreIntegrationTests` with a Redis Testcontainer). **A change to rate
limiting must be made in both factories** — a limit that holds on one node and not on four
is the bug this split exists to make visible.

`/health/live` says the process serves HTTP; `/health/ready` says PostgreSQL/migrations are
healthy and configured Redis is reachable. Both bodies are deliberately detail-free.
OpenTelemetry (ADR-0023, backend-neutral; ADR-0028, Azure Monitor) instruments ASP.NET Core,
`HttpClient`, Npgsql, the runtime and custom auth metrics.

## Pipeline, errors, and the traps that look like over-engineering

**The pipeline order is load-bearing and lives only in
`Extensions/ApplicationBuilderExtensions.Pipeline.cs`.** `Documentation/Architecture/Pipeline.md`
justifies every position: forwarded headers → correlation id → request logging → exception
handler → 64 KiB body cap → status-code pages → docs/health → HSTS/HTTPS → security headers
→ static files → rate limiting → CORS → authentication → authorization → controllers →
playground fallback. Three things that are not simplifiable:

1. `CorrelationIdMiddleware` and `SecurityHeadersMiddleware` write headers from `Response.OnStarting` callbacks, because `UseExceptionHandler` calls `Response.Clear()` before writing a problem body — headers set on the way in would be present on every 2xx and missing on every 5xx (`PipelineTests` fails if this is "cleaned up").
2. CORS uses a custom `ICorsPolicyProvider`: `AllowCredentials` is a property of a *built* policy, so "credentials only for cookie-mode origins" cannot be expressed in one.
3. Rate limiting goes **before** authentication to stop password-hash CPU exhaustion; CORS also stays before authentication because a preflight carries no credentials and would 401 behind deny-by-default.

**Every non-2xx response is RFC 9457 with a stable `errorCode`.** `Documentation/Errors.md`
is the catalogue and guard tests fail the build if a code is missing from it. Exception→status
mapping lives in exactly one place, `Exceptions/ExceptionToProblemDetailsMap.cs` — services
never construct responses, controllers never map errors. `title`/`detail` are prose that may
be reworded; `errorCode` is the contract. Outside Development the framework's `exception`
extension is stripped and 5xx `detail` is blanked, and `AccountLockedException` maps
**identically** to `InvalidCredentialsException` so lockout stays invisible.

Only `RemoteIpAddress` is ever read for a rate-limit key — never raw `X-Forwarded-For`;
`UseTrustedForwardedHeaders` runs first and must be configured with known proxies.

## Authentication and authorization invariants

`Documentation/Architecture/Authentication.md` is the token-lifecycle source of truth
(claims, cookie matrix, rotation, reuse detection, step-up, revocation); `Authorization.md`
covers the permission model. Read both before touching anything auth-related.

- **Deny-by-default is ACTIVE.** Any endpoint with no authorization metadata requires an authenticated user. Two consequences that look like bugs: an unknown path answers **401, not 404**, and any non-controller endpoint that must stay public needs an explicit `.AllowAnonymous()` (OpenAPI, Scalar and the playground do).
- **Authentication is a composite policy scheme**: `Composite` forwards to `ApiKey` when the `Authorization` header contains `ak_`, otherwise to `JwtBearer`. JwtBearer is configured by `ConfigureJwtBearerOptions` (an `IConfigureNamedOptions` class, *not* an inline lambda — a lambda would need `BuildServiceProvider()` during registration, creating a second container with a second Data Protection key ring). Two lines there must not be "simplified": `ValidAlgorithms = [ES256]` (closes `alg:none` and HS256-with-the-public-key), and the `kid` resolver returning `[]` for an unresolvable key — never the whole ring, or retired keys keep validating.
- **CSRF is enforced globally by `Filters/CsrfProtectionFilter.cs`**, and the exemption is the dangerous half. A request is challenged only when it is state-changing, authenticated, **and** cookie-authenticated — the marker `AuthTransport.CookieAuthenticatedItemKey`, set by `ConfigureJwtBearerOptions` when it reads the access cookie. Do not re-derive that from the absence of an `Authorization` header. Both halves are checked: constant-time double submit, plus the token's tag verified against the request's `sid` claim.
- **Two password-hashing profiles, and they must never merge.** `IPasswordHasher.Hash` is the slow profile for user passwords; `HashSecret` is the deliberately cheap one for API keys and recovery codes (high-entropy machine-generated secrets with no dictionary to attack). Separate methods rather than a parameter, because a defaulted parameter is how the fast profile eventually reaches the password path.
- **Lockout arithmetic lives in `Services/Security/LockoutPolicy.cs`, not in the login service** — 5 failures / 15 minutes, fixed window, reset on success, fresh allowance after an expired lock — so boundaries can be asserted without a database or a password hash per case.
- **The Data Protection key ring is persisted to PostgreSQL** (ADR-0021). Two lines in `ServiceCollectionExtensions.Auth.cs` are load-bearing: `PersistKeysToDbContext<AppDbContext>()` and `SetApplicationName`, whose constant is mixed into every purpose chain — **change it and every existing protected payload, signing keys included, becomes unreadable**. That failure is silent through JWKS (which projects public keys and never unprotects) and appears only at signing time. The runtime role needs **write** on `DataProtectionKeys`: the ring rolls a successor at runtime, so a read-only grant fails ~90 days in.
- `Documentation/Security/Enumeration.md` is the anti-enumeration contract — every 🔓 endpoint's exists-vs-absent pair on status, body, timing and side effects. Anonymous registration answers **202 for both cases**; `email_already_registered` is internal-only there, like `account_locked`. `ASVS-Checklist.md` is the L2 traceability doc.

## Logging and audit

Serilog is wired two-stage (`Logging/SerilogSetup.cs`); correlation id and user id reach log
events through **enrichers**, not a `LogContext` push. The audit trail is a **table, not a
sink**: `IAuditLogger` writes each row in its own service scope so an event survives the
rollback of whatever transaction produced it, and `IAuditQueryService` is the only read path.
`Documentation/Architecture/AuditTrail.md` is the source of truth (event catalog, 90-day
retention, two schema traps); `AuditCatalogTests` fails the build when that catalog and
`AuditEventType` disagree in either direction.

**The never-logged list lives in exactly one place**, `Logging/SensitiveFieldNames.cs`, read
by both the Serilog destructuring policy and `AuditMetadataSerializer`. The audit table's
`Metadata` column is the trap that sharing exists for: durable, exempt from log rotation,
readable over HTTP. Redaction there is a backstop — never hand it a request body.

**`AuditActionFilter` is global and opts in by `[AuditEvent(AuditEventType.X)]`** on the
action. An action must never both carry the attribute and call `IAuditLogger` itself, or the
event lands twice. `admin_user_deleted` is deliberately **not** attributed: the filter runs
after the delete commits, and `AuditLogEntry.UserId` is a foreign key to a row that no longer
exists.

## Layer rules

- **`Program.cs` may only call extension methods.** Registrations go in `Extensions/ServiceCollectionExtensions.*.cs`, pipeline in `ApplicationBuilderExtensions.Pipeline.cs`. Business logic in `Program.cs` is a review rejection.
- **Controllers are thin by rule** (14 files): map the request, make one service call, map the result to a status. Anything that branches beyond status selection belongs in a service. Controllers never read tokens, cookies or headers — handlers turn those into claims and `ApiControllerBase` exposes `CurrentUserId`/`CurrentSessionId`. Every action needs a `CancellationToken`, a `[ProducesResponseType]` set, and an explicit `[Authorize]`/`[AllowAnonymous]`/`[RequirePermission]`; `ControllerArchitectureTests` fails the build otherwise.
- **Every request DTO has a validator** in `Validators/<Feature>/`, mirroring `DTOs/`. Validators are `internal sealed` and registered by assembly scan — which requires `includeInternalTypes: true`; without it the scan finds nothing and validation silently stops happening. Validators are **structural only**: format, ranges, presence. Anything needing the database (email uniqueness, token validity) belongs in a service, both because a validator must stay side-effect-free and because "this email is taken" is an enumeration oracle. The password policy lives in exactly one place, `PasswordRules`.
- **DTOs are `record`s with `required init` properties, one per file, under `DTOs/<Feature>/`** (47 files). Entities are never serialized and never referenced from a DTO — `DtoContractTests` fails the build on both, and on any property named like a stored secret. Show-once secrets appear in exactly one response each: the API key in `CreateApiKeyResponse`, the TOTP secret in `TotpEnrollmentResponse`, recovery codes in `RecoveryCodesResponse`. Do not add a second endpoint that returns any of them.
- **Entities carry no EF attributes.** All mapping — keys, indexes, `citext`, `jsonb`, cascade behaviour — lives in `Data/Configurations/`, one file per entity (14 of them); type-level mapping (`timestamptz`, enum-as-string) is in `AppDbContext.ConfigureConventions`. `Models/` stays persistence-agnostic; enums live in `Models/Enums/` and entities must never reference the service layer.
- **Options classes carry an `Auth` prefix** where the plain name collides — `AuthSessionOptions`, `AuthCookieOptions`. `SessionOptions` and `CookieOptions` both collide with framework types that implicit usings pull into scope.
- **One type per file.** No analyzer enforces this; it is a review rule.

## Data layer

`Documentation/Architecture/DataAccess.md` is the source of truth: mapping conventions,
per-index rationale, cascade map, and the two patterns that break silently — a collection
value-converter without a `ValueComparer` (in-place mutations produce no UPDATE), and a
transaction opened outside `CreateExecutionStrategy()` under `EnableRetryOnFailure` (throws
at runtime; refresh rotation is that shape).

- **All tables live in the `auth` schema**, not `public` — `AppDbContext.Schema`, applied via `HasDefaultSchema`, including `__EFMigrationsHistory`. Hand-written SQL must qualify: `auth."Users"`.
- **No connection string is committed.** Development reads `ConnectionStrings:Postgres` from **user-secrets**; every other environment reads the `ConnectionStrings__Postgres` environment variable. Startup throws a named error when neither is set — do not "fix" that by adding a default to `appsettings.json`.
- **Development migrates and seeds at startup** (`UseDatabaseSetupAsync`), and only there — the seeder checks the environment twice and refuses to run elsewhere. Production applies EF migration bundles as a deploy step (CI builds a self-contained linux-x64 `efbundle`); the API process must never auto-migrate outside Development.
- Scaffolded migrations under `Data/Migrations/` are exempt from code-style rules via `.editorconfig` (`generated_code = true`) — without it every generated migration fails the build. Runbook: `Documentation/Operations/Migrations.md`.

## Decision record and roadmap

- `Documentation/Decisions/README.md` — ADR index and numbering rules; `ADR-0001`–`ADR-0031`, one decision per file. New decisions get a new ADR; superseding one sets the old file's status to `Superseded by ADR-XXXX` rather than editing or deleting it.
- `Documentation/Scope.md` — v1 in-scope capabilities and the deferred list with reasons.
- **Approved decisions are final**: attribute-routed MVC **controllers only — no minimal API endpoints**; `Program.cs` strictly a composition root; RFC 9457 everywhere; PostgreSQL + EF Core; FluentValidation; manual mapping extensions (no AutoMapper); Serilog; Argon2id; ES256 JWTs (15-min TTL) + opaque rotating refresh tokens; xUnit + Testcontainers; Scalar; URL-segment `/api/v1/…` versioning; 7-day absolute session cap.
- **Same-PR rule**: each feature slice lands its controller, DTOs, validators, services, tests, and per-endpoint Markdown doc in one PR. `DocumentationSyncTests` compares each file's `method`/`route`/`auth` front matter to generated OpenAPI **in both directions** and enforces the sixteen headings, so a new endpoint without its doc fails the build.
- Never commit a token into `http/` — real credentials belong in `http-client.private.env.json`, which is gitignored. `scripts/check-secrets.sh` is the CI backstop.
