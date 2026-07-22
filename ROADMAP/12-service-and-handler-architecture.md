# 12. Service and Handler Architecture

## Objective

All business logic in single-responsibility services behind interfaces; authentication schemes implemented as dedicated handlers; the token pipeline from §4 realized.

## Scope

Feature services, token/crypto services, auth-scheme handlers, email abstraction, background cleanup.

## Architectural Decisions

- One interface + one implementation per file, grouped per feature: `Services/Auth/` (`IRegistrationService`, `ILoginService`, `ILogoutService`), `Services/Sessions/ISessionService`, `Services/Tokens/` (`IAccessTokenIssuer`, `IRefreshTokenService`, `ISigningKeyManager`, `IMfaTicketService`), `Services/Crypto/` (`IPasswordHasher` → `Argon2PasswordHasher`, `ITokenGenerator` → CSPRNG + SHA-256 helpers), `Services/Email/` (`IEmailSender`, `IEmailTemplateRenderer`, Mailpit/SMTP implementation), `Services/Mfa/ITotpService`, `Services/Passkeys/IPasskeyService` (wraps Fido2NetLib), `Services/SocialAuth/ISocialAuthService`, `Services/ApiKeys/IApiKeyService`, `Services/Users/IUserService` + `IAdminUserService`, `Services/Audit/IAuditLogger`.
- Services throw typed exceptions from `Exceptions/` (`EmailAlreadyRegisteredException`, `InvalidCredentialsException`, `TokenReuseDetectedException`, `AccountLockedException`, …) → translated centrally (§14); no `(bool ok, string error)` tuples.
- Handlers in `Handlers/Authentication/`: `ApiKeyAuthenticationHandler` (scheme `ApiKey`), the policy scheme selector from §5. JwtBearer configured (not hand-rolled) in `Extensions/ServiceCollectionExtensions.Auth.cs`, including cookie-token extraction (`OnMessageReceived` reads `__Host-auth.access` when no bearer header).
- Argon2id parameters in `Configuration/PasswordHashingOptions.cs` (recommend start: 64 MB memory, iterations tuned to ~100 ms on prod hardware in §23); hash string embeds algorithm+version+params; `IPasswordHasher.NeedsRehash` drives re-hash-on-login.
- Refresh rotation runs in a serializable-scoped transaction: mark used → create successor → slide session; concurrent replay of the same token loses on the unique `TokenHash` + `UsedAt` check → reuse path.
- `BackgroundServices/ExpiredAuthArtifactCleanupService.cs` (P9): hourly, deletes expired/used refresh tokens, expired sessions, consumed verification tokens past retention.

## Technology Decisions Requiring Approval

P8 (email provider), P9 (background jobs) — recommendations stand.

## Tasks

- [x] **FIRST, before anything else:** deny-by-default is **active**. `options.FallbackPolicy = AuthorizationPolicies.DenyByDefault` is set, `app.UseAuthentication()` sits above `app.UseAuthorization()`, and the §11 placeholder scheme is deleted. **This closes §5's last open Definition-of-Done item.**
- [x] `Services/Crypto/` — `Argon2PasswordHasher` (two named profiles), `TokenGenerator` (CSPRNG + SHA-256 + constant-time compare).
- [x] `Services/Tokens/` per §4 design: ES256 issuance with `kid`, key manager (generate / rotate / retire / JWKS projection / exact-match resolution), refresh rotation with reuse detection, MFA tickets, sessions.
- [x] `Handlers/Authentication/ApiKeyAuthenticationHandler.cs` (prefix lookup → hash verify → claims principal with key scopes).
- [x] `Exceptions/` — 8 domain exception types.
- [x] `Extensions/ServiceCollectionExtensions.Services.cs` + real `ServiceCollectionExtensions.Auth.cs` (policy scheme → JwtBearer / ApiKey).
- [ ] Feature services: `Services/Auth`, `Users`, `Mfa`, `Passkeys`, `SocialAuth`, `ApiKeys`, `Audit`.
- [ ] `Services/Email/` with templated messages (embedded resources).
- [ ] `BackgroundServices/ExpiredAuthArtifactCleanupService.cs` + options.
- [ ] Wire the remaining 42 controller actions to their services (only JWKS is live).
- [ ] `Documentation/Architecture/Services.md`.

## Progress — this pass built the token pipeline, not the features

The workstream is split deliberately: everything below is what every feature service depends on, so it lands first and lands complete.

| Landed | Still open |
|---|---|
| Deny-by-default active; §5 closed | Registration / login / logout services |
| Argon2id hasher, two profiles, re-hash detection | MFA, passkeys, social, API-key, user, admin services |
| CSPRNG token generator, constant-time compare | Email sender + templates (P8 still open) |
| ES256 key ring: generate, rotate, retire, JWKS | Cleanup background worker (P9) |
| Access-token issuance with the full §2 claim set | 42 of 43 controller actions still return 501 |
| Refresh rotation + reuse detection, transactional | `Services.md` catalog |
| Session lifetime service (both bounds) | |
| MFA ticket issue/consume, atomic | |
| JwtBearer with the ES256 pin + cookie extraction | |
| API-key scheme + composite policy scheme | |
| 8 domain exceptions | |

## Decisions taken here

1. **`ISigningKeyManager` gained two methods** — `GetActiveKeyIdAsync` and `ResolveValidationKeyAsync`. A JWS signs `header.payload` and the header carries the `kid`, so the signer's identity must be known *before* there is anything to sign; and the bearer validator needs exact-match `kid` resolution. Without the first, the issuer would have to sign a throwaway payload just to read the key id back off the result.

2. **The key manager is scoped, not the singleton-with-cache the roadmap suggests.** A singleton holding `AppDbContext` is a captive dependency: the context outlives the request, accumulates tracked entities, and is not thread-safe. Caching belongs on the data it returns — §17.

3. **JWT bearer options are configured by an `IConfigureNamedOptions<JwtBearerOptions>` class**, not an inline lambda. The lambda form needs a service provider to reach the key manager, and the usual shortcut — `BuildServiceProvider()` during registration — builds a *second* container with its own singletons. Two Data Protection key rings would then exist, and keys protected by one would fail to unprotect under the other.

4. **`GetPublishableKeysAsync` bootstraps the ring.** Found live: a fresh database answered `/.well-known/jwks.json` with `{"keys":[]}`, because only the signing path created keys. An empty key set tells a client this issuer signs nothing, which it cannot distinguish from a misconfiguration.

5. **The composition-root smoke test now asserts `401` for an unknown path**, not `404`. The fallback applies to requests matching no endpoint, so an anonymous caller learns nothing about which paths exist. `MapOpenApi()` needed an explicit `.AllowAnonymous()` for the same reason.

## Expected Deliverables

`Services/` tree (~30 files), `Handlers/Authentication/`, `Exceptions/` (~10 files), cleanup worker.

## Dependencies

§4 (design), §6–§8 (data). Blocks nothing forward — features slice through with §11.

## Security Considerations

- All secret comparisons constant-time (`CryptographicOperations.FixedTimeEquals`).
- Login failure paths (unknown email vs wrong password) converge on one code path with a dummy-hash verification so timing does not reveal account existence.
- Lockout counter increments transactionally; lockout responses identical to invalid-credential responses externally (§16).
- Email sends are fire-and-forget through an outbox-less queue-in-process in v1 — but *responses never depend on send success*, so reset/verify endpoints cannot leak existence via email-provider latency.

## Testing Requirements

§20 unit tests per service (rotation state machine, reuse race, hasher roundtrip + rehash, TOTP window, lockout math); §21 covers the composed flows.

## Documentation Requirements

`Documentation/Architecture/Services.md`: service catalog, exception→status map.

## Definition of Done

All inventory endpoints backed by real services; §4 sequence diagrams match implementation; unit suites green.

**Status: not met — the token pipeline half is done, the feature half is not.** What is verified today:

- `GET /.well-known/jwks.json` serves a real ES256 key generated on first request:
  `{"kty":"EC","use":"sig","alg":"ES256","kid":"3-b1sPZ9…","crv":"P-256","x":"…","y":"…"}` — public components only, no `d`.
- The key row exists in `auth."SigningKeys"` with a 368-character Data-Protection-wrapped private key and a 124-character public SPKI. Private material never leaves `SigningKeyManager`.
- Deny-by-default is live: `/api/v1/sessions` → 401, `/api/v1/admin/users` → 401, unknown path → 401, `/openapi/v1.json` → 200.
- 93 tests green, including hasher round-trip, re-hash-on-cost-increase, corrupt-hash rejection, profile separation, and token entropy/uniqueness/URL-safety.

Not yet demonstrable: a full login → refresh → revoke round trip, because the login service does not exist. Token *validation* against the real ES256 ring is therefore also unexercised end to end — §21's Testcontainers harness is where that becomes testable.

## Questions for the Project Owner

1. **P8 (email provider) and P9 (background jobs)** — approve the standing recommendations? Both block the remaining half: `IEmailSender` gates registration and password reset, and the cleanup worker is P9.
2. Should "new login" notification emails be sent on every new-device login (recommended) or opt-in?
3. Still open from §11: **does `POST /auth/register` return `409` on a duplicate email, or always `202`?** The registration service is the next thing to be written and this decides its shape.
