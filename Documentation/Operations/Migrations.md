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
| Development | `dotnet user-secrets set "ConnectionStrings:Postgres" "Host=127.0.0.1;Port=55432;Database=startpack;Username=startpack;Password=…"` |
| Everything else | `ConnectionStrings__Postgres` environment variable (§25) |

Startup fails with a named error when neither is present — a missing connection string should look like a missing connection string, not like a database outage on the first login.

EF design-time commands use `AppDbContextDesignTimeFactory`. This prevents bundle and
scaffold commands from executing `Program.cs`, starting hosted services, or attempting the
Development database setup merely to inspect the model. The factory reads JSON,
environment-specific JSON, user-secrets, environment variables and command-line values in
that order, and keeps the migration history table in the `auth` schema.

---

## 1. Where the tables live

Every table is in the **`auth` schema**, not `public`, including `__EFMigrationsHistory`.

That keeps the API's fourteen tables identifiable as one unit inside a database it may share, and it makes a scoped grant expressible: the runtime role needs rights on `auth` and nothing else.

> The grant must include **write** on `auth."DataProtectionKeys"`. Data Protection creates a successor key at runtime as the active one ages, without a migration, so a read-only grant there produces an outage roughly 90 days after deployment rather than at deploy time (ADR-0021).

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

CI runs `dotnet ef migrations has-pending-model-changes`; changing the model without a
matching migration and snapshot update is therefore a hard failure before bundle creation.

Scaffolded files are exempt from the repository's code-style rules (`.editorconfig`, `[Data/Migrations/*.cs]`, `generated_code = true`). Without that exemption every generated migration fails the build on file-scoped-namespace and unused-using errors — and the hand-edits that fix it are lost on the next regeneration.

---

## 3. Applying

### Development

Automatic. `UseDatabaseSetupAsync` migrates and seeds at startup, **in Development only**. Manually:

```bash
dotnet ef database update
```

### Production — one-shot deployment job, never the API process

CI still builds a self-contained bundle as a portable migration artifact. The Azure deploy
runs the immutable application image in a manual Container Apps job before health-gating the
new app revision (§27):

```bash
dotnet Api.dll operations migrate-database
```

The job uses the administrator connection and `DatabaseDeployment__RuntimePassword`; it
applies migrations and idempotently provisions/grants the DML-only runtime role. The runtime
password is passed through parameterized transaction-local PostgreSQL settings and never
concatenated into the SQL command text.

**The API never auto-migrates outside Development.** Three reasons, in the order they bite:

1. Multiple instances starting together race to apply the same migration.
2. Auto-migration needs DDL rights on the runtime role — precisely the rights an application server should not hold. The job runs as an administrator; the API runs as a role with DML only.
3. A migration that fails during startup leaves the schema half-changed with nobody watching, instead of failing a deploy step that can be stopped and rolled back.

---

## 4. Seed data

Two mechanisms, separated on purpose:

| | `HasData` (roles) | `IDataSeeder` (dev fixtures) |
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

The same seeder adds two recognisable sessions for the ordinary user, a linked GitHub
fixture, three audit events, and the following admin-owned API key with every currently
defined scope:

```text
ak_demoAdmin01_Dev_Demo_Api_Key_Only_Local_2026
```

Their fixed IDs and credentials are repeated in `/playground/`, next to the requests that
consume them. Real provider credentials are not required in Development:
`SocialProviders:DemoMode` maps Google and GitHub authorize/callback requests to
deterministic local identities and is ignored outside Development.

`DevDataSeeder` hashes both passwords with the registered `Argon2PasswordHasher` and repairs
older deterministic rows whose `PasswordHash` is null. Production seeds no user
credentials.

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
psql -c "SELECT tablename FROM pg_tables WHERE schemaname='auth';"   # 14 tables + __EFMigrationsHistory
psql -c 'SELECT "Name" FROM auth."Roles";'                            # Admin, User
psql -c "SELECT extname FROM pg_extension WHERE extname='citext';"    # citext
```

From §21 onward the integration harness applies these same migrations to a Testcontainers database on every run — so the migration chain is validated continuously rather than at release time.

---

## 8. One-off: deploying `AddDataProtectionKeys` (ADR-0021)

**Applies once, to any environment that already holds signing keys.** This is not an ordinary schema migration — it changes where the Data Protection key ring lives *and* the discriminator payloads are protected under, so every `SigningKey.PrivateKeyProtected` value written before it becomes unreadable.

**It does not fail loudly.** `/.well-known/jwks.json` keeps answering `200`, because JWKS projects the public key and never unprotects. The failure surfaces only when the API first tries to *sign* an access token.

Development repairs this case on first signing use by rotating the unreadable active key;
the workbench can therefore reuse an older local database volume without a manual wipe.
That recovery is deliberately disabled outside Development, where changing the issuer key
must remain an announced operational action.

Run as part of the same deploy, after `dotnet ef database update`:

```bash
# Discard every orphaned signing key; the key manager generates a fresh one on next use.
psql -c 'DELETE FROM auth."SigningKeys";'
```

Consequences to expect, and to announce before rather than after:

- Access tokens signed by the discarded keys **fail validation immediately** — the `kid` resolver returns `[]` for an unresolvable key rather than the whole ring, which is the correct behaviour and the reason this is abrupt rather than silent.
- Every client must refresh. Sessions and refresh tokens survive (they are opaque and hashed, not Data-Protection payloads), so this is a re-issue, not a mass logout.
- Live CSRF tokens also fail to unprotect. Cookie-mode clients fetch a new one from `GET /api/v1/auth/csrf`; the first state-changing request after deploy may be rejected once.

A fresh install needs none of this — there are no keys to orphan.

---

## 9. Expand-contract policy

Production rollback means deploying the previous image, not asking an incident responder to
reverse a destructive schema change. Every schema evolution therefore follows three releases:

1. **Expand:** add nullable columns/tables/indexes or compatible defaults. The old image must
   continue to read and write.
2. **Migrate:** deploy code that understands both shapes; backfill in bounded, restartable
   batches and measure completion.
3. **Contract:** only after the minimum overlap window and rollback window have passed,
   remove the old read/write path, then remove the old schema in a later migration.

Rules:

- no rename as drop/add; add the new column, dual-write, backfill, switch reads, then drop;
- no new `NOT NULL` without a populated default/backfill and a separate validation step;
- build large PostgreSQL indexes concurrently through hand-reviewed SQL where table locking
  would violate the availability budget;
- enum/string values are added before writers emit them and removed only after no row uses
  them;
- never combine an irreversible data deletion with the release that stops reading the data;
- every migration PR states compatibility with the previous image and its abort point.

The migration job runs before readiness is accepted. If it fails, the deployment stops.
If the new image fails readiness, roll back the image and leave the additive migration in
place. A destructive `Down()` is not an emergency rollback.
