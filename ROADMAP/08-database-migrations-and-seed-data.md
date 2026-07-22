# 8. Database Migrations and Seed Data

## Objective

Reproducible schema lifecycle: initial migration, deterministic seed data, and a documented migration-execution strategy per environment.

## Scope

Migrations, role seeding, dev-only user seeding, migration runbook.

## Architectural Decisions

- Static reference data (roles) seeded via `HasData` in configurations — versioned inside migrations, deterministic GUIDs.
- Dev/test users seeded at runtime by `Data/Seeding/DevDataSeeder.cs`, executed only when `IHostEnvironment.IsDevelopment()` — never compiled into migrations, so no dev credentials can reach prod schema history.
- Dev: `dotnet ef database update` (or auto-migrate on startup in Development only). Prod: **EF migration bundles** produced by CI (§26), executed as a deploy step (§27) — the API process never auto-migrates in prod.

## Technology Decisions Requiring Approval

None.

## Tasks

- [x] `dotnet ef migrations add InitialCreate` after §7; review generated SQL line-by-line (citext extension creation, indexes, cascades).
- [x] `Data/Seeding/RoleSeed.cs` (`HasData`: Admin, User with fixed GUIDs).
- [🔄] `Data/Seeding/DevDataSeeder.cs` + `IDataSeeder` interface: one admin + one regular dev user, logged loudly at startup. **Passwords pending §12** — see Deviations.
- [x] `Extensions/ApplicationBuilderExtensions.Database.cs`: dev-only migrate+seed call.
- [x] `Documentation/Operations/Migrations.md`: add/apply/rollback runbook, bundle usage, "never edit an applied migration" rule.

## Deviations and decisions taken here

1. **Dev seed passwords are blocked on §12.** `Argon2PasswordHasher` belongs to §12, and hashing is the one thing this workstream cannot do for itself. `Services/Crypto/IPasswordHasher.cs` — the **contract only** — landed here so the seeder can take it as an optional dependency; the accounts are seeded with `PasswordHash = null` and a startup warning until §12 registers an implementation, at which point they gain working passwords with no code change. A placeholder hash was rejected: one that verifies against a known string is a backdoor, one that verifies against nothing is an undiagnosable login bug, and `null` is a state ADR-0006 already sanctions.

2. **All tables moved to an `auth` schema** (`AppDbContext.Schema`, `HasDefaultSchema`), including `__EFMigrationsHistory`. The API shares its database with other things; a dedicated schema keeps its thirteen tables identifiable as one unit and makes the runtime grant scopeable to `auth` alone. Decided during §8 because it changes the initial migration — retrofitting it later would mean moving every table.

3. **No connection string is committed anywhere.** `appsettings.json` and `appsettings.Development.json` carry none: Development reads user-secrets, everything else reads `ConnectionStrings__Postgres` (§25). The §3 composition-root smoke test supplies its own and pins itself to a non-Development environment, so it neither migrates nor seeds.

## Expected Deliverables

`Data/Migrations/*`, seeding files, migrations runbook.

## Dependencies

§7. Blocks §21 (integration tests apply migrations to containers).

## Security Considerations

Dev seed passwords are obviously fake (`Dev_Admin_Password_1!`), logged as a warning at startup, and the seeder refuses to run outside Development. Migration bundles run with a DB role that has DDL rights; the runtime API role does not (documented in §27 checklist).

## Testing Requirements

Integration harness applies real migrations (not `EnsureCreated`) to the Testcontainers database — every test run validates the migration chain.

## Documentation Requirements

Migrations runbook as above.

## Definition of Done

Fresh database stands up from migrations alone; roles present; dev seed works in Development and provably no-ops elsewhere.

**Status: met, with the password caveat above.** Verified against the project's PostgreSQL 18.4 container (`dotnet-postgres`, database `appdb`):

- `dotnet ef database update` on an empty database produced the `auth` schema, 13 tables, `__EFMigrationsHistory`, and the `citext` extension. `public` was left untouched.
- `auth."Roles"` contains `Admin` and `User` with their fixed GUIDs, inserted by the migration rather than by startup code.
- Running the app in Development seeded both accounts with their role assignments and logged the credentials as warnings. A second run left the counts unchanged — the seeder is idempotent by user id.
- The composition-root test boots in a non-Development environment and neither migrates nor seeds; `UseDatabaseSetupAsync` returns immediately.
- Not yet exercised: the production bundle path (§26/§27 own it) and migration application against Testcontainers (§21).

## Questions for the Project Owner

1. The development database credentials were shared in chat. They are stored in user-secrets and nothing is committed, but the password now exists in a conversation log — worth rotating on the container if that matters to you.
