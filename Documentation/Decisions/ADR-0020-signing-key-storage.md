# ADR-0020: Signing-Key Private Material Protected by ASP.NET Core Data Protection

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decision P17** (`ROADMAP/00-overview.md`). Completes the open item in [ADR-0004](ADR-0004-signing-key-management.md).
- **Affects:** §4 (token architecture), §6 (`SigningKey` entity), §12 (services), §25 (secrets), §27 (deployment runbook)

## Context

[ADR-0004](ADR-0004-signing-key-management.md) put the ES256 key ring in the database and deliberately left one question open: how the private key material is protected at rest. It is the most sensitive value in the system — anyone holding it can mint access tokens for any user, and no amount of correct validation elsewhere compensates.

The deployment target is still undecided (P14), which rules out committing to a cloud KMS today.

## Decision

**Private keys are encrypted with ASP.NET Core Data Protection before being written to `SigningKey.PrivateKeyProtected`.** The column never holds plaintext key material. Public keys are stored unprotected — they are published through JWKS anyway.

The Data Protection payload is created with a **purpose string specific to signing keys**, so a payload from another Data Protection consumer cannot be decrypted by the key manager and vice versa.

**This is explicitly an interim decision.** The vault target is chosen with P7 and the deployment target with P14; when either lands, this ADR is superseded rather than edited.

## Alternatives considered

**Cloud KMS or a vault now** (Azure Key Vault, AWS KMS, HashiCorp Vault). The strongest option, and the intended destination. Rejected as premature: P14 is open, so choosing a provider now means choosing a cloud by proxy — and a KMS integration written against the wrong provider is thrown away. Recorded as the successor decision, not a rejected one.

**A symmetric master key from an environment variable.** Simple and portable. Rejected as a step sideways: it has the same "protect the protector" problem as Data Protection but with none of the key-rotation, key-lifetime, or payload-versioning machinery, all of which would have to be hand-built.

**Plaintext private keys in the database.** Rejected — a database compromise would become a total, silent authentication compromise: an attacker could mint valid tokens indefinitely without touching the application.

**Private keys in configuration or environment variables instead of the database.** Rejected because it defeats the key *ring*. Rotation ([ADR-0004](ADR-0004-signing-key-management.md)) needs several keys coexisting in Active/Retiring/Retired states, which is a data-shaped problem, not a configuration-shaped one.

**Generating an ephemeral key per process.** Rejected: multiple instances would sign with different keys and no instance could validate another's tokens.

## Consequences

- **This moves the secret rather than removing it.** Data Protection's own key ring becomes the thing that must be protected — if it is stored unprotected on disk alongside the database backup, the encryption is decorative. The deployment runbook (§27) must state where the Data Protection key ring lives and how it is protected, and that instruction is not optional.
- The Data Protection key ring must be **shared and persistent across instances**. Its default is per-machine and, in a container, per-container-lifetime — which would make every restart unable to decrypt existing signing keys. §27 configures persistence explicitly; getting this wrong produces an outage on the first restart, not a subtle bug.
- Losing the Data Protection key ring means losing every signing key. Recovery is key rotation plus mass re-authentication of every user — survivable, loud, and worth a runbook entry.
- Private key material must never be logged, never serialised into a response, and never appear in a Problem Details payload. §22 asserts the JWKS response contains public components only.
- Migration to a vault is a change in one component — the key manager's protect/unprotect calls — because nothing else touches the raw material. That containment is why the interim choice is acceptable.
- Local development uses the same mechanism with the developer's local key ring; no plaintext-key shortcut exists for dev, because a dev-only bypass is exactly the code path that reaches production by accident.
