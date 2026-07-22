# ADR-0006: Argon2id Password Hashing with Parameters Versioned in the Hash

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Password hashing*
- **Affects:** §6 (entities), §12 (services), §22 (security tests), §28 (metrics), §29 (parameter upgrades)

## Context

Stored password hashes are what an attacker walks away with after a database compromise. Their only job is to make offline cracking expensive enough to be impractical.

"Expensive enough" is not a constant. Hardware improves, and GPU and ASIC attacks improve faster than general-purpose compute. Any fixed cost parameter chosen today is under-tuned within a few years — so the hash function matters, and so does the ability to raise its cost afterwards without asking every user to reset their password.

## Decision

**Argon2id**, the memory-hard winner of the Password Hashing Competition, chosen for its resistance to GPU and ASIC parallelism — attackers cannot trade memory for speed as freely as with purely compute-bound functions.

**Cost parameters are stored inside the hash string itself**, alongside the salt and digest. Verification reads the parameters from the stored value rather than from current configuration, which means old hashes keep verifying correctly after the configured parameters change.

**Re-hash on login.** After a successful verification, if the stored hash's parameters are weaker than the current configuration, the password — available in plaintext at exactly that moment and no other — is re-hashed with current parameters and the row is updated. The fleet migrates itself as users log in, with no reset emails and no forced churn.

`User.PasswordHash` is **nullable**: social-login-only and passkey-only users have no password, and that is a legitimate state rather than a defect ([ADR-0005](ADR-0005-custom-user-store.md)).

**API keys are hashed with a deliberately cheap Argon2id profile.** API keys are high-entropy machine-generated secrets, not human-chosen passwords, so they are not vulnerable to dictionary attack and do not need a slow hash. The distinction is recorded here so the fast profile is never mistaken for an oversight and "corrected" onto the password path.

## Alternatives considered

**PBKDF2** (what ASP.NET Core Identity uses by default). Widely available and FIPS-friendly, but compute-bound only — GPUs parallelise it very effectively. Rejected: memory-hardness is the property that matters against modern cracking hardware.

**bcrypt.** A solid choice, and far better than PBKDF2. Rejected in favour of Argon2id for its tunable memory cost and its 72-byte input truncation, which is a sharp edge on long passphrases.

**scrypt.** Memory-hard and a reasonable alternative. Argon2id was preferred as the more recent design with the explicit hybrid resistance to both side-channel and time-memory-tradeoff attacks.

**Storing cost parameters in configuration rather than in the hash.** Rejected: it makes hashes non-self-describing, so raising the cost would invalidate every existing hash at once and force a global password reset. Self-describing hashes are what make gradual migration possible at all.

## Consequences

- Argon2id's memory cost is a real server-side resource cost. Parameters must be tuned against production hardware and load, not copied from a blog post — too aggressive and login becomes a denial-of-service vector against ourselves.
- Password verification is deliberately slow, which interacts with rate limiting (§17): the login endpoint is both expensive and attacker-facing, so it needs its own limiter rather than sharing a general bucket.
- Parameter upgrades follow the §29 procedure: raise the configured cost → `NeedsRehash` migrates users on their next login → the `auth.password_hash_duration` metric (§28) confirms the fleet has moved.
- Users who never log in again keep their old, weaker hashes indefinitely. Accepted — the alternative is a forced reset, and a dormant account's hash is only reachable through a database compromise that is already catastrophic.
- The nullable `PasswordHash` means every password code path must handle "this user has no password" explicitly. §22 must confirm a passwordless account cannot be authenticated by supplying an empty or null password.
- The cheap API-key profile must never be applied to user passwords; the two profiles are separate configuration with separate names, and §22 asserts the password path uses the slow one.
