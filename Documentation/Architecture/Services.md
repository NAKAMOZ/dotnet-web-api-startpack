# Service Architecture

**Status:** Implemented · **Workstream:** §12

Controllers map HTTP inputs and outputs only. Business rules, persistence, token handling,
provider calls and audit decisions live behind the interfaces below. Services are scoped
when they use `AppDbContext`; stateless crypto, templates, metrics and queues are singleton.

## Service catalog

| Area | Interface(s) | Responsibility |
|---|---|---|
| Registration and email | `IRegistrationService`, `IEmailVerificationService`, `IEmailSender`, `IEmailTemplateRenderer` | Enumeration-safe registration, single-use verification tokens, embedded templates and queued SMTP delivery |
| Authentication | `ILoginService`, `IAuthenticationSessionFactory`, `IRefreshService`, `ILogoutService`, `IAuthTokenTransport` | Password/lockout/MFA decisions, session creation, token rotation and exclusive cookie/body transport |
| Passwords and sessions | `IPasswordResetService`, `ISessionService`, `ISessionQueryService` | Reset tokens, security-stamp rotation, bounded sessions and ownership-scoped revocation |
| MFA | `ITotpService`, `IRecoveryCodeService`, `IMfaTicketService` | Protected TOTP secrets, windowed verification, single-use recovery codes and MFA tickets |
| Credentials | `IPasskeyService`, `IApiKeyService` | WebAuthn ceremonies and prefix-indexed, hash-at-rest API keys |
| Social authentication | `ISocialAuthService` | Single-use OAuth state, Google/GitHub exchange and provider-subject account matching |
| Users and administration | `IUserService`, `IAdminUserService`, `IAdminRoleService`, `IAdminSessionService` | Self-service profile/security actions and permission-gated administration |
| Tokens and keys | `IAccessTokenIssuer`, `IRefreshTokenService`, `ISigningKeyManager` | ES256 access tokens, rotating opaque refresh tokens and cached exact-`kid` key resolution |
| Audit and maintenance | `IAuditLogger`, `IAuditQueryService`, `ExpiredAuthArtifactCleanupService` | Independent durable events, indexed querying, 90-day retention and bounded cleanup |

## Exception to HTTP mapping

Services never create HTTP responses. `ExceptionToProblemDetailsMap` is the single mapping:

| Exception | Status | Public code |
|---|---:|---|
| `InvalidCredentialsException`, `AccountLockedException`, refresh reuse | 401 | `invalid_credentials` |
| `InvalidTokenException` | 400 | `invalid_token` |
| `UnsupportedProviderException` | 400 | `unsupported_provider` |
| `ForbiddenOperationException` | 403 | `forbidden` |
| `ResourceNotFoundException` | 404 | `not_found` |
| `EmailAlreadyRegisteredException`, `ConflictException` | 409 | per-case conflict code |
| unmapped domain or infrastructure fault | 500 | `internal_error` |

Lockout and refresh-token reuse remain precise internal outcomes and audit events, but are
collapsed to the generic credential failure on the wire. Own-resource misses and resources
owned by another account both map to 404.

## Transaction and side-effect boundaries

- Refresh rotation marks the presented token, creates its successor and slides only
  `LastActiveAt` in one retry-aware database transaction.
- Verification, reset, MFA and WebAuthn challenges are hashed, typed, expiring and consumed
  atomically.
- SMTP delivery is an in-process bounded queue. Request success never waits for provider
  latency or depends on delivery success.
- Audit writes use their own scope and connection, so rejected authentication and replay
  detections survive rollback of the caller's work.
- Cleanup uses `CleanupOptions.BatchSize` and index-shaped queries. Spent refresh tokens are
  retained until expiry because replay detection depends on them.

