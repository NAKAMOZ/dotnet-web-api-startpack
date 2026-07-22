# Migrations Runbook

**Status:** Written 2026-07-22 · **Workstream:** §8 · **Consumed by:** §21 (test harness), §26 (CI), §27 (deploy)

How schema changes are authored, reviewed, applied and rolled back. The design behind the mapping is [`../Architecture/DataAccess.md`](../Architecture/DataAccess.md); this document is the operational half.

---

## 0. Prerequisites

```bash
dotnet tool restore     # once per clone — installs the pinned dotnet-ef (.config/dotnet-tools.json)
```

**The connection string is never committed.** No `appsettings*.json` in this repository carries one.

| Environment | Source |
|---|---|
| Development | `dotnet user-secrets set "ConnectionStrings:Postgres" "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=…"` |
| Everything else | `ConnectionStrings__Postgres` environment variable (§25) |

Startup fails with a named error when neither is present — a missing connection string should look like a missing connection string, not like a database outage on the first login.

---

## 1. Where the tables live

Every table is in the **`auth` schema**, not `public`, including `__EFMigrationsHistory`.

That keeps the API's thirteen tables identifiable as one unit inside a database it may share, and it makes a scoped grant expressible: the runtime role needs rights on `auth` and nothing else.

```sql
-- what the API owns
SELECT tablename FROM pg_tables WHERE schemaname = 'auth';
```

The schema is created by the initial migration (`EnsureSchema`), so a fresh database needs no manual preparation.

---

## 2. Adding a migration

```bash
dotnet ef migrations add <DescriptiveName> -o Data/Migrations
dotnet ef migrations script                 # review the SQL — always
```

Review the generated SQL before committing, and read it for these specifically:

- **Unintended drops.** A renamed property scaffolds as `DropColumn` + `AddColumn`, which is data loss wearing a rename's clothes. Use `migrationBuilder.RenameColumn` instead.
- **Index changes you did not ask for.** A changed `HasFilter` string silently rebuilds a partial index.
- **`HasData` churn.** Seed rows should only appear in a migration when the seed actually changed. If they show up unprompted, something in `RoleSeed` stopped being deterministic — see §4.
- **Cascade changes.** `ON DELETE` behaviour is a security control here (`DataAccess.md` §4).

Scaffolded files are exempt from the repository's code-style rules (`.editorconfig`, `[Data/Migrations/*.cs]`, `generated_code = true`). Without that exemption every generated migration fails the build on file-scoped-namespace and unused-using errors — and the hand-edits that fix it are lost on the next regeneration.

---

## 3. Applying

### Development

Automatic. `UseDatabaseSetupAsync` migrates and seeds at startup, **in Development only**. Manually:

```bash
dotnet ef database update
```

### Production — bundles, never the API process

CI builds a self-contained executable (§26); deployment runs it as a step before the new version starts (§27):

```bash
dotnet ef migrations bundle --self-contained -r linux-x64 -o efbundle
./efbundle --connection "$ConnectionStrings__Postgres"
```

**The API never auto-migrates outside Development.** Three reasons, in the order they bite:

1. Multiple instances starting together race to apply the same migration.
2. Auto-migration needs DDL rights on the runtime role — precisely the rights an application server should not hold. The bundle runs as a migration role; the API runs as a role with DML only.
3. A migration that fails during startup leaves the schema half-changed with nobody watching, instead of failing a deploy step that can be stopped and rolled back.

---

## 4. Seed data

Two mechanisms, separated on purpose:

| | `HasData` (roles) | `IDataSeeder` (dev users) |
|---|---|---|
| Lives in | The migration | Runtime code |
| Reaches | Every environment | Development only |
| Written by | `Data/Seeding/RoleSeed.cs` | `Data/Seeding/DevDataSeeder.cs` |

**Reference data the authorization model depends on belongs in migrations.** The `Admin` and `User` rows carry fixed GUIDs and a constant timestamp — deterministic because `HasData` is diffed on every scaffold, so a `Guid.NewGuid()` or a `UtcNow` there would emit spurious updates forever and give the same logical role different ids per environment.

**Development credentials must never cross into a migration.** A fake password compiled into schema history is a real password in production. `DevDataSeeder` is guarded twice — the caller only invokes it in Development, and the seeder re-checks the environment itself — and it is idempotent by user id, because the dev loop restarts the app far more often than it recreates the database.

Seeded development accounts (Development only, logged loudly at startup):

| Email | Password | Role |
|---|---|---|
| `admin@localhost.dev` | `Dev_Admin_Password_1!` | `Admin` |
| `user@localhost.dev` | `Dev_User_Password_1!` | `User` |

> Until §12 registers `Argon2PasswordHasher`, these accounts are seeded with **no password** and cannot be logged into. A placeholder hash was rejected deliberately: one that verifies against a known string is a backdoor, one that verifies against nothing is an undiagnosable login bug.

---

## 5. Rolling back

```bash
dotnet ef database update <PreviousMigrationName>   # apply the Down() of everything after it
dotnet ef migrations remove                          # delete the last migration — ONLY if unapplied
```

**Never edit a migration that has been applied anywhere but your own machine.** The history table records which migrations ran by name; editing an applied one leaves every environment that already ran it with a schema the code no longer describes, and nothing detects the divergence. Fix forward with a new migration.

`Down()` is generated but rarely exercised. A rollback that drops a column is data loss — for anything destructive, prefer a forward migration that restores the shape, and take a backup first regardless.

---

## 6. Rebuilding from scratch (Development)

```bash
dotnet ef database drop --force
dotnet ef database update
dotnet run                        # seeds the development accounts
```

`DROP SCHEMA auth CASCADE` is the equivalent if the database holds other schemas you want to keep.

---

## 7. Verifying a fresh install

The §8 Definition of Done, as commands:

```bash
dotnet ef database update
psql -c "SELECT tablename FROM pg_tables WHERE schemaname='auth';"   # 13 tables + __EFMigrationsHistory
psql -c 'SELECT "Name" FROM auth."Roles";'                            # Admin, User
psql -c "SELECT extname FROM pg_extension WHERE extname='citext';"    # citext
```

From §21 onward the integration harness applies these same migrations to a Testcontainers database on every run — so the migration chain is validated continuously rather than at release time.
