# ADR-0004: ES256 Signing-Key Ring with `kid` Rotation and a JWKS Endpoint

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Signing keys*
- **Affects:** §4 (token architecture), §6 (`SigningKey` entity), §12 (services), §27 (runbook), §29 (rotation cadence)

## Context

Access tokens are asymmetrically signed ([ADR-0001](ADR-0001-token-strategy.md)), so the API needs somewhere to keep a private key and some way for verifiers to obtain the matching public key.

The hard part is not signing — it is rotation. A signing key that cannot be rotated without downtime will not be rotated, and a key that is never rotated becomes a single unbounded point of compromise. Retrofitting rotation onto a system that assumed one static key is painful, because every assumption of "there is *the* key" has to be unwound.

So rotation is designed in from day one, before a single token is issued.

## Decision

A **key ring** persisted as `SigningKey` rows, each with a `KeyId` (`kid`), the protected private key, the public key, and a status.

**Three states, in one direction:**

| State | Signs new tokens | Validates tokens | Published in JWKS |
|---|---|---|---|
| `Active` | ✅ | ✅ | ✅ |
| `Retiring` | ❌ | ✅ | ✅ |
| `Retired` | ❌ | ❌ | ❌ |

**Rotation procedure:** generate a new `Active` key → demote the previous key to `Retiring` → wait **at least the access-token TTL plus clock skew** (15 min + 30 s) → mark it `Retired`. The wait is what makes rotation zero-downtime: tokens signed just before the switch remain verifiable for their whole lifetime.

Every JWT header carries its `kid`. `GET /.well-known/jwks.json` publishes the public keys of all `Active` and `Retiring` keys, so verifiers resolve the right key by `kid` without coordination.

Private keys at rest are protected per **P17** — ✅ **resolved 2026-07-22**: ASP.NET Core Data Protection over the DB rows, recorded in [ADR-0020](ADR-0020-signing-key-storage.md).

## Alternatives considered

**A single static signing key in configuration.** Simplest, and the most common starting point. Rejected: rotation then requires either downtime or mass token invalidation, and in practice means the key is never rotated at all.

**Symmetric HS256 with a shared secret.** Rejected in [ADR-0001](ADR-0001-token-strategy.md) — every verifier would need the signing secret, so there is no meaningful separation between "can verify" and "can mint".

**RS256 instead of ES256.** Well-supported and entirely defensible. ES256 was chosen for materially smaller signatures and keys at equivalent security, which matters for a token sent on every request. Not a security-driven preference — either would have been acceptable.

**Rotation as an automated background job in v1.** Deferred to §29's backlog. v1 rotates through a documented admin procedure; automating a process that destroys the ability to verify outstanding tokens if it gets the timing wrong is not where the first automation effort belongs.

## Consequences

- The JWKS endpoint is **public and unversioned** (`/.well-known/jwks.json`, per the endpoint inventory) — it must expose public keys only. Leaking a private key here would be a total compromise of token integrity, so §22 must assert the serialised JWKS contains no private key material.
- Verifiers can validate tokens without any shared secret and without calling back into the API beyond fetching JWKS.
- Rotation timing is load-bearing: retiring a key sooner than TTL + skew invalidates tokens that are still legitimately in flight. The runbook (§27) must state the minimum wait explicitly rather than leaving it to judgement.
- JWT validation must pin `alg` to ES256. Accepting the algorithm named in the token header is the classic algorithm-substitution vulnerability; §22 tests `alg: none` and substituted-algorithm tokens as explicit negative cases.
- Key rotation cadence is quarterly (§29), plus on demand via the runbook.
- Private-key storage depends on unresolved **P17**. Until it is decided, no key material is generated or persisted.
