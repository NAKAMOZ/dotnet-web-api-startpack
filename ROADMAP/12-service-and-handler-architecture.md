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

- [ ] **FIRST, before anything else:** once the authentication schemes below are registered, set `options.FallbackPolicy = AuthorizationPolicies.DenyByDefault;` in `ServiceCollectionExtensions.Authorization.cs`, and add `app.UseAuthentication()` immediately above `app.UseAuthorization()` in the pipeline. §5 built deny-by-default but could not switch it on — no scheme existed to challenge with. Until this lands, **every endpoint without an explicit `[Authorize]` is anonymous**. §5's Definition of Done stays open until it does.

- [ ] Implement `Services/Crypto/` first (hasher + token generator) — everything depends on them.
- [ ] Implement `Services/Tokens/` per §4 design: ES256 issuance with `kid`, key manager (generate/activate/retire, JWKS projection), refresh rotation + reuse detection, MFA tickets.
- [ ] Implement feature services in the §11 build order; each service file lands with its unit tests.
- [ ] `Handlers/Authentication/ApiKeyAuthenticationHandler.cs` (prefix lookup → hash verify → claims principal with key scopes).
- [ ] `Services/Email/` with templated messages (`Templates/` as embedded resources: verify-email, reset-password, password-changed-notice, new-login-notice).
- [ ] `BackgroundServices/ExpiredAuthArtifactCleanupService.cs` + options.
- [ ] `Extensions/ServiceCollectionExtensions.Services.cs`: registrations (scoped services, singleton key manager with cache).

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

## Questions for the Project Owner

1. Approve P8/P9 recommendations?
2. Should "new login" notification emails be sent on every new-device login (recommended) or opt-in?
