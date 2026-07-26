# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

A Better Auth–inspired authentication & authorization REST API on .NET 10. **Phases A–D landed**, with §15–§20 partially or fully implemented: decisions recorded, packages pinned, data/token/pipeline architecture built, rate limiting active, OpenAPI + Scalar configured, all endpoint Markdown present, and 203 tests green.

What exists in code: the full data layer (§6–§8, migration applied), 47 DTOs (§9), 20 validators (§10), 14 controllers covering all 43 inventory operations (§11), and the **token pipeline** (§12): Argon2id hashing, CSPRNG tokens, the ES256 signing-key ring with JWKS, access-token issuance, refresh rotation with reuse detection, session lifetime, MFA tickets, and the real authentication schemes.

**§12 is half done.** The feature services — registration, login, logout, users, MFA, passkeys, social, API keys, email — do not exist, so 41 of 43 controller actions still return `501` via `ApiControllerBase.NotImplementedYet()`. Only `/.well-known/jwks.json` and `GET /api/v1/admin/audit-logs` are live. Also missing: the cleanup background worker and `Documentation/Architecture/Services.md`. Do not write feature code ahead of the workstream that owns it.

**§15 landed its observability half.** Serilog is wired two-stage (`Logging/SerilogSetup.cs`), the correlation id and user id reach log events through **enrichers** rather than a `LogContext` push, and `SensitiveDataDestructuringPolicy` redacts credential-shaped properties out of anything destructured with `{@…}`. The audit trail is a **table, not a sink**: `IAuditLogger` writes each row in a service scope of its own so an event survives the rollback of whatever transaction produced it, and `IAuditQueryService` is the only read path. `Documentation/Architecture/AuditTrail.md` is the source of truth — event catalog, retention (**90 days**, P18), and the two schema traps. `AuditCatalogTests` fails the build when that catalog and `AuditEventType` disagree in either direction.

**The never-logged list lives in exactly one place**, `Logging/SensitiveFieldNames.cs`, read by both the Serilog policy and `AuditMetadataSerializer`. The audit table's `Metadata` column is the trap the sharing exists for: it is durable, exempt from log rotation, and readable over HTTP. Redaction there is a backstop — never hand it a request body.

**`AuditActionFilter` is global and opts in by `[AuditEvent(AuditEventType.X)]`** on the action. An action must never both carry the attribute and call `IAuditLogger` itself, or the event lands twice. `admin_user_deleted` is deliberately **not** attributed: the filter runs after the delete commits, and `AuditLogEntry.UserId` is a foreign key to a row that no longer exists.

**§16 landed everything that does not need a login service.** The Data Protection key ring is now persisted to PostgreSQL (`ADR-0021`) — before this it was per-machine, and per-process in any container without a writable home, which orphaned every stored signing key on restart. Two lines in `ServiceCollectionExtensions.Auth.cs` are load-bearing: `PersistKeysToDbContext<AppDbContext>()` and `SetApplicationName`, whose constant is mixed into every purpose chain — **change it and every existing protected payload, signing keys included, becomes unreadable**. That failure is silent through JWKS, which projects public keys and never unprotects; it appears only at signing time. `Documentation/Operations/Migrations.md` §8 has the one-off deploy step. `auth` now holds **fourteen** tables, and the runtime role needs **write** on `DataProtectionKeys` — Data Protection rolls a successor key at runtime, so a read-only grant fails ~90 days in.

**Lockout arithmetic lives in `Services/Security/LockoutPolicy.cs`, not in the login service** — a recorded deviation from §16, so §22 can assert boundaries without a database and a password hash per case. Policy is 5 failures / 15 minutes, fixed window, counter reset on success, and a fresh allowance after an expired lock. The transition is implemented and boundary-tested; wiring it into login remains blocked on §12's missing `LoginService`. It is only half a control: lockout bounds guessing against *one* account and does nothing about one password sprayed across ten thousand, which is §17's per-IP limiting.

**`Documentation/Security/Enumeration.md` is the anti-enumeration contract**, written ahead of §12's services rather than after them — every 🔓 endpoint's exists-vs-absent response pair, on status, body, timing and side effects. It resolves the open question in `Documentation/Errors.md` §4: anonymous registration answers **202 for both cases**, and `email_already_registered` becomes internal-only there, exactly like `account_locked`. `ASVS-Checklist.md` is the L2 traceability doc; its ⏸️ and ❌ rows are the point of it, and the one ❌ is that the key ring itself is unencrypted at rest pending P7/P14.

**§17 rate limiting is active before authentication.** `general` is the global sliding-window default; `auth-strict`, `registration`, and the IP half of `email-sending` are named endpoint policies. The target-account half of `email-sending` is deliberately `EmailTargetRateLimitFilter`, after authentication and validation: before those stages neither the reset email nor the verified resend subject can be trusted. Email keys are SHA-256 hashed in memory. All rejections are RFC 9457 `rate_limited` with `Retry-After`. Only `RemoteIpAddress` is read — never raw `X-Forwarded-For`; §27 must configure known proxies ahead of this stage. P6's formal store approval remains pending even though the recommended in-memory implementation is present.

**§18–§19 are mechanically joined.** `SecuritySchemeTransformer` publishes bearer, cookie and API-key schemes; `AuthRequirementOperationTransformer` derives per-operation security from endpoint metadata. `/openapi/v1.json` and `/scalar/v1` exist in Development and Staging and are unmapped in Production (P16 formal approval remains pending). All 43 endpoint files are under `Documentation/<Feature>/`; `DocumentationSyncTests` compares their `method`/`route`/`auth` front matter to generated OpenAPI in both directions and enforces the sixteen headings.

**The test baseline is 203: 187 unit, 16 integration.** Unit coverage includes lockout boundaries, JWT claims/ES256 signatures and the key-rotation race, crypto, authorization, mappings, all-validator accepted fixtures and architecture guards. EF-dependent refresh/session transitions stay out of unit tests; §21 must exercise them against PostgreSQL rather than EF's in-memory provider.

**Two password-hashing profiles, and they must never merge.** `IPasswordHasher.Hash` is the slow profile for user passwords; `HashSecret` is the deliberately cheap one for API keys and recovery codes, which are high-entropy machine-generated secrets with no dictionary to attack. Separate methods rather than a parameter, because a defaulted parameter is how the fast profile eventually reaches the password path.

**Entities carry no EF attributes.** All mapping — keys, indexes, `citext`, `jsonb`, cascade behaviour — lives in `Data/Configurations/`, one file per entity; type-level mapping (`timestamptz`, enum-as-string) is in `AppDbContext.ConfigureConventions`. `Models/` stays persistence-agnostic. Enums (including `AuthenticationMethod` and `SessionRevocationReason`, moved out of `Services/Tokens/` in §6) live in `Models/Enums/`; entities must never reference the service layer.

`Documentation/Architecture/DataAccess.md` is the data-layer source of truth: mapping conventions, per-index rationale, cascade map, and the two patterns that break silently — a collection value-converter without a `ValueComparer` (in-place mutations produce no UPDATE), and a transaction opened outside `CreateExecutionStrategy()` under `EnableRetryOnFailure` (throws at runtime). §12's refresh rotation is the second shape.

**Migrations tooling is a pinned local tool**: run `dotnet tool restore` once per clone, then `dotnet ef …`. Scaffolded migrations under `Data/Migrations/` are exempt from the code-style rules via `.editorconfig` (`generated_code = true`) — without it every generated migration fails the build. The runbook is `Documentation/Operations/Migrations.md`.

**All tables live in the `auth` schema**, not `public` — `AppDbContext.Schema`, applied via `HasDefaultSchema`, including `__EFMigrationsHistory`. SQL written by hand must qualify: `auth."Users"`.

**No connection string is committed.** Development reads `ConnectionStrings:Postgres` from **user-secrets**; every other environment reads the `ConnectionStrings__Postgres` environment variable. Startup throws a named error when neither is set — do not "fix" that by adding a default to `appsettings.json`. The local database is the `dotnet-postgres` container (PostgreSQL 18.4, database `appdb`).

**Development migrates and seeds at startup** (`UseDatabaseSetupAsync`), and only there. Production applies EF migration bundles as a deploy step; the API process must never auto-migrate outside Development.

**Every non-2xx response is RFC 9457 with a stable `errorCode`** (§13). `Documentation/Errors.md` is the catalogue and guard tests fail the build if a code is missing from it. Exception→status mapping lives in exactly one place, `Exceptions/ExceptionToProblemDetailsMap.cs` — services never construct responses and controllers never map errors. `title`/`detail` are prose that may be reworded; `errorCode` is the contract. Two rules that are load-bearing: outside Development the framework's `exception` extension is stripped and 5xx `detail` is blanked, and `AccountLockedException` maps **identically** to `InvalidCredentialsException` so lockout stays invisible.

**The pipeline order is load-bearing and lives only in `Extensions/ApplicationBuilderExtensions.Pipeline.cs`** (§14). `Documentation/Architecture/Pipeline.md` is the source of truth: correlation id → request logging → exception handler → HTTPS/security headers → rate limiting → CORS → authentication → authorization → endpoints, each position justified. Two things that look like over-engineering and are not. First, `CorrelationIdMiddleware` and `SecurityHeadersMiddleware` write their headers from `Response.OnStarting` callbacks, because `UseExceptionHandler` calls `Response.Clear()` before writing a problem body — headers set on the way in would be present on every 2xx and missing on every 5xx (`PipelineTests` fails if this is "simplified"). Second, CORS uses a custom `ICorsPolicyProvider`: `AllowCredentials` is a property of a built policy, so "credentials only for cookie-mode origins" cannot be expressed in a single one. Rate limiting goes **before** authentication to stop password-hash CPU exhaustion; CORS also stays before authentication because a preflight carries no credentials and would 401 behind deny-by-default.

**CSRF is enforced globally by `Filters/CsrfProtectionFilter.cs`**, and the exemption is the dangerous half. A request is challenged only when it is state-changing, authenticated, **and** authenticated by cookie — the marker `AuthTransport.CookieAuthenticatedItemKey`, set by `ConfigureJwtBearerOptions` at the moment it reads the access cookie. Do not re-derive that condition from the absence of an `Authorization` header; the handler's precedence rule is the only authority. Both halves are checked: constant-time double submit, plus the token's tag verified against the request's `sid` claim. The tag comes from an `ITimeLimitedDataProtector`, a recorded deviation from Authentication.md's raw-HMAC formula with the same binding property.

**Controllers are thin by rule** (§11, 14 files): an action maps the request, makes one service call, maps the result to a status. Anything that branches beyond status selection belongs in a service. Controllers never read tokens, cookies or headers — handlers turn those into claims, and `ApiControllerBase` exposes `CurrentUserId`/`CurrentSessionId`. Every action needs a `CancellationToken`, a `[ProducesResponseType]` set, and an explicit `[Authorize]`/`[AllowAnonymous]`/`[RequirePermission]`; `ControllerArchitectureTests` fails the build otherwise. 41 action bodies currently return `NotImplementedYet()` (501) — §12 replaces each with its service call.

**Every request DTO has a validator** in `Validators/<Feature>/`, mirroring `DTOs/` (§10, 20 validators). Validators are `internal sealed` and registered by assembly scan — which requires `includeInternalTypes: true`; without it the scan finds nothing and validation silently stops happening. Validators are **structural only**: format, ranges, presence. Anything needing the database (email uniqueness, token validity) belongs in a service, both because a validator must stay side-effect-free and because "this email is taken" is an enumeration oracle. The password policy lives in exactly one place, `PasswordRules` — register, reset and change all call it, so they cannot drift.

**DTOs are `record`s with `required init` properties, one per file, under `DTOs/<Feature>/`** (§9, 47 files). Entities are never serialized and never referenced from a DTO — `DtoContractTests` fails the build on both, and on any property named like a stored secret. Show-once secrets appear in exactly one response each: the API key in `CreateApiKeyResponse`, the TOTP secret in `TotpEnrollmentResponse`, recovery codes in `RecoveryCodesResponse`. Do not add a second endpoint that returns any of them.

`Documentation/Architecture/Authentication.md` is the token-lifecycle source of truth: claims, cookie matrix, rotation, reuse detection, step-up, revocation paths. `Authorization.md` covers the permission model. Read both before touching anything auth-related.

**Options classes carry an `Auth` prefix** — `AuthSessionOptions`, `AuthCookieOptions`. Plain `SessionOptions` and `CookieOptions` both collide with framework types that implicit usings pull into scope.

**Deny-by-default is ACTIVE** (activated in §12). Any endpoint with no authorization metadata requires an authenticated user, so a forgotten `[Authorize]` fails closed. Two consequences that look like bugs otherwise: an unknown path answers **401, not 404** (the fallback covers requests matching no endpoint — an anonymous caller learns nothing about which paths exist), and any non-controller endpoint that must stay public needs an explicit `.AllowAnonymous()`, as OpenAPI and Scalar do.

**Authentication is a composite policy scheme**: `Composite` forwards to `ApiKey` when the `Authorization` header contains `ak_`, otherwise to `JwtBearer`. JwtBearer is configured by `ConfigureJwtBearerOptions` (an `IConfigureNamedOptions` class, *not* an inline lambda — a lambda would need `BuildServiceProvider()` during registration, which creates a second container with a second Data Protection key ring). Two lines there are load-bearing and must not be "simplified": `ValidAlgorithms = [ES256]` (the pin that closes `alg:none` and HS256-with-the-public-key), and the `kid` resolver returning `[]` for an unresolvable key — never the whole ring, or retired keys keep validating.

`Documentation/Decisions/` is the source of truth for architectural decisions; the roadmap is the source of truth for workstream scope and sequencing. Where the two disagree, the ADR wins — `ROADMAP/` is a planning artifact that §29 archives at v1 close.

## Layout

**Flat — the API project is at the repository root** (ADR-0018 supersedes the `src/` layout ADR-0014 originally approved).

```text
dotnet-web-api-startpack.csproj   the API project — RootNamespace is `Api`
dotnet-web-api-startpack.slnx     solution (XML format — see note under Commands)
Directory.Packages.props          all NuGet versions (CPM)
Directory.Build.props             solution-wide build settings
Program.cs                        composition root, 16 lines, zero logic
Extensions/                       the Add*/Use* methods Program.cs calls
Controllers/ DTOs/ Validators/ Services/ Models/ Data/ …   application source
tests/UnitTests/                  validators, mappers, services in isolation
tests/IntegrationTests/           WebApplicationFactory; Testcontainers arrives in §21
Documentation/                    ADRs, architecture/security docs, 43 endpoint files
http/                             per-controller .http files, populated in §24
ROADMAP/                          the 29 workstreams
```

⚠️ **The project globs from the repository root**, so any new top-level directory containing C# that is not application source **must** be added to `<DefaultItemExcludes>` in the csproj. `tests/**` is already excluded — without it, test files compile into the API assembly and every type is defined twice.

## Commands

Run from the repo root.

- **Run the API**: `dotnet run` — `http://localhost:5035` (`https` profile on 7052).
- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Watch**: `dotnet watch run`
- **OpenAPI document**: `curl http://localhost:5035/openapi/v1.json` (Development/Staging only)
- **Scalar UI**: `http://localhost:5035/scalar/v1` (Development/Staging only)

The solution file is **`.slnx`**, not `.sln` — the .NET 10 SDK's default format. Requires SDK 10, VS 2022 17.14+, or Rider 2025.1+. The roadmap text says `.sln`; this is a recorded deviation.

## Build enforcement — read before writing code

These will fail your build, by design:

- **No `Version` on a `PackageReference`.** Central Package Management is on; versions live in `Directory.Packages.props` and a local `Version` is error `NU1008`. Adding a package requires an ADR.
- **`TreatWarningsAsErrors`** — every compiler warning is an error.
- **File-scoped namespaces** (`IDE0161`) and **explicit accessibility modifiers** (`IDE0040`) are errors, enforced at build via `EnforceCodeStyleInBuild` + `.editorconfig`.
- **Unused usings** (`IDE0005`) are errors in application code, disabled under `tests/` — the rule only reports when `GenerateDocumentationFile` is true, which test projects switch off.
- **NuGet audit** runs at `mode=all`, `level=low`. A newly published advisory against any dependency, direct or transitive, **fails the build**. Fix by pinning the patched version, not by suppressing.
- `CS1591` (missing XML comment) is suppressed — doc comments are written where they carry meaning, not on every DTO property.

`Microsoft.OpenApi` is pinned to the **2.x** line. Do not "upgrade" it to 3.x — `Microsoft.AspNetCore.OpenApi` 10.0.10 targets 2.x and its source generator breaks against 3.x. See `ADR-0013`.

## Decision record (`Documentation/`)

- `Documentation/Decisions/README.md` — ADR index and numbering rules.
- `ADR-0001`–`ADR-0021` — one decision per file. New decisions get a new ADR; superseding one sets the old file's status to `Superseded by ADR-XXXX` rather than editing or deleting it.
- `Documentation/Scope.md` — v1 in-scope capabilities and the deferred list with reasons.

## The roadmap (`ROADMAP/`)

- `ROADMAP/README.md` — status board: per-workstream status, phase ordering, what happens next.
- `ROADMAP/00-overview.md` — the anchor document: approved decisions, pending decisions, target directory structure, 13-entity model, full endpoint inventory.
- `ROADMAP/01–29-*.md` — one file per workstream, each ending with a **Definition of Done**.

Rules encoded in the roadmap that override any default instinct:

- **Approved decisions are final** (table in `00-overview.md`): attribute-routed MVC **controllers only — no minimal API endpoints**; `Program.cs` strictly a composition root; RFC 9457 Problem Details for all errors; PostgreSQL + EF Core; FluentValidation; manual mapping extensions (no AutoMapper); Serilog; Argon2id password hashing; ES256 JWTs (15-min TTL) + opaque rotating refresh tokens; xUnit + Testcontainers; Scalar for API docs.
- **Pending decisions P6–P14 and P16–P18** carry recommendations but require owner approval. Do not start a workstream whose dependencies or blocking pending decisions are unresolved (e.g., P14 blocks §27; P8 blocks email delivery in §12). **P1–P5, P12, P13, P15 and P17 are resolved** (2026-07-22): 7-day absolute session cap, URL-segment `/api/v1/…` versioning, all four proposed directories approved, flat root layout (revised by ADR-0018) with root namespace `Api`, in-memory `HybridCache`, k6 for load testing, Google + GitHub social login via API-driven redirect, signing keys protected by Data Protection.
- **Status discipline**: mark a workstream ✅ in `README.md` only when its Definition of Done is met; 🔄 for partial progress; fine-grained checkboxes live inside each workstream file.
- **Same-PR rule**: each feature slice lands its controller, DTOs, validators, services, tests, and per-endpoint Markdown doc (`Documentation/`) in one PR to prevent doc drift.

## Conventions

- **`Program.cs` may only call extension methods.** Registrations go in `Extensions/ServiceCollectionExtensions.*.cs`, pipeline in `ApplicationBuilderExtensions.Pipeline.cs`. Business logic in `Program.cs` is a review rejection.
- **One type per file.** No analyzer enforces this — the .NET SDK has none, contrary to the roadmap's claim about the `IDE0040` family. It is a review rule until §20 adds an architecture test.
- Stub extension methods carry `TODO §N:` comments naming the workstream that fills them. Fill the stub when you reach that workstream, not before.
- Never commit a token into `http/` — real credentials belong in `http-client.private.env.json`, which is gitignored.
