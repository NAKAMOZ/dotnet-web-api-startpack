# ADR-0021: Data Protection Key Ring Persisted to the Database

- **Status:** Superseded in production by ADR-0027; PostgreSQL persistence remains
- **Date:** 2026-07-23
- **Deciders:** Project owner
- **Source:** Closes the persistence consequence left open by [ADR-0020](ADR-0020-signing-key-storage.md); §16 task 3.
- **Affects:** §12 (signing keys, CSRF tokens), §14 (CSRF filter), §16 (security hardening), §27 (deployment, multi-instance)

## Context

`AddDataProtection()` was registered in §12 with no persistence configured. Two components depend on that key ring:

- `SigningKeyManager` — encrypts ES256 **private key material** before it is written to `SigningKey.PrivateKeyProtected` (ADR-0020).
- `CsrfTokenService` — issues the session-bound tag through an `ITimeLimitedDataProtector` (§14).

With no explicit provider, Data Protection resolves storage by probing the host: a registry key on Windows, `$HOME/.aspnet/DataProtection-Keys` where a home directory exists, and — where neither does, which is the normal case for a container running as a non-root user with no writable home — **an ephemeral in-memory ring, announced only as a startup warning**.

The consequences are not gradual:

- Every process restart generates a fresh ring. Every `SigningKey` row in the database becomes permanently undecryptable, so the key manager cannot sign, `/.well-known/jwks.json` cannot be built from stored keys, and token issuance stops.
- Every in-flight CSRF token fails to unprotect, so every authenticated cookie-mode state-changing request is rejected until the client fetches a new token.
- Horizontal scaling is impossible before it is attempted: two instances cannot read each other's payloads.

ADR-0020 recorded this as a §27 obligation. It is being closed now instead, because the failure is present in the code today and dev machines — which have a writable `$HOME` — do not reproduce it.

## Decision

**The Data Protection key ring is persisted to PostgreSQL through `AppDbContext`**, in the same `auth` schema as everything else, using `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`:

```csharp
services.AddDataProtection()
    .SetApplicationName(DataProtectionApplicationName)
    .PersistKeysToDbContext<AppDbContext>();
```

`AppDbContext` implements `IDataProtectionKeyContext`; the `DataProtectionKeys` table is mapped in `Data/Configurations/DataProtectionKeyConfiguration.cs` like every other table, and created by a migration.

**`SetApplicationName` is part of the decision, not a detail.** Unset, Data Protection derives its isolation discriminator from the content root path. Two instances deployed at different paths would share the ring and still fail to decrypt each other's payloads — a failure indistinguishable from persistence not working at all. The name is a fixed constant, not configuration: an environment that changes it silently invalidates every existing payload.

Adding the package is the ADR-required event under [ADR-0013](ADR-0013-package-manifest.md). It is a first-party ASP.NET Core package pinned to `10.0.10`, matching every other `Microsoft.AspNetCore.*` and EF Core pin in `Directory.Packages.props`.

## Alternatives considered

**`PersistKeysToFileSystem` on a mounted volume.** No new package. Rejected because it moves the requirement into infrastructure that this repository cannot assert: a missing volume mount produces the exact ephemeral-ring failure above, silently, and the only place it would be caught is production. The database is already a hard dependency with a backup story; the key ring inherits it.

**A cloud KMS or vault now.** This was blocked by P7/P14 when the ADR was written. Both were
later resolved by ADR-0027, which keeps PostgreSQL persistence and wraps the ring with a
versionless Azure Key Vault key.

**Encrypting the ring at rest with `ProtectKeysWith*`.** Deferred, not rejected. Every option is host-specific (DPAPI, X.509 certificate, Azure Key Vault), so choosing one is choosing a deployment target. Recorded below as an accepted, named gap.

**Leaving it to §27.** Rejected on the timing: §12's signing-key ring is live now, and a defect that only manifests in an unbuilt deployment is still a defect in the shipped composition root.

## Consequences

- **The protected material and its protector share one database.** A full database compromise yields both, so Data Protection is not defence against that — it defends against a leaked backup of the `SigningKey` table alone, an over-broad read grant, or a query log. §27 must still keep the two in different backup/restore trust boundaries if that stays unacceptable, and that is the successor decision, not this one.
- The original unwrapped database ring was an explicit gap. Production now calls
  `ProtectKeysWithAzureKeyVault` and fails startup without a versionless Key Vault key URI;
  local development intentionally retains database-only persistence.
- The runtime database role now needs read **and write** on `auth."DataProtectionKeys"` — Data Protection creates a new key when the current one nears expiry, at runtime, without a migration. A read-only grant produces an outage 90 days after deployment rather than at deploy time.
- Migrations must be applied before the first protect/unprotect call. Development migrates at startup (`UseDatabaseSetupAsync`); production applies the bundle as a deploy step. Both already order correctly.
- Losing this table is equivalent to losing the ring: recovery is signing-key rotation plus mass re-authentication, as ADR-0020 states. It is now covered by the ordinary database backup, which is the point.
- `SetApplicationName` is now load-bearing and must never be changed, environment-scoped, or derived from configuration.
- **Adopting this is a one-way door for existing protected payloads.** Any `SigningKey` row written before this change was protected by the previous ring and under the previous, path-derived discriminator; the new ring cannot read it. Verified locally on 2026-07-23: Data Protection created a fresh ring key on first use rather than importing the old one, leaving the existing signing key orphaned. **It does not fail loudly.** `/.well-known/jwks.json` keeps answering `200`, because JWKS projects the *public* key and never unprotects — the failure appears only when something signs. Every environment that already has signing keys must delete them as part of this deploy, so the key manager generates a fresh ring; see `Documentation/Operations/Migrations.md`.
