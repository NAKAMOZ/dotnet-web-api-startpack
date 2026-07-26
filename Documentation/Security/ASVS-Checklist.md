# OWASP ASVS 4.0.3 — Level 2 Traceability

Scope: the chapters of ASVS L2 that apply to a stateless authentication and authorization REST API. Chapters that describe surfaces this API does not have — file upload (V12), server-side rendering and output encoding for browsers (parts of V5), WebSockets, SOAP, GraphQL — are recorded as **N/A** with the reason, once, rather than repeated per requirement.

**Status vocabulary**

| | |
|---|---|
| ✅ **Met** | Implemented and pointed at a file. Where a test enforces it, the test is named. |
| 🔄 **Designed** | Decided and documented, implementation waits on a named workstream. **Not a pass.** |
| ⏸️ **Deferred** | Deliberately not done for v1. Every one has a reason and an owner-visible consequence. |
| ❌ **Gap** | Should be met, is not, and is not deferred on purpose. |

**Read the ⏸️ rows.** They are the point of this document; the ✅ rows are the easy half.

Last reviewed: 2026-07-26 (§28). Re-review at v1 close (§29) and whenever §12 lands a feature service.

---

## V1 — Architecture, Design and Threat Modelling

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 1.1.x | Secure SDLC, documented decisions | ✅ | `Documentation/Decisions/` — 26 ADRs, one per decision, superseded rather than deleted |
| 1.2.2 | Components communicate with least-privilege accounts | 🔄 | Single `auth` schema makes a scoped grant expressible (`AppDbContext.Schema`); the grant itself is §27's deploy step |
| 1.4.1 | Access control enforced at a trusted layer | ✅ | Deny-by-default fallback policy, §5/§12 — `Extensions/ServiceCollectionExtensions.Authorization.cs` |
| 1.4.4 | One access-control mechanism, not several | ✅ | `[RequirePermission]` only; scheme-agnostic behind the `Composite` policy scheme |
| 1.5.x | Input/output trust boundaries defined | ✅ | DTOs never expose entities — `DtoContractTests` |
| 1.14.6 | No unsupported/insecure client tech | ✅ | .NET 10, all packages pinned centrally |

## V2 — Authentication

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 2.1.1 | Passwords ≥ 12 characters | ✅ | `Validators/Common/PasswordRules.cs` — `MinimumLength = 12` |
| 2.1.2 | Passwords of 64+ characters permitted | ✅ | `MaximumLength = 256`; Argon2id has no truncation behaviour to work around |
| 2.1.3–2.1.6 | No truncation, all characters allowed, unicode permitted | ✅ | No character-class rules exist to violate |
| 2.1.7 | Breached-password check on set/change | ✅ | `Validators/Common/CommonPasswords.txt`, embedded in the assembly so it cannot go missing at deploy and fail open |
| 2.1.9 | No composition rules | ✅ | Deliberately absent from `PasswordRules` |
| 2.1.10 | No periodic rotation requirement | ✅ | Not implemented, by decision |
| 2.2.1 | Anti-automation on credential endpoints | 🔄 | Lockout is §16 (`Services/Security/LockoutPolicy.cs`, options bound); **per-IP limiting is §17 and is the half that stops password spraying** |
| 2.2.2 | No SMS/voice as a default MFA factor | ✅ | TOTP and WebAuthn only — `Documentation/Scope.md` |
| 2.2.3 | Notification on credential change | 🔄 | SMTP delivery and templates are live; adding notifications to every credential-change path remains separate scope |
| 2.3.1 | Activation/reset tokens random and short-lived | ✅ | CSPRNG via `ITokenGenerator`; lifetimes in `AuthSessionOptions` |
| 2.4.1 | Passwords stored with an approved one-way KDF | ✅ | Argon2id — ADR-0006, `Services/Crypto/Argon2PasswordHasher.cs` |
| 2.4.x | KDF parameters at or above recommended cost | ✅ | `PasswordHashingOptions`, validated at startup. **Two profiles**: `Hash` for passwords, `HashSecret` for high-entropy machine secrets — separate methods, never a parameter, so the cheap profile cannot default its way onto the password path |
| 2.5.1 | System-generated secrets not sent in cleartext where avoidable | ⏸️ | Reset and verification links travel by email, which is the medium. Bounded by short lifetimes, single use, and hashing at rest |
| 2.5.4 | No shared or default accounts | ✅ | Development-only named accounts receive Argon2 hashes; Production seeds no users |
| 2.5.6 | Reset uses a random, short-lived, single-use token | ✅ | `VerificationToken` — hashed at rest, `ConsumedAt` enforces single use |
| 2.5.7 | Reset does not reveal account existence | ✅ | `Documentation/Security/Enumeration.md` §3 |
| 2.7.x | Out-of-band verifier (email links) | 🔄 | Entity and token pipeline exist; delivery is §12 |
| 2.8.x | One-time verifier (TOTP), secrets protected at rest | 🔄 | `TotpCredential` mapped; encryption via Data Protection is §12's MFA service. **ADR-0021's key ring is what makes that survivable** |
| 2.9.x | Cryptographic verifier (WebAuthn) | 🔄 | `PasskeyCredential` mapped; §12 |
| 2.10.1 | Service/API secrets not stored in cleartext | ✅ | API keys hashed with `HashSecret`; shown once in `CreateApiKeyResponse` |

## V3 — Session Management

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 3.2.1 | New session token generated on authentication | ✅ | `SessionService` creates a session per login |
| 3.2.2 | ≥ 64 bits of entropy in session tokens | ✅ | `TokenGenerator` — CSPRNG opaque refresh tokens |
| 3.2.3 | Tokens stored securely (cookie flags / not in URL) | ✅ | `AuthCookieOptions` — `HttpOnly`, `Secure`, `SameSite`; never a query parameter |
| 3.3.1 | Logout invalidates the session | 🔄 | `ISessionService` revocation exists; the logout endpoint is §12 |
| 3.3.2 | Inactivity and absolute timeouts | ✅ | `AuthSessionOptions.InactivityWindow` (6 h) and `AbsoluteLifetime` (7 d, P1). Absolute is written once and never extended on refresh |
| 3.3.3 | Ability to terminate all other sessions | 🔄 | `DELETE /api/v1/sessions` routes and authorizes; service is §12 |
| 3.3.4 | Active sessions listable by the user | 🔄 | Same |
| 3.5.2 | Static API secrets avoided in favour of dynamic tokens | ⏸️ | API keys are a deliberate v1 capability for machine callers. Mitigated: hashed at rest, prefix-identified, revocable, separately audited |
| 3.5.3 | Stateless tokens carry a verified digital signature | ✅ | ES256, `ValidAlgorithms = [ES256]` — the pin that closes `alg:none` and HS256-with-the-public-key. `kid` resolver returns `[]` for an unresolvable key, never the whole ring |
| 3.7.1 | Re-authentication before sensitive operations | ✅ | `auth_time` step-up, `RecentAuthenticationWindow` (5 min) — Authentication.md §14 |

## V4 — Access Control

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 4.1.1 | Enforced on a trusted service layer | ✅ | Authorization policies in the pipeline; controllers never decide |
| 4.1.2 | User/data attributes not manipulable by the client | ✅ | Claims come from handlers; `ApiControllerBase.CurrentUserId` — controllers never read tokens, cookies or headers |
| 4.1.3 | Least privilege / no elevation | ✅ | `[RequirePermission]`, per-permission — `Documentation/Architecture/Authorization.md` |
| 4.1.5 | Access control fails securely | ✅ | Deny-by-default fallback. An unknown path answers **401, not 404** — an anonymous caller learns nothing about which paths exist. `ControllerArchitectureTests` fails the build on a missing authorization attribute |
| 4.2.1 | No IDOR — object references scoped to the caller | ✅ | Own-resource routes scope every lookup to `CurrentUserId`; a miss is `404`, never `403` |
| 4.2.2 | CSRF defences on state-changing operations | ✅ | `Filters/CsrfProtectionFilter.cs`, global. Challenged only when state-changing **and** cookie-authenticated, via `AuthTransport.CookieAuthenticatedItemKey` — never re-derived from a missing `Authorization` header. Constant-time double submit plus the token's tag verified against the request's `sid` claim |
| 4.3.1 | Administrative interfaces use MFA | ⏸️ | Admin endpoints require `[RequirePermission]`, not step-up MFA. Deferred to post-v1; the mechanism (`auth_time`) already exists, so this is one attribute per admin action when approved. **Owner decision outstanding** |

## V5 — Validation, Sanitization and Encoding

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 5.1.3–5.1.4 | All input validated, positively | ✅ | 20 FluentValidation validators, one per request DTO, registered by assembly scan with `includeInternalTypes: true` |
| 5.2.x | Sanitisation of untrusted content | N/A | The API returns JSON only and renders no user content. `nosniff` plus a restrictive CSP close the "JSON interpreted as HTML" path |
| 5.3.4 | Parameterised queries / no SQL injection | ✅ | EF Core LINQ throughout; the only hand-written SQL is in `Documentation/Operations/Migrations.md` runbooks |
| 5.3.8 | No OS command injection | N/A | No process execution anywhere in the API |
| 5.5.x | Deserialisation safety | ✅ | `System.Text.Json` into `record` DTOs with `required init`; no polymorphic deserialisation, no `BinaryFormatter` |

## V7 — Error Handling and Logging

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 7.1.1 | No credentials or payment details in logs | ✅ | `Logging/SensitiveDataDestructuringPolicy.cs` redacts credential-shaped properties from anything destructured with `{@…}` |
| 7.1.3 | Security-relevant events logged | 🔄 | `IAuditLogger` + `AuditEventType` catalogue; **20 of the events belong to services §12 has not written**. `AuditCatalogTests` fails the build when the catalogue and the enum disagree in either direction |
| 7.1.4 | Each log event has enough context | ✅ | Correlation id and user id reach every event through Serilog **enrichers**, not a `LogContext` push |
| 7.2.1 | Authentication decisions logged | 🔄 | `login_succeeded` / `login_failed` / `account_locked` catalogued; written by §12's login service |
| 7.2.2 | Access-control decisions logged | ⏸️ | Only admin operations are audited. Logging every authorization decision on a token-validated API is volume without signal; revisit if an incident needs it |
| 7.3.1 | Log injection prevented | ✅ | Structured logging only — message templates with properties, never interpolated strings |
| 7.3.3 | Logs protected from unauthorised access | 🔄 | Audit rows readable only through `GET /api/v1/admin/audit-logs` behind `[RequirePermission]`. **Log file/stream protection is §27's** |
| 7.4.1 | Generic error message to the client | ✅ | RFC 9457 everywhere; outside Development the framework's `exception` extension is stripped and 5xx `detail` is blanked — `ExceptionToProblemDetailsMap` |

> **The audit `Metadata` column is the sharpest surface in this chapter.** It is durable, exempt from log rotation, and readable over HTTP. `AuditMetadataSerializer` shares its never-logged list with the Serilog policy through `Logging/SensitiveFieldNames.cs` — one definition, two readers. That redaction is a backstop, not a licence: never hand it a request body.

## V8 — Data Protection

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 8.1.1 | Sensitive data protected from unauthorised access | ✅ | Signing-key private material encrypted at rest — ADR-0020 |
| 8.1.6 | Backups of sensitive data protected | ⏸️ | §27. **ADR-0021 makes this sharper, not softer**: the Data Protection key ring now lives in the same database as the keys it protects, so one backup contains both |
| 8.2.1 | No sensitive data in browser storage | ✅ | Cookie mode is `HttpOnly` — tokens are unreachable from JavaScript by construction |
| 8.3.1 | Sensitive data not sent in URL parameters | ✅ | Tokens in bodies, headers or cookies; never the query string |
| 8.3.4 | Sensitive data inventoried | ✅ | `Logging/SensitiveFieldNames.cs` is the single list |
| — | **Data Protection key ring encrypted at rest** | ❌ **Gap** | `ProtectKeysWith*` is not configured — every option is host-specific, so choosing one means choosing a deployment target (P14). **The largest known gap in §16.** ADR-0021 "Consequences"; owner: project owner; resolves with P7/P14 |

## V9 — Communications

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 9.1.1 | TLS for all client connectivity | ✅ | `UseHttpsRedirection`; HSTS outside Development — `ApplicationBuilderExtensions.Pipeline.cs` |
| 9.1.2–9.1.3 | Strong cipher suites, TLS 1.2+ | ⏸️ | A host/reverse-proxy property, not an application one. §27 runbook item |
| — | Forwarded scheme/client IP accepted only from trusted proxies | ✅ | Validated exact IP/CIDR allowlist, fail-fast production configuration and known/unknown proxy tests — `ReverseProxyOptionsValidator`, `ForwardedHeadersTests` |
| 9.2.x | Server-side TLS for outbound connections | 🔄 | Applies once social login and email delivery exist (§12) |

## V11 — Business Logic

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 11.1.1 | Logic flows processed in sequence | ✅ | MFA tickets are single-use and hashed at rest; refresh rotation is one transaction inside `CreateExecutionStrategy()` |
| 11.1.2 | Limits on business-logic flows | ✅ | Lockout (§16) + `auth-strict`, `email-sending`, `registration`, and global limits — `RateLimits.md` |
| 11.1.4 | Anti-automation on high-value flows | ✅ | IP and target-account partition tests — `RateLimitingTests` |
| 11.1.5 | Business-logic limits enforced server-side | ✅ | Validators are structural only; anything needing the database is a service decision, never a client one |

## V13 — API and Web Service

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 13.1.1 | No credentials in URLs | ✅ | See 8.3.1 |
| 13.1.3 | API URLs do not expose sensitive information | ✅ | Guid v7 identifiers; no sequential ids to walk |
| 13.1.4 | Authorization decisions at URI and resource level | ✅ | Route-level `[RequirePermission]` plus owner scoping in the service |
| 13.2.1 | Only permitted HTTP methods accepted | ✅ | Attribute routing only; unmatched methods answer `405` (`method_not_allowed`) |
| 13.2.2 | Schema validation before acceptance | ✅ | Validation filter runs before the action body |
| 13.2.3 | CSRF protection for RESTful services | ✅ | See 4.2.2 |
| 13.2.5 | Content-Type checked | ✅ | `415 unsupported_media_type` for anything but `application/json` |
| 13.2.6 | Message payload signed for reliable transport | N/A | No message bus in v1 |

## V14 — Configuration

| Req | Requirement | Status | Evidence |
|---|---|---|---|
| 14.1.1 | Build and deploy are repeatable and automated | 🔄 | §26 |
| 14.2.1 | No known-vulnerable components | ✅ | NuGet audit at `mode=all`, `level=low` — **a newly published advisory against any dependency, direct or transitive, fails the build.** Fixed by pinning the patched version, never by suppressing |
| 14.2.2 | Unneeded features and frameworks removed | ✅ | Every package requires an ADR (ADR-0013); nothing arrives casually |
| 14.2.3 | Third-party assets from a trusted source | 🔄 | `.github/dependabot.yml` opens the update PRs; §26 gates the merge on `dotnet list package --vulnerable --include-transitive` |
| 14.3.2 | Debug modes disabled in production | ✅ | Developer exception page and OpenAPI are Development-only; `exception` extension stripped outside it |
| 14.3.3 | No version-disclosing headers | ✅ | `Server` and `X-Powered-By` are not emitted |
| 14.4.1 | Content-Type on every response | ✅ | `application/json` / `application/problem+json` |
| 14.4.3 | Content Security Policy | ✅ | `SecurityHeadersMiddleware` — a restrictive API policy, a separate one for the documentation route |
| 14.4.4 | `X-Content-Type-Options: nosniff` | ✅ | Same |
| 14.4.5 | HSTS | ✅ | `UseHsts()`, non-Development only — correct, since HSTS on `localhost` poisons the developer's browser for every other local project |
| 14.4.7 | Framing controls | ✅ | `X-Frame-Options: DENY` plus CSP `frame-ancestors`; redundancy for older clients, not duplicated policy |
| 14.5.1 | Unusual HTTP methods rejected | ✅ | See 13.2.1 |
| 14.5.3 | CORS `Access-Control-Allow-Origin` not a wildcard with credentials | ✅ | Custom `ICorsPolicyProvider` — `AllowCredentials` is a property of a *built* policy, so "credentials only for cookie-mode origins" cannot be expressed in one. CORS sits **before** authentication: a preflight carries no credentials and would `401` behind deny-by-default |

> **Headers are written from `Response.OnStarting` callbacks, not on the way in.** `UseExceptionHandler` calls `Response.Clear()` before writing a problem body, so inbound-written headers would be present on every `2xx` and missing on every `5xx` — absent from exactly the responses an attacker studies. `PipelineTests` fails if this is "simplified".

---

## Summary of open items

| # | Item | Chapter | Owner | Resolves with |
|---|---|---|---|---|
| 1 | ❌ Data Protection key ring unencrypted at rest | V8 | Project owner | P7 / P14 → supersedes ADR-0020 + ADR-0021 |
| 2 | ⏸️ Admin operations do not require step-up MFA | V4.3.1 | Project owner | Post-v1 decision; mechanism already exists |
| 3 | ⏸️ Backup trust boundary for keys vs key ring | V8.1.6 | Project owner | §27 |
| 4 | ⏸️ TLS cipher/version policy | V9.1.2 | Project owner | §27 |
| 5 | ⏸️ API keys as static secrets | V3.5.2 | Accepted for v1 | — |
| 6 | ⏸️ Authorization decisions not audited | V7.2.2 | Accepted for v1 | — |
| 7 | 🔄 Credential-change notifications on every path | V2.2.3 | Project owner | Post-v1 scope |

Per-account lockout and per-IP limiting look redundant and are not: lockout bounds
guessing against *one* account, and does nothing about one password tried against ten
thousand accounts, because no single account ever reaches the threshold. Both are active.
