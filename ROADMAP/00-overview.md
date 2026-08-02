# Project Implementation Roadmap

**Project:** Better Auth–inspired authentication & authorization system — headless .NET 10 REST API
**Status:** Planning and all v1 technology decisions are complete. Implementation is code-
complete; first-environment rollout evidence and owner sign-offs remain where named.
**Deliverable policy:** No UI. Controllers only (no minimal API endpoints). `Program.cs` is strictly a composition root. Better Auth is architectural inspiration only — no source code is copied.

---

## How to read this document

- Each numbered top-level section is one **workstream**. Workstreams are grouped into phases and ordered by dependency, not by the original requirement list order (the reordering is explained in [Workstream order rationale](#workstream-order-rationale)).
- **Approved** decisions were explicitly confirmed by the project owner and are final.
- **`Pending Decision`** items carry a recommendation but must not be treated as final until approved.
- Every task names concrete files or directories. A task that cannot name its output file is not specific enough and was rewritten until it could.

---

## Approved decisions (final)

| Area | Decision |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10, existing `net10.0` target |
| API style | RESTful, attribute-routed MVC controllers only; RFC 9457 Problem Details for all errors |
| Token strategy | JWT access tokens, **15-minute TTL**, signed **ES256** + opaque rotating refresh tokens (SHA-256 hashed at rest, single-use, bound to a session row, reuse detection revokes the session) |
| Session lifetime | **Sliding 6-hour inactivity window + 7-day absolute cap** (cap value approved 2026-07-22, P1 — ADR-0002) |
| Session model | **Multi-device**: one session row per login with device metadata (IP, user agent, created, last-active); list / revoke-one / revoke-all endpoints; password change preserves current and revokes siblings (ADR-0026) |
| Token transport | **Both**: `httpOnly` `Secure` cookies (with CSRF defense) for browser clients and `Authorization: Bearer` for mobile/CLI/server clients |
| Signing keys | ES256 with `kid`-based rotation and a public **JWKS** endpoint; rotation designed in from day one |
| User store | **Custom entities** (Better Auth–style schema). No ASP.NET Core Identity. ASP.NET Core `JwtBearer` middleware, custom authentication handlers, and policy-based authorization on top |
| Password hashing | **Argon2id** with parameters versioned inside the stored hash (enables re-hash-on-login migration) |
| v1 feature scope | Email/password + sessions + roles, **plus**: email verification, password reset, TOTP MFA + recovery codes, social login (OAuth/OIDC), API keys / personal access tokens, **passkeys (WebAuthn/FIDO2)** |
| Database | **PostgreSQL** (Npgsql EF Core provider); EF Core owns schema, relationships, indexes, migrations; `citext` for email columns |
| Validation | **FluentValidation** — one validator class per request DTO |
| Mapping | **Manual mapping extension methods** (AutoMapper rejected: commercial license, runtime opacity) |
| Logging | **Serilog** — structured logging, correlation-ID and user-ID enrichers, request logging |
| Testing | **xUnit** + `WebApplicationFactory` + **Testcontainers** (real PostgreSQL) for integration tests; unit tests for validators, services, token logic |
| Containers / CI | **Docker + docker-compose** (API + PostgreSQL + Mailpit) and **GitHub Actions** |
| API documentation | **Scalar** UI over the built-in OpenAPI generator + one Markdown file per endpoint under `Documentation/` |

## Decision traceability

**P1–P18 are resolved.** The table remains as historical traceability; durable decisions
live in `Documentation/Decisions/`.

| # | Decision | Recommendation | Blocking workstream(s) |
|---|---|---|---|
| ~~P1~~ | Absolute session cap value | ✅ **Approved: 7 days** — `ADR-0002` | ~~4~~ resolved |
| ~~P2~~ | API versioning style | ✅ **Approved: URL segment `/api/v1/…`** via `Asp.Versioning.Mvc` — `ADR-0015` | ~~3, 11~~ resolved |
| ~~P3~~ | Additional directories beyond the mandated list | ✅ **Approved: all four** — `Validators/`, `Extensions/`, `Exceptions/`, `BackgroundServices/` — `ADR-0014` | ~~3~~ resolved |
| ~~P4~~ | Solution layout | ✅ Resolved — **revised to a flat root layout** (`ADR-0018`, superseding the `src/Api/` decision in `ADR-0014`). Root namespace `Api`. | ~~3~~ resolved |
| ~~P5~~ | Caching | ✅ **Approved: `HybridCache`** — local L1 remains; Azure L2 added by `ADR-0029`, partially superseding `ADR-0016` | ~~12, 17~~ resolved |
| ~~P6~~ | Rate-limiting store | ✅ **Approved: Azure Managed Redis atomic distributed counters**; local single-node mode remains in-memory — `ADR-0029` | ~~17~~ resolved |
| ~~P7~~ | Secret management (prod) | ✅ **Approved: Azure Key Vault references + managed identity** — `ADR-0027` | ~~25, 27~~ resolved |
| ~~P8~~ | Email provider (prod) | ✅ **Approved:** SMTP behind `IEmailSender`; Mailpit in dev — `ADR-0024` | ~~12~~ resolved |
| ~~P9~~ | Background jobs | ✅ **Approved:** plain `BackgroundService`; no Hangfire/Quartz in v1 — `ADR-0025` | ~~12~~ resolved |
| ~~P10~~ | Observability backend | ✅ **Approved: Azure Monitor**, with optional OTLP retained — `ADR-0028` | ~~28~~ resolved |
| ~~P11~~ | Message broker | ✅ **Approved: none in v1**; no cross-service asynchronous communication exists — `ADR-0030` | resolved |
| ~~P12~~ | Initial social providers | ✅ **Approved: Google + GitHub** — `ADR-0019` | ~~4, 12~~ resolved |
| ~~P13~~ | Social login flow style | ✅ **Approved: API-driven redirect**; SPA-PKCE deferred to §29 — `ADR-0019` | ~~4~~ resolved |
| ~~P14~~ | Deployment target | ✅ **Approved: Azure Container Apps** with private PostgreSQL/Redis and Bicep — `ADR-0027` | ~~27~~ resolved |
| ~~P15~~ | Load-testing tool | ✅ **Approved: k6** — scripts live outside the solution — `ADR-0017` | ~~23~~ resolved |
| ~~P16~~ | Scalar exposure in production | ✅ **Approved: Development + Staging only; disabled in Production** — `ADR-0031` | ~~18~~ resolved |
| ~~P17~~ | Signing-key private-key storage at rest | ✅ **Approved: Data Protection persisted to PostgreSQL and production-wrapped by Azure Key Vault** — `ADR-0020`, `ADR-0021`, `ADR-0027` | ~~4, 27~~ resolved |
| P18 | Audit log retention period | 90 days, then archive/delete via cleanup job | 15 |

**Explicitly out of v1 scope** (owner did not select; documented as future work in §29): organizations / multi-tenancy, machine-to-machine client-credentials flow. The full in-scope/out-of-scope statement now lives in `Documentation/Scope.md` (§1).

---

## Target directory structure

**Directories approved 2026-07-22** (P3 — `ADR-0014`). **Layout revised 2026-07-22** (`ADR-0018` supersedes the P4 `src/` decision): the API project sits at the **repository root**, not under `src/Api/`. Root namespace is `Api`.

```text
dotnet-web-api-startpack/                 # the API project lives here (ADR-0018)
├── Attributes/                           # [RequirePermission], [Idempotent], marker attributes
├── BackgroundServices/                   # APPROVED (P3): expired-session/token cleanup workers
├── Configuration/                        # typed options classes (JwtOptions, SessionOptions, …)
├── Controllers/                          # one controller per resource/responsibility
├── Data/
│   ├── Configurations/                   # one IEntityTypeConfiguration<T> per entity
│   ├── Migrations/                       # EF Core migrations
│   └── Seeding/                          # role seed data, dev-only user seeder
├── DTOs/                                 # per-feature request/response records
├── Exceptions/                           # APPROVED (P3): domain exception types
├── Extensions/                           # APPROVED (P3): composition-root extension methods
├── Filters/                              # validation filter, audit action filter
├── Handlers/                             # authentication + authorization handlers
├── Helpers/                              # small static utilities (Base64Url, device parsing)
├── Logging/                              # Serilog setup + enrichers
├── Mappings/                             # manual entity↔DTO mapping extensions, per feature
├── Middleware/                           # correlation ID, security headers, exception handling
├── Models/                               # one file per entity
├── Properties/                           # launchSettings.json
├── Services/                             # per-feature service interfaces + implementations
├── Validators/                           # APPROVED (P3): one FluentValidation validator per request DTO
├── wwwroot/                              # static assets (kept minimal; .gitkeep)
├── Program.cs                            # composition root only
├── tests/                                # excluded from the API project's globs — see below
│   ├── UnitTests/
│   └── IntegrationTests/
├── Documentation/                        # ADRs, Scope.md, per-endpoint Markdown (§19)
├── http/                                 # per-controller .http files (§24)
├── docker-compose.yml
├── Dockerfile                            # at root (multi-stage)
├── .github/workflows/ci.yml
├── ROADMAP/
├── Directory.Packages.props              # all NuGet versions (ADR-0013)
├── Directory.Build.props                 # solution-wide build settings
├── dotnet-web-api-startpack.csproj
└── dotnet-web-api-startpack.slnx         # .slnx, the .NET 10 SDK default (see §3)
```

⚠️ Because the project file is at the root, the SDK globs `**/*.cs` across the whole repository. `tests/`, `Documentation/`, `ROADMAP/` and `http/` are listed in `<DefaultItemExcludes>` in the csproj. **Any new top-level directory holding C# that is not application source must be added there**, or it silently compiles into the API assembly.

**Justification for the four approved additions (P3 — recorded in `ADR-0014`):**

- `Validators/` — FluentValidation is approved; one validator class per request DTO is mandated by the modularity rules. Placing validators inside `DTOs/` would mix two responsibilities in one tree; a sibling directory mirroring the `DTOs/<Feature>/` layout keeps both discoverable.
- `Extensions/` — `Program.cs` may only call modular extension methods. Those methods (`AddApiServices`, `AddAuthenticationSetup`, `UseApiPipeline`, …) need a home; `Configuration/` is reserved for options classes.
- `Exceptions/` — services signal failures with typed domain exceptions (`EmailAlreadyRegisteredException`, `TokenReuseDetectedException`, …) translated centrally to Problem Details. One class per file requires a directory.
- `BackgroundServices/` — expired-session/token cleanup (P9) is neither a service called by controllers nor middleware; hosted workers deserve their own directory.

---

## Entity model (13 entities)

```mermaid
erDiagram
    User ||--o{ Session : "has"
    User ||--o{ Account : "links"
    User |o--o{ VerificationToken : "owns"
    User ||--o| TotpCredential : "enrolls"
    User ||--o{ RecoveryCode : "holds"
    User ||--o{ PasskeyCredential : "registers"
    User ||--o{ ApiKey : "creates"
    User ||--o{ UserRole : "assigned"
    Role ||--o{ UserRole : "grants"
    Session ||--o{ RefreshToken : "issues"
    User |o--o{ AuditLogEntry : "generates"
    SigningKey
```

Two relationships are **optional on the `User` side**, and the diagram says so: a
`VerificationToken` of type `PasskeyAuthenticationChallenge` is issued before any user is
identified, and an `AuditLogEntry` survives the deletion of the account it describes
(`SetNull`, §7).

| Entity | Purpose | Key fields (beyond id/timestamps) |
|---|---|---|
| `User` | Account principal | `Email` (citext, unique), `EmailVerified`, `PasswordHash` (nullable — social/passkey-only users), `DisplayName`, `LockoutEndsAt`, `FailedLoginCount`, `SecurityStamp` |
| `Session` | One login on one device | `UserId`, `IpAddress`, `UserAgent`, `DeviceLabel`, `AuthenticatedAt`, `AuthenticationMethods`, `SecurityStamp` (snapshot), `LastActiveAt`, `AbsoluteExpiresAt`, `RevokedAt`, `RevocationReason` |
| `RefreshToken` | Rotating opaque token | `SessionId`, `TokenHash` (SHA-256, unique), `ExpiresAt`, `UsedAt`, `ReplacedByTokenId` |
| `Account` | External identity link | `UserId`, `Provider`, `ProviderAccountId` (unique per provider) |
| `VerificationToken` | Every short-lived single-use credential: email verify, password reset, MFA ticket, WebAuthn challenge | `UserId` (nullable — passkey authentication challenges only), `Type` (enum), `TokenHash`, `ExpiresAt`, `ConsumedAt` |
| `TotpCredential` | MFA authenticator secret | `UserId` (unique), `SecretEncrypted`, `ConfirmedAt` |
| `RecoveryCode` | MFA fallback | `UserId`, `CodeHash`, `UsedAt` |
| `PasskeyCredential` | WebAuthn credential | `UserId`, `CredentialId` (unique), `PublicKey`, `SignCount`, `Aaguid`, `Transports`, `Label`, `LastUsedAt` |
| `ApiKey` | Programmatic access | `UserId`, `Name`, `KeyPrefix` (lookup), `KeyHash`, `Scopes`, `ExpiresAt`, `LastUsedAt`, `RevokedAt` |
| `Role` | Authorization role | `Name` (unique), `Description` |
| `UserRole` | Join table | `UserId` + `RoleId` composite key |
| `AuditLogEntry` | Security audit trail | `UserId` (nullable), `EventType`, `IpAddress`, `UserAgent`, `CorrelationId`, `Metadata` (jsonb), `OccurredAt` |
| `SigningKey` | ES256 key ring | `KeyId` (kid), `PrivateKeyProtected`, `PublicKey`, `Status` (Active/Retiring/Retired), `ActivatedAt`, `RetiringAt`, `RetiredAt` |

Permissions are **code constants** mapped to roles in a static policy map (not DB rows) in v1 — keeps the schema lean; DB-driven permissions are listed as future work (§29).

### Fields §6 added beyond the original table

The three `Session` additions are direct consequences of the §4 token design, not new scope
— without them the documented refresh path cannot be implemented:

| Field | Why it must be a column |
|---|---|
| `Session.AuthenticatedAt` | The `auth_time` claim survives refresh unchanged (Authentication.md §14). After the first rotation the original login time exists nowhere else, so step-up would have nothing to measure against. |
| `Session.AuthenticationMethods` | `amr` is reissued on every rotation. Deriving it from the presented token is not possible — the client presents an opaque refresh token, which carries no claims. |
| `Session.SecurityStamp` | `RefreshOutcome.SecurityStampChanged` compares the user's current stamp against *something*; that something is the value captured at login. |

`SigningKey.RetiringAt` anchors the retirement grace period (`RetireElapsedKeysAsync` needs to
know when demotion happened). `User.DisplayName` gives `PATCH /users/me` a field to write.
`ApiKey.Name` makes the key list readable enough to revoke from confidently.

---

## Endpoint inventory

Drives the controller split (§11) and the `Documentation/` file list (§19). All routes below use URL-segment versioning (P2 approved — `ADR-0015`). Auth column: 🔓 anonymous, 🔐 authenticated, 👑 admin role.

| Method | Route | Auth | Controller |
|---|---|---|---|
| POST | `/api/v1/auth/register` | 🔓 | `AuthController` |
| POST | `/api/v1/auth/login` | 🔓 | `AuthController` |
| POST | `/api/v1/auth/login/mfa` | 🔓 (MFA ticket) | `AuthController` |
| POST | `/api/v1/auth/refresh` | 🔓 (refresh token) | `AuthController` |
| POST | `/api/v1/auth/logout` | 🔐 | `AuthController` |
| GET | `/api/v1/auth/csrf` | 🔓 | `AuthController` |
| GET | `/api/v1/auth/social/{provider}/authorize` | 🔓 | `SocialAuthController` |
| GET | `/api/v1/auth/social/{provider}/callback` | 🔓 | `SocialAuthController` |
| GET | `/api/v1/sessions` | 🔐 | `SessionsController` |
| DELETE | `/api/v1/sessions/{sessionId}` | 🔐 | `SessionsController` |
| DELETE | `/api/v1/sessions` | 🔐 | `SessionsController` (revoke all except current) |
| POST | `/api/v1/email-verification/send` | 🔐 | `EmailVerificationController` |
| POST | `/api/v1/email-verification/confirm` | 🔓 | `EmailVerificationController` |
| POST | `/api/v1/password-reset/request` | 🔓 | `PasswordResetController` |
| POST | `/api/v1/password-reset/confirm` | 🔓 | `PasswordResetController` |
| POST | `/api/v1/mfa/totp/enroll` | 🔐 | `MfaController` |
| POST | `/api/v1/mfa/totp/confirm` | 🔐 | `MfaController` |
| DELETE | `/api/v1/mfa/totp` | 🔐 (recent auth) | `MfaController` |
| POST | `/api/v1/mfa/recovery-codes/regenerate` | 🔐 (recent auth) | `MfaController` |
| POST | `/api/v1/passkeys/registration/options` | 🔐 | `PasskeysController` |
| POST | `/api/v1/passkeys/registration/complete` | 🔐 | `PasskeysController` |
| POST | `/api/v1/passkeys/authentication/options` | 🔓 | `PasskeysController` |
| POST | `/api/v1/passkeys/authentication/complete` | 🔓 | `PasskeysController` |
| GET | `/api/v1/passkeys` | 🔐 | `PasskeysController` |
| DELETE | `/api/v1/passkeys/{credentialId}` | 🔐 | `PasskeysController` |
| POST | `/api/v1/api-keys` | 🔐 | `ApiKeysController` |
| GET | `/api/v1/api-keys` | 🔐 | `ApiKeysController` |
| DELETE | `/api/v1/api-keys/{keyId}` | 🔐 | `ApiKeysController` |
| GET | `/api/v1/users/me` | 🔐 | `UsersController` |
| PATCH | `/api/v1/users/me` | 🔐 | `UsersController` |
| DELETE | `/api/v1/users/me` | 🔐 (recent auth) | `UsersController` |
| PUT | `/api/v1/users/me/password` | 🔐 | `UsersController` (preserves current, revokes siblings) |
| GET | `/api/v1/users/me/accounts` | 🔐 | `UsersController` |
| DELETE | `/api/v1/users/me/accounts/{accountId}` | 🔐 | `UsersController` |
| GET | `/api/v1/admin/users` | 👑 | `AdminUsersController` (paged/filter/sort) |
| GET | `/api/v1/admin/users/{userId}` | 👑 | `AdminUsersController` |
| PATCH | `/api/v1/admin/users/{userId}` | 👑 | `AdminUsersController` |
| DELETE | `/api/v1/admin/users/{userId}` | 👑 | `AdminUsersController` |
| POST | `/api/v1/admin/users/{userId}/roles` | 👑 | `AdminUserRolesController` |
| DELETE | `/api/v1/admin/users/{userId}/roles/{roleId}` | 👑 | `AdminUserRolesController` |
| DELETE | `/api/v1/admin/users/{userId}/sessions` | 👑 | `AdminUserSessionsController` |
| GET | `/api/v1/admin/audit-logs` | 👑 | `AdminAuditLogsController` (paged/filter) |
| GET | `/.well-known/jwks.json` | 🔓 | `WellKnownController` (unversioned) |
| GET | `/health/live`, `/health/ready` | 🔓 | health-check middleware (unversioned) |

---

## Phase overview and workstream order rationale

| Phase | Workstreams | Theme |
|---|---|---|
| A — Foundation | 1, 2, 3 | Decisions, stack, skeleton |
| B — Architecture | 4, 5 | Token + authorization design |
| C — Data | 6, 7, 8 | Entities, EF Core, migrations |
| D — API plumbing | 9, 10, 11, 12, 13, 14 | DTOs, validation, controllers, services, standards, middleware |
| E — Cross-cutting | 15, 16, 17 | Logging/audit, hardening, rate limiting |
| F — Documentation | 18, 19 | Scalar/OpenAPI, endpoint Markdown |
| G — Testing | 20, 21, 22, 23 | Unit, integration, security, load |
| H — Operations | 24, 25, 26, 27, 28 | Docker, config, CI/CD, deployment, monitoring |
| I — Longevity | 29 | Maintenance, extensibility |

**Workstream order rationale**: the original requirement list places authentication architecture (its #3) before solution structure (its #5). This roadmap swaps them: the solution skeleton, directories, and composition-root pattern must exist before architecture components have anywhere to land, and the token design (§4) must precede entity modeling (§6) because the `Session`/`RefreshToken`/`SigningKey` schemas are direct outputs of it. All 29 required workstreams are present; none are omitted. Feature implementation (register → login → refresh → sessions → verification/reset → MFA → social → passkeys → API keys → admin) is not a separate workstream — each feature slice cuts through §9–§15 and §19–§22, and the recommended build order is listed in §11.

---
