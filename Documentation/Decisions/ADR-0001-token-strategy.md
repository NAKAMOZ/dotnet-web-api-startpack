# ADR-0001: Token Strategy — JWT Access Tokens with Opaque Rotating Refresh Tokens

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Token strategy*
- **Affects:** §4 (token architecture), §6 (entities), §12 (services), §22 (security tests)

## Context

The API is headless and serves heterogeneous clients: browser SPAs, mobile apps, CLIs, and server-to-server callers. Every authenticated request needs a credential the API can validate cheaply, and every long-lived session needs a credential that can be revoked.

These two needs pull in opposite directions. Cheap validation wants a self-contained, stateless token. Revocation wants server-side state. Serving both with one token type forces a bad compromise: either a database round-trip on every request, or a credential that stays valid after logout.

## Decision

Two distinct token types with distinct jobs.

**Access token** — a JWT, **15-minute TTL**, signed **ES256**. Self-contained and validated statelessly against the JWKS key ring (see [ADR-0004](ADR-0004-signing-key-management.md)). Carries `iss`, `aud`, `sub`, `sid` (session id), `jti`, `iat`, `exp`, `email_verified`, `roles`, `amr`, and `token_use: access`.

**Refresh token** — a 256-bit CSPRNG value, opaque to the client, **stored only as a SHA-256 hash**. It is:

- **single-use** — each `/auth/refresh` marks the presented token `UsedAt` and issues a successor, linked through `ReplacedByTokenId`;
- **bound to a session row**, so it cannot outlive or escape the session it was minted for;
- **reuse-detecting** — presenting an already-used token revokes the entire session, writes a `token_reuse_detected` audit entry, and returns 401.

The `amr` claim records *how* the session authenticated (`pwd`, `otp`, `webauthn`, `recovery`), so endpoints requiring recent or strong authentication can check it without a second lookup.

## Alternatives considered

**Stateful opaque access tokens** (database lookup on every request). Gives instant revocation, which is genuinely attractive for an auth service. Rejected because it puts the database on the hot path of every single authenticated request in the system — the one component whose latency budget every downstream consumer inherits.

**Long-lived JWTs with no refresh token.** Simplest possible design. Rejected outright: a stolen token stays valid for its full lifetime with no revocation path, and lengthening the TTL to make it practical makes the exposure worse.

**Non-rotating refresh tokens.** Rotation is what makes theft *detectable*. Without it, a stolen refresh token can be used indefinitely alongside the legitimate one, silently. Rejected.

**HS256 (symmetric) signing.** Every verifier would need the signing secret, which makes key distribution a liability and rules out third-party validation via JWKS. ES256 keeps the private key in one place.

## Consequences

- Access-token validation requires no database access — the API scales horizontally without a shared session store.
- **Revocation is not instantaneous for access tokens.** A revoked session's access token remains cryptographically valid until it expires. The 15-minute TTL is the deliberate bound on that window, and it is why the TTL is short rather than convenient.
- Database compromise does not yield usable refresh tokens; only their SHA-256 hashes are stored.
- Reuse detection is *loud by design*: it burns the legitimate user's session rather than letting an attacker coexist silently. A user forced to re-login is a far better outcome than an undetected parallel session.
- Requires a `RefreshToken` entity with a rotation chain (§6) and a `Session` row to bind against ([ADR-0002](ADR-0002-session-lifetime-and-model.md)).
- `SecurityStamp` is checked at refresh time, not per request — this preserves stateless access-token validation while still giving a global per-user kill switch that takes effect within one access-token lifetime.
- §22 must prove the negative cases: `alg: none` rejection, algorithm substitution, expired-token acceptance, and reuse detection firing.
