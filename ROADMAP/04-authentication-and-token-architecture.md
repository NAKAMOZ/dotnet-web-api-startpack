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

✅ **None outstanding.** All resolved 2026-07-22: P1 (7-day cap, `ADR-0002`), P12 + P13 (Google + GitHub, API-driven redirect — `ADR-0019`), P17 (Data Protection — `ADR-0020`).

## Tasks

- [x] Write `Documentation/Architecture/Authentication.md`: sequence diagrams (login, refresh+rotation, reuse detection, MFA challenge, social callback, passkey ceremonies), state machines for `Session` and `RefreshToken`, cookie matrix, claims table.
- [x] Define interfaces (files in `Services/Tokens/`, bodies in §12): `IAccessTokenIssuer`, `IRefreshTokenService`, `ISessionService`, `ISigningKeyManager`, `IMfaTicketService`.
- [x] Define `Configuration/JwtOptions.cs`, `Configuration/SessionOptions.cs`, `Configuration/CookieOptions.cs` (typed options, validated on start — §25).
- [x] Specify clock-skew tolerance (recommend 30 s) and document it in `JwtOptions`.

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

✅ **Review performed 2026-07-22.** All 20 attacks in §22's list are mapped in `Documentation/Architecture/Authentication.md` §16. The review found **six gaps** in the first draft of the design; all are now closed:

| # | Gap found | Fix |
|---|---|---|
| 1 | **"Recent auth" was never defined**, despite four endpoints in the inventory depending on it | New §14. Added the `auth_time` claim, a 5-minute `RecentAuthenticationWindow`, and `ISessionService.MarkReauthenticatedAsync` |
| 2 | `kid` resolution unspecified — unknown and retired `kid` behaviour undefined | New §12 subsection: exact match only, **no fallback to trying other keys** |
| 3 | CSRF was plain double-submit | Token is now HMAC-bound to `sessionId`; §22 asserts a token from another session fails |
| 4 | Algorithm confusion described only generically | §2 now names the concrete attack: sign with `HS256` using our *public* key as the HMAC secret |
| 5 | Lockout and enumeration parity absent from the login flow | §5 now states the three constraints §16 imposes on what login may return |
| 6 | Cross-session refresh-token use not addressed | §6 states the binding that closes it |

Gap 1 is the substantive one. Without `auth_time`, the natural implementation checks `iat` — which moves forward on every refresh, so a stolen session would satisfy step-up permanently. That is a control that silently does nothing while appearing to work.

§22 also lists input-abuse attacks (oversized bodies, malformed JSON, correlation-ID injection, sort-field injection). Those are §13/§14/§17 concerns and are named as out-of-scope in §16 of the architecture doc so the omission is deliberate rather than overlooked.

## Documentation Requirements

Architecture doc kept current as the single source of truth; endpoint docs (§19) link to it.

## Definition of Done

Architecture doc reviewed and approved by owner; interfaces compile; every §22 attack scenario has a mapped defense.

- [x] **Interfaces compile** — `dotnet build`, 0 warnings, 0 errors.
- [x] **Every §22 attack scenario has a mapped defence** — all 20 mapped in `Documentation/Architecture/Authentication.md` §16, six design gaps found and closed (see Testing Requirements above).
- [ ] **Architecture doc reviewed and approved by the owner** — the outstanding item. §4 stays 🔄 until this is signed off.

### Deviations from this workstream's original text

1. **`Configuration/CookieOptions.cs` is named `AuthCookieOptions.cs`.** `Microsoft.AspNetCore.Http.CookieOptions` already exists and is in scope through implicit usings, so the roadmap's name would collide on every file that touches both.

2. **`ISigningKeyManager` exposes `SignAsync`, not a key accessor.** The roadmap did not
   specify the shape. Handing out an unprotected key would spread private material across
   components; keeping signing inside the manager let ADR-0027 add Key Vault wrapping at the
   Data Protection registration boundary without changing signing call sites.

3. **`amr` gained `google` and `github` values** beyond the roadmap's `pwd`/`otp`/`webauthn`/`recovery`, now that P12 names concrete providers. A session authenticated by social login is otherwise indistinguishable from a password login in the token.

### Also landed (not in the original list)

- `SessionRevocationReason` and `RefreshOutcome` enums. Both lifetime bounds and both revocation paths need distinguishable outcomes — `ADR-0002` requires that the API tell "you were idle" apart from "your session aged out", and that distinction has to exist in the service contract, not just the HTTP layer.
- `ADR-0019` (P12 + P13) and `ADR-0020` (P17) written; `ADR-0003`'s open transport question closed; `ADR-0004`'s P17 placeholder resolved.
- **Step-up authentication** (§14 of the architecture doc): the `auth_time` claim, `SessionOptions.RecentAuthenticationWindow`, `AccessTokenRequest.AuthenticatedAt`, and `ISessionService.MarkReauthenticatedAsync`. The roadmap marks four endpoints 🔐 *(recent auth)* but never defined the mechanism; the §22 cross-check surfaced it.
- **`kid` resolution rules** and **session-bound CSRF tokens**, both also surfaced by the §22 cross-check.

## Questions for the Project Owner

1. ~~Confirm P1 cap and P17 key storage.~~ ✅ **7-day cap** (`ADR-0002`); **Data Protection** for key material (`ADR-0020`).
2. ~~Google + GitHub as launch providers (P12)? Redirect-first social flow (P13)?~~ ✅ **Both yes** — `ADR-0019`.
3. ~~Is the `X-Auth-Transport` header approach acceptable?~~ ✅ **Yes, the header stays.** ADR-0003's open note is closed.

**Remaining:** formal owner sign-off on `Documentation/Architecture/Authentication.md`.
