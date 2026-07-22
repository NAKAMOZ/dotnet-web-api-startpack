# 4. Authentication and Token Architecture

## Objective

Specify the complete token lifecycle — issuance, validation, refresh rotation, reuse detection, revocation, key rotation, dual transport — precisely enough that §6/§12 implement it without further design work.

## Scope

Design document + interface definitions. Implementation lands in §12; entities in §6.

## Architectural Decisions

- **Access token (JWT, ES256, 15 min)** claims: `iss`, `aud`, `sub` (user id), `sid` (session id), `jti`, `iat`, `exp`, `email_verified`, `roles`, `amr` (`pwd`, `otp`, `webauthn`, `recovery` — records how the session authenticated), `token_use: access`.
- **Refresh token**: 256-bit CSPRNG value, transmitted opaque, stored as SHA-256 hash. **Single-use**: each `/auth/refresh` marks the presented token `UsedAt`, issues a successor (`ReplacedByTokenId` chain), and slides `Session.LastActiveAt`. Presenting an already-used token = **reuse detection** → revoke the session, audit `token_reuse_detected`, 401.
- **Session expiry**: valid while `now < LastActiveAt + 6h` **and** `now < AbsoluteExpiresAt` (login time + **7 days**, P1 approved — `ADR-0002`). Refresh beyond either bound fails → re-login. A refresh slides `LastActiveAt` but never extends `AbsoluteExpiresAt`.
- **Dual transport**:
  - Browser: access token in `__Host-auth.access` cookie (`httpOnly`, `Secure`, `SameSite=Lax`, `Path=/`); refresh token in `__Secure-auth.refresh` cookie (`httpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth/refresh` — the `__Host-` prefix is incompatible with a restricted path, hence `__Secure-`).
  - Non-browser: both tokens in the JSON response body; access token sent back as `Authorization: Bearer`.
  - Client selects mode via `X-Auth-Transport: cookie|body` header on login (default `body`); server never sends tokens in both places at once.
- **CSRF (cookie mode only)**: double-submit token — `GET /api/v1/auth/csrf` sets a non-httpOnly `__Host-auth.csrf` cookie; state-changing requests authenticated via cookie must echo it in `X-CSRF-Token`; enforced by a filter that exempts bearer-authenticated requests.
- **Key management**: `SigningKey` ring in DB, private keys protected at rest (P17). States: Active (signs) → Retiring (validates only, published in JWKS) → Retired (removed). Rotation = generate new Active, demote old to Retiring for ≥ access-TTL + clock skew, then retire. `kid` in every JWT header; JWKS serves Active + Retiring public keys.
- **Password change / reset**: bump `User.SecurityStamp`, revoke all sessions (approved decision), audit.
- **Login with MFA enrolled**: password success returns `202` with a short-lived (5 min) single-use **MFA ticket** (opaque, hashed, stored in `VerificationToken` with type `MfaChallenge`) instead of tokens; `/auth/login/mfa` exchanges ticket + TOTP/recovery code for tokens.
- **Social login (P13)**: API-driven redirect first — `authorize` returns 302 to provider with `state` (signed, short-lived); `callback` exchanges code server-side, links/creates `Account` + `User`, issues session exactly like password login. SPA-PKCE variant deferred until P13 approval.
- **Passkeys**: standard WebAuthn ceremonies via Fido2NetLib; challenge stored server-side with 5-min TTL; successful assertion issues a session with `amr: webauthn`.
- **API keys**: format `ak_<prefix>_<secret>`; prefix stored plaintext for O(1) lookup, secret Argon2id-hashed (cheap parameters — keys are high-entropy, unlike passwords, so a fast hash profile is acceptable and documented); authenticated by a dedicated `ApiKeyAuthenticationHandler` scheme; scopes map to the permission constants (§5).

## Technology Decisions Requiring Approval

P12, P13, P17. (**P1 resolved**: 7-day absolute cap — `ADR-0002`.)

## Tasks

- [ ] Write `Documentation/Architecture/Authentication.md`: sequence diagrams (login, refresh+rotation, reuse detection, MFA challenge, social callback, passkey ceremonies), state machines for `Session` and `RefreshToken`, cookie matrix, claims table.
- [ ] Define interfaces (files in `Services/Tokens/`, bodies in §12): `IAccessTokenIssuer`, `IRefreshTokenService`, `ISessionService`, `ISigningKeyManager`, `IMfaTicketService`.
- [ ] Define `Configuration/JwtOptions.cs`, `Configuration/SessionOptions.cs`, `Configuration/CookieOptions.cs` (typed options, validated on start — §25).
- [ ] Specify clock-skew tolerance (recommend 30 s) and document it in `JwtOptions`.

## Expected Deliverables

`Documentation/Architecture/Authentication.md`, interface files, options classes.

## Dependencies

§3 (skeleton). Blocks §6, §12.

## Security Considerations

This section is written deliberately and fully — it is the core security design:

- Refresh tokens are never stored in plaintext; DB compromise does not yield usable tokens.
- Reuse detection revokes the whole session, not just the token — an attacker replaying a stolen rotated token burns the legitimate user's session loudly (audited) instead of silently coexisting.
- JWT validation pins `alg` to ES256 and validates `iss`/`aud`/lifetime strictly; `alg: none` and algorithm-substitution are rejected by configuration, and §22 tests prove it.
- `SameSite=Strict` on the refresh cookie plus path scoping means browsers never attach it outside the refresh endpoint.
- MFA tickets, WebAuthn challenges, and verification tokens are all single-use, hashed at rest, and short-lived.
- `SecurityStamp` gives an instant global kill switch per user independent of token TTLs (checked at refresh time, not per-request — preserving stateless access-token validation).

## Testing Requirements

Design reviewed against §22's negative-test list before implementation starts (every attack listed there must have a designed defense here).

## Documentation Requirements

Architecture doc kept current as the single source of truth; endpoint docs (§19) link to it.

## Definition of Done

Architecture doc reviewed and approved by owner; interfaces compile; every §22 attack scenario has a mapped defense.

## Questions for the Project Owner

1. ~~Confirm P1 cap~~ ✅ **7 days, approved 2026-07-22.** P17 key storage still open.
2. Google + GitHub as launch providers (P12)? Redirect-first social flow (P13)?
3. Is the `X-Auth-Transport` header approach acceptable, or should transport be inferred (cookie present ⇒ cookie mode)?
