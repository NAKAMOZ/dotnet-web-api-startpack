# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

A Better Auth–inspired authentication & authorization REST API on .NET 10. **Phase A complete** (§1–§3), **§4–§5 written**, **§6–§7 landed**: decisions recorded, packages pinned, skeleton built, token architecture designed, entity model implemented and mapped to PostgreSQL.

What exists in code: the composition root, extension stubs, three options classes in `Configuration/`, five token-service **interfaces** in `Services/Tokens/`, the authorization handlers in `Handlers/Authorization/`, the 13-entity model in `Models/` (§6), and `AppDbContext` + 13 `IEntityTypeConfiguration<T>` in `Data/` (§7). There is still **no migration, no service implementation, and no controller** — those are §8, §12 and §11. Do not write feature code ahead of the workstream that owns it.

**Entities carry no EF attributes.** All mapping — keys, indexes, `citext`, `jsonb`, cascade behaviour — lives in `Data/Configurations/`, one file per entity; type-level mapping (`timestamptz`, enum-as-string) is in `AppDbContext.ConfigureConventions`. `Models/` stays persistence-agnostic. Enums (including `AuthenticationMethod` and `SessionRevocationReason`, moved out of `Services/Tokens/` in §6) live in `Models/Enums/`; entities must never reference the service layer.

`Documentation/Architecture/DataAccess.md` is the data-layer source of truth: mapping conventions, per-index rationale, cascade map, and the two patterns that break silently — a collection value-converter without a `ValueComparer` (in-place mutations produce no UPDATE), and a transaction opened outside `CreateExecutionStrategy()` under `EnableRetryOnFailure` (throws at runtime). §12's refresh rotation is the second shape.

**Migrations tooling is a pinned local tool**: run `dotnet tool restore` once per clone, then `dotnet ef …`. Scaffolded migrations under `Data/Migrations/` are exempt from the code-style rules via `.editorconfig` (`generated_code = true`) — without it every generated migration fails the build. The runbook is `Documentation/Operations/Migrations.md`.

**All tables live in the `auth` schema**, not `public` — `AppDbContext.Schema`, applied via `HasDefaultSchema`, including `__EFMigrationsHistory`. SQL written by hand must qualify: `auth."Users"`.

**No connection string is committed.** Development reads `ConnectionStrings:Postgres` from **user-secrets**; every other environment reads the `ConnectionStrings__Postgres` environment variable. Startup throws a named error when neither is set — do not "fix" that by adding a default to `appsettings.json`. The local database is the `dotnet-postgres` container (PostgreSQL 18.4, database `appdb`).

**Development migrates and seeds at startup** (`UseDatabaseSetupAsync`), and only there. Production applies EF migration bundles as a deploy step; the API process must never auto-migrate outside Development.

**Controllers are thin by rule** (§11, 14 files): an action maps the request, makes one service call, maps the result to a status. Anything that branches beyond status selection belongs in a service. Controllers never read tokens, cookies or headers — handlers turn those into claims, and `ApiControllerBase` exposes `CurrentUserId`/`CurrentSessionId`. Every action needs a `CancellationToken`, a `[ProducesResponseType]` set, and an explicit `[Authorize]`/`[AllowAnonymous]`/`[RequirePermission]`; `ControllerArchitectureTests` fails the build otherwise. Action bodies currently return `NotImplementedYet()` (501) — §12 replaces each with its service call.

**Every request DTO has a validator** in `Validators/<Feature>/`, mirroring `DTOs/` (§10, 20 validators). Validators are `internal sealed` and registered by assembly scan — which requires `includeInternalTypes: true`; without it the scan finds nothing and validation silently stops happening. Validators are **structural only**: format, ranges, presence. Anything needing the database (email uniqueness, token validity) belongs in a service, both because a validator must stay side-effect-free and because "this email is taken" is an enumeration oracle. The password policy lives in exactly one place, `PasswordRules` — register, reset and change all call it, so they cannot drift.

**DTOs are `record`s with `required init` properties, one per file, under `DTOs/<Feature>/`** (§9, 47 files). Entities are never serialized and never referenced from a DTO — `DtoContractTests` fails the build on both, and on any property named like a stored secret. Show-once secrets appear in exactly one response each: the API key in `CreateApiKeyResponse`, the TOTP secret in `TotpEnrollmentResponse`, recovery codes in `RecoveryCodesResponse`. Do not add a second endpoint that returns any of them.

`Documentation/Architecture/Authentication.md` is the token-lifecycle source of truth: claims, cookie matrix, rotation, reuse detection, step-up, revocation paths. `Authorization.md` covers the permission model. Read both before touching anything auth-related.

**Options classes carry an `Auth` prefix** — `AuthSessionOptions`, `AuthCookieOptions`. Plain `SessionOptions` and `CookieOptions` both collide with framework types that implicit usings pull into scope.

**Deny-by-default is not active yet.** `AuthorizationPolicies.DenyByDefault` is written and tested but unassigned — until then every endpoint without an explicit `[Authorize]` is anonymous. The activation line is marked in `ServiceCollectionExtensions.Authorization.cs`, and §12 sets it once the real schemes exist. It cannot be set now because the fallback also applies to requests matching no endpoint, which would turn every 404 into a 401.

⚠️ **`PlaceholderAuthenticationHandler` is temporary and must die in §12.** It is registered as the default scheme and authenticates nobody, so `[Authorize]` endpoints can be *challenged* (401) instead of throwing (500) — ASP.NET Core does not return 401 without a challenge scheme. It is fail-closed: while it is the only scheme, nobody can log in. Replace it with JwtBearer + cookie + API-key, do not build on it.

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
Controllers/ DTOs/ Validators/ Services/ Models/ Data/ …   empty, .gitkeep only
tests/UnitTests/                  validators, mappers, services in isolation
tests/IntegrationTests/           WebApplicationFactory; Testcontainers arrives in §21
Documentation/                    ADRs, Scope.md; per-endpoint docs land per feature (§19)
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
- **OpenAPI document**: `curl http://localhost:5035/openapi/v1.json` — currently `"paths": {}`, correctly, since no controllers exist. Scalar UI arrives in §18.

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
- `ADR-0001`–`ADR-0020` — one decision per file. New decisions get a new ADR; superseding one sets the old file's status to `Superseded by ADR-XXXX` rather than editing or deleting it.
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
