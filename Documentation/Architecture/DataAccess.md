# Data Access

**Status:** Written 2026-07-22 · **Workstream:** §7 · **Consumed by:** §8 (migrations), §12 (services), §21 (integration tests)

How this API talks to PostgreSQL: the mapping conventions, why each index exists, what a delete destroys, and the two patterns that are easy to get wrong. Decisions implemented here: [ADR-0008](../Decisions/ADR-0008-persistence-postgresql-efcore.md) (PostgreSQL + EF Core), [ADR-0011](../Decisions/ADR-0011-testing-and-ci.md) (`TimeProvider`), [ADR-0013](../Decisions/ADR-0013-package-manifest.md) (package manifest).

---

## 1. There is no repository layer

`DbContext` **is** a unit of work. `DbSet<T>` **is** a repository. A wrapper around them adds a hop and enables nothing:

- It is not needed for testing. Integration tests run against real PostgreSQL through Testcontainers (§21), because the things worth testing here — `citext` uniqueness, cascade behaviour, partial indexes, concurrent rotation — are database behaviours that a mocked repository cannot exhibit.
- It is not needed for swapping providers. There is no second persistence target, and ADR-0008 already accepted Postgres-specific types.
- It would cost the thing that matters most: `IQueryable` composition. The admin endpoints page, filter and sort; a repository returning `IEnumerable` moves that work into memory, and one returning `IQueryable` is `DbSet` with extra steps.

Services depend on `AppDbContext` directly. Revisit only if a second persistence target appears (§29).

---

## 1a. Schema

Every table lives in the **`auth` schema**, not `public` — including `__EFMigrationsHistory`. `AppDbContext.Schema` is the constant; `HasDefaultSchema` applies it to the whole model.

The API may share a database with other things. A dedicated schema keeps its fourteen tables identifiable as one unit, and makes a scoped grant expressible: the runtime role needs rights on `auth` and nothing else, so a mistake elsewhere in the database cannot reach the credential store.

> Thirteen of the fourteen are domain entities. The fourteenth, `DataProtectionKeys`, is the ASP.NET Core Data Protection key ring (ADR-0021) — its entity type ships with the package, nothing in this codebase reads it, and it is the one table written at runtime by something other than a service.

**The connection string is never committed.** Development reads `ConnectionStrings:Postgres` from user-secrets, every other environment from the `ConnectionStrings__Postgres` environment variable (§25). Startup fails with a named error when neither is present.

---

## 2. Mapping conventions

| Concern | Convention | Where |
|---|---|---|
| Entity classes | POCOs. **No EF attributes** — `Models/` reads as a domain model, not a schema | `Models/` |
| Per-entity mapping | One `IEntityTypeConfiguration<T>`, auto-applied by assembly scan | `Data/Configurations/` |
| Type-level mapping | `ConfigureConventions` — applies to every property of a type | `Data/AppDbContext.cs` |
| Primary keys | `Guid` v7, assigned in the entity initializer, `ValueGeneratedNever()` | §6 + each configuration |
| Timestamps | `DateTimeOffset` → `timestamptz`, always | `ConfigureConventions` |
| Enums | Stored as **strings** | `ConfigureConventions` |
| Collections | `text[]` via an explicit converter **and comparer** | `Data/PostgresArrayConversions.cs` |
| Audit stamps | `AuditableEntityInterceptor`, from `TimeProvider` | `Data/AuditableEntityInterceptor.cs` |

**Why enums are strings.** An ordinal column encodes position, so inserting a member in the middle of an enum silently re-points every existing row. The failure is invisible: no error, no migration, just rows that now mean something else. Strings also make the audit table readable without a lookup table the reviewer does not have.

**Why `Guid` v7 rather than database-generated.** v7 is time-ordered, so inserts append to the index instead of scattering across it the way v4 does — the difference compounds on `Sessions`, `RefreshTokens` and `AuditLogEntries`, the three tables that only ever grow. Assigning client-side also means a service holds the id before `SaveChanges`, so it can build a relationship graph in one round trip.

**Why the collection converters carry a `ValueComparer`.** EF compares mutable reference types by reference unless told otherwise. Without a comparer, `key.Scopes.Add("audit:read")` produces **no UPDATE and no error** — the change is simply lost. `PostgresArrayConversions` attaches one to all three collection properties, and a unit test asserts they are present.

**Table naming is EF's default PascalCase**, so identifiers are quoted in SQL (`SELECT * FROM "Users"`). Snake-case naming would need `EFCore.NamingConventions`, and adding a package requires an ADR (ADR-0013) — not worth one for cosmetics. Hand-written SQL in this repository quotes its identifiers.

---

## 3. Index rationale

Every index below exists for a named query or a named invariant. The unique ones are **security controls**: they make a dangerous state impossible in the database rather than unlikely in application code.

### Uniqueness as a constraint

| Index | Prevents |
|---|---|
| `Users (Email)` unique, `citext` | Two accounts for one human. "SELECT then INSERT" loses the race between concurrent registrations; case-insensitivity in the column type means `Alice@x.com` cannot register alongside `alice@x.com` |
| `RefreshTokens (TokenHash)` unique | Ambiguous rotation — two rows matching one presented token |
| `VerificationTokens (TokenHash)` unique | The same, for reset, verification, MFA tickets and WebAuthn challenges |
| `Accounts (Provider, ProviderAccountId)` unique | One external identity linked to two local users |
| `PasskeyCredentials (CredentialId)` unique | An assertion arrives with no user id; a duplicate would make caller resolution a choice |
| `ApiKeys (KeyPrefix)` unique | Ambiguous key lookup |
| `TotpCredentials (UserId)` unique | Two authenticator secrets for one user, so disabling MFA leaves one behind |
| `Roles (Name)` unique | Two role ids granting the same authority |
| `SigningKeys (KeyId)` unique | Two keys answering to one `kid`, so a token could validate against a key that did not sign it |
| `SigningKeys (Status) WHERE Status = 'Active'` unique | **Two active signing keys.** Rotation demotes and promotes in one transaction; if that is ever split, this constraint fails the write instead of leaving a state where retiring either key breaks live tokens |
| `UserRoles (UserId, RoleId)` composite PK | Duplicate role grants |

### Lookup and cleanup

| Index | Query |
|---|---|
| `Sessions (UserId, RevokedAt)` | The session-list endpoint: a user's live sessions |
| `Sessions (AbsoluteExpiresAt) WHERE RevokedAt IS NULL` | Cleanup worker: live sessions past the absolute cap |
| `RefreshTokens (ExpiresAt) WHERE UsedAt IS NULL` | Cleanup worker: unspent tokens past expiry |
| `VerificationTokens (ExpiresAt) WHERE ConsumedAt IS NULL` | Cleanup worker: unconsumed tokens past expiry |
| `RecoveryCodes (UserId, UsedAt)` | MFA fallback: a user's unused codes |
| `ApiKeys (UserId, RevokedAt)` | The key-list endpoint |
| `PasskeyCredentials (UserId)` | The passkey list, and the owner-scoped delete |
| `UserRoles (RoleId)` | "Who holds this role?" — the composite key covers the other direction |
| `AuditLogEntries (UserId, OccurredAt)`, `(EventType, OccurredAt)`, `(OccurredAt)`, `(CorrelationId)` | The four admin audit filters. Each leads with its selective column and ends in `OccurredAt`, so the range predicate is served by the same index rather than a sort |

**The three cleanup indexes are partial on purpose.** Their filters match exactly the rows the worker scans, so each index stays proportional to *live* rows rather than to every row ever written — and on these three tables the difference grows without bound.

**`RecoveryCodes.CodeHash` is deliberately not unique.** Two users may legitimately hold the same code value; a global unique constraint would turn that coincidence into a failed regeneration.

**`RefreshToken.ReplacedByTokenId` is deliberately not a foreign key.** The chain is an audit artefact read by a human reconstructing an incident. An FK would impose a delete order on cleanup — a successor would have to outlive its predecessor — for no runtime benefit.

---

## 4. Cascade map

```text
User ─┬─ Sessions ─── RefreshTokens        CASCADE
      ├─ Accounts                          CASCADE
      ├─ VerificationTokens                CASCADE
      ├─ TotpCredential                    CASCADE
      ├─ RecoveryCodes                     CASCADE
      ├─ PasskeyCredentials                CASCADE
      ├─ ApiKeys                           CASCADE
      ├─ UserRoles                         CASCADE
      └─ AuditLogEntries                   SET NULL   ← the deliberate exception

Role ─── UserRoles                         CASCADE
SigningKey                                 (no owner — belongs to the deployment)
```

**Cascade everywhere is the security position**: deleting an account must destroy every way of authenticating as it. A credential surviving its user is a live credential with no owner to revoke it.

**`AuditLogEntry` is the exception, and it is why `AuditLogEntry.UserId` is nullable.** Deleting an account must not erase the record of what it did. `VerificationToken.UserId` is also nullable — for anonymous passkey authentication challenges — but its FK still cascades, so pending resets die with the account rather than being orphaned.

A unit test asserts this whole map (`AppDbContextModelTests`); it is the kind of thing that regresses quietly when a relationship is reconfigured.

---

## 5. Two patterns that are easy to get wrong

### Transactions under the retrying execution strategy

`EnableRetryOnFailure` is on (3 attempts, 5-second cap). The consequence: **code that opens its own transaction must go through the execution strategy**, or EF throws at runtime.

```csharp
var strategy = db.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

    // ... mark the presented token used, insert the successor, slide the session ...

    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
});
```

§12's refresh rotation is exactly this shape: marking the token used, linking the successor and sliding `LastActiveAt` must succeed or fail together (Authentication.md §6). The retry makes the whole block re-runnable, which is why it has to own the transaction — a retry inside a transaction it did not open would replay half an operation.

### Tracking

Read paths use `AsNoTracking()`; write paths stay tracked. Admin list endpoints are the ones that matter — a tracked page of 100 users builds 100 change-tracking entries for data that is serialised and discarded. Tracking is not a micro-optimisation to discover later; it is the default and it is wrong for reads.

---

## 6. Timestamps

Nothing in the data layer calls `DateTimeOffset.UtcNow`. `AuditableEntityInterceptor` takes `TimeProvider` (ADR-0011) and stamps `CreatedAt`/`UpdatedAt` on save:

- **Added** — both set to the same instant.
- **Modified** — `UpdatedAt` set; `CreatedAt` explicitly marked unmodified, so a detached entity attached with a default value cannot rewrite history.
- One timestamp per `SaveChanges`, not per entity, so rows written by one transaction share a `CreatedAt` and ordering by it is unambiguous.

`AuditLogEntry` implements neither — audit rows are append-only, so `OccurredAt` is set at the call site and there is no `UpdatedAt` to suggest a write path that must not exist.

---

## 7. Migrations

Tooling is a **pinned local tool** (`.config/dotnet-tools.json`), so every machine and CI run uses one version:

```bash
dotnet tool restore                       # once per clone
dotnet ef migrations add <Name> -o Data/Migrations
dotnet ef migrations script               # review the SQL before committing
```

Scaffolded migrations are exempted from the code-style rules in `.editorconfig` (`[Data/Migrations/*.cs]`, `generated_code = true`). Without that, every generated migration fails the build on file-scoped-namespace and unused-using errors, and the hand-edits fixing it are lost on regeneration.

The add/apply/rollback runbook — including the "never edit an applied migration" rule, the bundle-based production path, and the seed-data split — is [`../Operations/Migrations.md`](../Operations/Migrations.md).

---

## 8. What is verified where

| Check | Where |
|---|---|
| `citext`, `jsonb`, `timestamptz`, `text[]`, enum-as-string | `AppDbContextModelTests` (§7, no database) |
| Unique and partial indexes, cascade map, value comparers | `AppDbContextModelTests` |
| Model builds against real PostgreSQL; constraint violations surface as expected | §21 integration tests |
| Interceptor stamps behave under a `FakeTimeProvider` | §21 — it needs a real save to observe |
| Generated SQL matches this document | Reviewed at §8 when `InitialCreate` lands |
