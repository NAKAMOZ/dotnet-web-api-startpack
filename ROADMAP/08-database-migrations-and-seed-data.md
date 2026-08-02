# 8. Database Migrations and Seed Data

## Objective

Reproducible schema lifecycle: initial migration, deterministic seed data, and a documented migration-execution strategy per environment.

## Scope

Migrations, role seeding, dev-only user seeding, migration runbook.

## Architectural Decisions

- Static reference data (roles) seeded via `HasData` in configurations — versioned inside migrations, deterministic GUIDs.
- Dev/test users seeded at runtime by `Data/Seeding/DevDataSeeder.cs`, executed only when `IHostEnvironment.IsDevelopment()` — never compiled into migrations, so no dev credentials can reach prod schema history.
- Dev: `dotnet ef database update` (or auto-migrate on startup in Development only).
  Production: the reviewed image runs `operations migrate-database` as a one-shot Azure job;
  CI also produces a portable EF bundle. The API process never auto-migrates outside
  Development.

## Technology Decisions Requiring Approval

None.

## Tasks

- [x] `dotnet ef migrations add InitialCreate` after §7; review generated SQL line-by-line (citext extension creation, indexes, cascades).
- [x] `Data/Seeding/RoleSeed.cs` (`HasData`: Admin, User with fixed GUIDs).
- [x] `Data/Seeding/DevDataSeeder.cs` + `IDataSeeder` interface: one admin + one regular dev user, logged loudly at startup; legacy rows with null password hashes are repaired idempotently.
- [x] `Extensions/ApplicationBuilderExtensions.Database.cs`: dev-only migrate+seed call.
- [x] `Data/AppDbContextDesignTimeFactory.cs`: EF tooling inspects the model without
  executing the web host or contacting the Development database.
- [x] `Documentation/Operations/Migrations.md`: add/apply/rollback runbook, bundle usage, "never edit an applied migration" rule.

## Deviations and decisions taken here

1. **Dev seed password dependency is closed.** `Argon2PasswordHasher` is registered by §12. The seeder both hashes new rows and repairs the two deterministic legacy rows when their `PasswordHash` is null.

2. **All tables moved to an `auth` schema** (`AppDbContext.Schema`, `HasDefaultSchema`), including `__EFMigrationsHistory`. The API shares its database with other things; a dedicated schema keeps its fourteen tables identifiable as one unit and makes the runtime grant scopeable to `auth` alone. Decided during §8 because it changes the initial migration — retrofitting it later would mean moving every table.

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

**Status: met, with the password caveat above.** Re-verified against PostgreSQL 18 through
the current Testcontainers/Compose paths:

- Applying the migration chain to an empty database produces the `auth` schema, 14 tables,
  `__EFMigrationsHistory`, and the `citext` extension. `public` is left untouched.
- `auth."Roles"` contains `Admin` and `User` with their fixed GUIDs, inserted by the migration rather than by startup code.
- Running the app in Development seeded both accounts with their role assignments and logged the credentials as warnings. A second run left the counts unchanged — the seeder is idempotent by user id.
- The composition-root test boots in a non-Development environment and neither migrates nor seeds; `UseDatabaseSetupAsync` returns immediately.
- The 105-test integration suite applies real migrations; the least-privilege deployment
  operation is idempotent and the runtime role cannot alter schema.
- CI rejects pending model/snapshot changes and builds a self-contained Linux x64 EF bundle;
  the exact bundle chain was reproduced locally on 2026-08-02.

## Questions for the Project Owner

1. The development database credentials were shared in chat. They are stored in user-secrets and nothing is committed, but the password now exists in a conversation log — worth rotating on the container if that matters to you.
