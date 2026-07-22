# 7. Entity Framework Core Configuration

## Objective

Explicit, reviewable EF Core mapping: every relationship, index, and constraint declared in one `IEntityTypeConfiguration<T>` per entity.

## Scope

`AppDbContext`, 13 configuration classes, interceptors, conventions. Migrations are §8.

## Architectural Decisions

- **No repository / unit-of-work layer.** Justification (as required): `DbContext` *is* a unit of work and `DbSet<T>` *is* a repository; a wrapper would add indirection without enabling anything — services depend on `AppDbContext` directly and integration tests run against real PostgreSQL (§21), so there is no mocking need that a repository would serve. Revisited only if a second persistence target ever appears (§29).
- `AppDbContext` in `Data/AppDbContext.cs`; configurations auto-applied via `ApplyConfigurationsFromAssembly`.
- PostgreSQL specifics: `citext` extension for `User.Email`; `jsonb` for `AuditLogEntry.Metadata`; `timestamptz` for all `DateTimeOffset`.
- Enums stored as strings (readable audits, safe reordering).
- Read paths use `AsNoTracking()`; write paths tracked — convention documented in `Documentation/Architecture/DataAccess.md`.
- `AuditableEntityInterceptor` (in `Data/`) stamps `CreatedAt`/`UpdatedAt` from `TimeProvider`.
- Deletes: `User` delete cascades to owned auth artifacts (sessions, tokens, credentials, keys) — deliberate: account deletion must destroy access; `AuditLogEntry.UserId` uses `SetNull` to preserve the audit trail.

## Technology Decisions Requiring Approval

None (PostgreSQL + EF Core approved).

## Tasks

- [x] `Data/AppDbContext.cs`.
- [x] `Data/Configurations/` — one file per entity (13): keys, required fields, max lengths, unique indexes (`User.Email` citext-unique, `RefreshToken.TokenHash`, `Session` active lookup `(UserId, RevokedAt)`, `ApiKey.KeyPrefix`, `PasskeyCredential.CredentialId`, `Account (Provider, ProviderAccountId)`, `VerificationToken.TokenHash`), FK behaviors as above.
- [x] Filtered/partial indexes for hot cleanup queries: `RefreshToken.ExpiresAt WHERE UsedAt IS NULL`, `Session.AbsoluteExpiresAt WHERE RevokedAt IS NULL`, plus `VerificationToken.ExpiresAt WHERE ConsumedAt IS NULL` — the same cleanup shape, same reasoning.
- [x] `Data/AuditableEntityInterceptor.cs`.
- [x] `Extensions/ServiceCollectionExtensions.Data.cs`: `AddDbContext` with Npgsql, retry-on-failure, interceptor registration.
- [x] `Documentation/Architecture/DataAccess.md` (incl. the no-repository ADR cross-link).

## Additions beyond the task list

1. **`Data/PostgresArrayConversions.cs`** — the three collection properties (`Session.AuthenticationMethods`, `ApiKey.Scopes`, `PasskeyCredential.Transports`) map to `text[]` through a shared converter **and value comparer**. The comparer is not optional: without one EF compares these by reference, so an in-place mutation produces no UPDATE and no error.
2. **A partial unique index on `SigningKeys.Status WHERE Status = 'Active'`** — encodes "exactly one active signing key" as a database constraint rather than a rotation convention.
3. **`tests/UnitTests/Data/AppDbContextModelTests.cs`** (10 tests) — asserts `citext`, `jsonb`, `timestamptz`, enum-as-string, the unique and partial indexes, the cascade map and the value comparers. No database: EF builds the model without connecting, so these run in milliseconds and catch the mappings whose absence is otherwise silent.
4. **`.config/dotnet-tools.json`** — `dotnet-ef` pinned at 10.0.10 as a local tool, so §8 and CI (§26) use one version.
5. **`.editorconfig` section for `Data/Migrations/*.cs`** (`generated_code = true`). Scaffolded migrations use block-scoped namespaces and a fixed using block, both of which are build **errors** under the §3 rules — without this exemption every `dotnet ef migrations add` breaks the build and the hand-edits fixing it are lost on regeneration.
6. **EF Core runtime packages pinned centrally** (`Microsoft.EntityFrameworkCore`, `.Relational`, `.Abstractions` at 10.0.10). Not new dependencies — both were already in the graph. The Npgsql provider asks for 10.0.4 and `Design` asks for 10.0.10, and because `Design` is `PrivateAssets=all` the test projects resolved 10.0.4 against an API assembly compiled against 10.0.10, producing MSB3277 on every test build. No ADR: ADR-0013 already endorses transitive pinning.
7. **Connection string added to `appsettings.json`** (`ConnectionStrings:Postgres`, localhost dev default). `Include Error Detail=true` lives in `appsettings.Development.json` only — it inlines parameter *values* into Npgsql exception messages, which is a data leak anywhere else.

## Expected Deliverables

`Data/` populated (17 files), data extension method, DataAccess doc, model-shape unit tests.

## Dependencies

§6. Blocks §8.

## Security Considerations

Unique constraints are security controls here (duplicate emails, colliding token hashes must be DB-impossible, not just app-checked). Connection strings come from configuration (§25), never literals.

## Testing Requirements

Integration (§21): model builds against real PostgreSQL (`EnsureCreated` smoke), constraint violations surface as expected.

## Documentation Requirements

DataAccess doc: conventions, index rationale, cascade map.

## Definition of Done

`dotnet ef migrations add` produces a migration whose generated SQL is reviewed and matches the design (indexes, citext, cascades all present).

**Status: met.** A migration was scaffolded as a dry run and its SQL reviewed against this design:

- `CREATE EXTENSION IF NOT EXISTS citext;` emitted; `Users."Email"` is `citext` with a unique index.
- Every `DateTimeOffset` column is `timestamptz`; `AuditLogEntries."Metadata"` is `jsonb`; the three collection columns are `text[]`; every enum column is `character varying` — including the nullable `Sessions."RevocationReason"`, confirming the convention reaches `Nullable<TEnum>`.
- 26 indexes: 11 unique, 3 partial (`WHERE "UsedAt" IS NULL`, `WHERE "RevokedAt" IS NULL`, `WHERE "ConsumedAt" IS NULL`) plus the `WHERE "Status" = 'Active'` unique filter.
- 11 foreign keys: `ON DELETE CASCADE` on all ten credential relationships, `ON DELETE SET NULL` on `AuditLogEntries`.

The scaffold was then removed with `dotnet ef migrations remove --force` — §8 owns the committed `InitialCreate`, and leaving a second initial migration behind would have made its first task ambiguous.

## Questions for the Project Owner

None.
