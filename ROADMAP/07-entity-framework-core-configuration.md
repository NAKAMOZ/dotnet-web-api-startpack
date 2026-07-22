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

- [ ] `Data/AppDbContext.cs`.
- [ ] `Data/Configurations/` — one file per entity (13): keys, required fields, max lengths, unique indexes (`User.Email` citext-unique, `RefreshToken.TokenHash`, `Session` active lookup `(UserId, RevokedAt)`, `ApiKey.KeyPrefix`, `PasskeyCredential.CredentialId`, `Account (Provider, ProviderAccountId)`, `VerificationToken.TokenHash`), FK behaviors as above.
- [ ] Filtered/partial indexes for hot cleanup queries: `RefreshToken.ExpiresAt WHERE UsedAt IS NULL`, `Session.AbsoluteExpiresAt WHERE RevokedAt IS NULL`.
- [ ] `Data/AuditableEntityInterceptor.cs`.
- [ ] `Extensions/ServiceCollectionExtensions.Data.cs`: `AddDbContext` with Npgsql, retry-on-failure, interceptor registration.
- [ ] `Documentation/Architecture/DataAccess.md` (incl. the no-repository ADR cross-link).

## Expected Deliverables

`Data/` populated (15+ files), data extension method, DataAccess doc.

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

## Questions for the Project Owner

None.
