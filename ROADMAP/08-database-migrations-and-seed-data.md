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

- [ ] `dotnet ef migrations add InitialCreate` after §7; review generated SQL line-by-line (citext extension creation, indexes, cascades).
- [ ] `Data/Seeding/RoleSeed.cs` (`HasData`: Admin, User with fixed GUIDs).
- [ ] `Data/Seeding/DevDataSeeder.cs` + `IDataSeeder` interface: one admin + one regular dev user, Argon2id-hashed known passwords, logged loudly at startup.
- [ ] `Extensions/ApplicationBuilderExtensions.Database.cs`: dev-only migrate+seed call.
- [ ] `Documentation/Operations/Migrations.md`: add/apply/rollback runbook, bundle usage, "never edit an applied migration" rule.

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

## Questions for the Project Owner

None.
