# 21. Integration Testing

## Objective

Prove the composed system — real pipeline, real PostgreSQL, real migrations — behaves per design for every flow in both transports.

## Scope

`tests/IntegrationTests` with `WebApplicationFactory` + Testcontainers.

## Architectural Decisions

- `IntegrationTestFactory : WebApplicationFactory<Program>`: one PostgreSQL container per test run (collection fixture), migrations applied (not `EnsureCreated`), `Respawn` resets tables between test classes.
- Service overrides: `IEmailSender` → `CapturingEmailSender` (exposes sent messages incl. extracted tokens/links); `TimeProvider` → shared `FakeTimeProvider` (advance time to cross expiry boundaries in-test); everything else real.
- Two client helpers: `BearerClient` (body transport) and `CookieClient` (cookie container + CSRF handshake) — every auth flow test runs against both via theory data.
- Passkey ceremonies tested with a software authenticator helper (Fido2NetLib test utilities / hand-rolled credential generator).

## Technology Decisions Requiring Approval

None.

## Tasks

- [ ] Factory + fixtures (`IntegrationTests/Infrastructure/`): PostgreSQL 17 container lifecycle, migrations, Respawn, shared fake clock, and bearer/cookie transport helpers are implemented. `CapturingEmailSender` and `RegisterAndLoginAsync` wait for §12's email/auth service contracts.
- [ ] Flow suites (each its own file):
  - Registration → verification email captured → confirm → `email_verified` claim true after next login.
  - Login/logout both transports; wrong password; lockout after 5; unlock by time advance.
  - Refresh rotation: happy chain; reuse of rotated token → 401 + session revoked + audit row; refresh after 6 h idle (time advance) → 401; refresh past absolute cap → 401.
  - Sessions: list shows device metadata; revoke one (other device's refresh fails); revoke all-but-current; password change kills all sessions.
  - Password reset: request (existing + non-existing email — identical responses), token from captured email, confirm, old sessions dead, old token single-use.
  - MFA: enroll → confirm → login returns 202 ticket → complete with TOTP; recovery code path (single-use); disable requires recent auth.
  - Social: provider stubbed at the `Backchannel` level; callback creates user + account; second login links, not duplicates.
  - Passkeys: register ceremony, authenticate ceremony → session with `amr: webauthn`; delete credential.
  - API keys: create (secret shown once), authenticate with key, scope enforcement, revoke, expiry.
  - Admin: role grant/revoke matrix (403s), user list paging/filter/sort, audit-log query, admin session revocation.
  - Cross-cutting: RFC 9457 envelope on representative errors (§13), security headers (§14), CSRF matrix (§14), rate-limit 429s (§17), JWKS serves active+retiring keys and rotation keeps old tokens valid until expiry.
  - `DocumentationSyncTests` (§19) and OpenAPI snapshot (§18).

### Implementation status (2026-07-26)

- 72 integration tests are green against the real pipeline; the PostgreSQL collection uses
  one random-port Testcontainer and preserves migrations/reference roles across Respawn.
- Refresh rotation/replay, idle/absolute bounds, signing-key/JWKS lifecycle, health
  up/down probes,
  OpenAPI/docs sync, rate limits, headers, and authorization/CSRF matrices are executable.
- The listed registration, login, session-controller, reset, MFA, social, passkey, API-key,
  and admin feature flows remain blocked by the §12 actions that intentionally return 501.
  They are not represented by skipped or placeholder-green tests.

## Expected Deliverables

Full integration suite; runs locally via Docker and in CI.

## Dependencies

§8 (migrations), §11–§17 implemented per slice; suite grows with each slice (same-PR rule).

## Security Considerations

The suite is the executable proof of §4's promises; any refactor that breaks a security property fails CI, not production.

## Testing Requirements

Runtime budget: full suite ≤ 5 min in CI (parallel collections, one shared container).

## Documentation Requirements

`tests/README.md` extended: container prerequisites, running a single flow suite.

## Definition of Done

Every flow suite listed above exists and is green in CI on both transports.

## Questions for the Project Owner

None.
