# Audit Trail

Source of truth for the security audit trail (§15): the event catalog, who writes each event, what is stored, how long it is kept, and how it is queried.

The trail is **not** the application log. `Documentation/Decisions/ADR-0010-logging-serilog.md` covers operational logging — diagnostic, sampled, disposable. This document covers the durable security record: one row per security-relevant event, in `auth."AuditLogEntries"`, queryable by field, surviving log rotation.

---

## 1. The two systems, and why they are two

| | Operational log | Audit trail |
|---|---|---|
| Written by | `ILogger<T>` → Serilog | `IAuditLogger` → PostgreSQL |
| Audience | Engineers debugging | Administrators investigating |
| Retention | Whatever the log platform keeps | **90 days** (P18, §4 below) |
| Queryable by | Whatever the sink indexes | `GET /api/v1/admin/audit-logs`, backed by declared indexes |
| Survives log rotation | No | Yes |
| Contains emails | Masked (`n***@example.com`) | **In full, deliberately** — see §5 |

Conflating them fails in both directions: a security record that ages out with the debug logs cannot answer "what happened three weeks ago", and a debug log durable enough to answer it is a privacy liability.

The two join on `CorrelationId`. An audit row names the request that produced it, and the log lines from that same request carry the same value under the same property name (§14, `CorrelationIdEnricher`).

---

## 2. The event catalog

The closed set lives in `Models/Enums/AuditEventType.cs`. **Adding an event means adding an enum member first** — `AuditCatalogTests` fails the build when the enum and this table disagree, in either direction.

The stored form is the enum member name (`LoginFailed`); the snake_case name is how the event is written in prose and in this table.

| Event | Enum member | Subject (`UserId`) | Written by |
|---|---|---|---|
| `user_registered` | `UserRegistered` | the new account | §12 registration service |
| `login_succeeded` | `LoginSucceeded` | the account | §12 login service |
| `login_failed` | `LoginFailed` | the account, or **null** when the address is unknown | §12 login service |
| `mfa_challenge_issued` | `MfaChallengeIssued` | the account | §12 login service |
| `mfa_failed` | `MfaFailed` | the account | §12 MFA service |
| `account_locked` | `AccountLocked` | the account | §12 login service |
| `token_refreshed` | `TokenRefreshed` | the account | §12 refresh service |
| `token_reuse_detected` | `TokenReuseDetected` | the account | §12 refresh service |
| `session_revoked` | `SessionRevoked` | the account whose session ended | §12 session service, and `AuditActionFilter` for the admin route |
| `password_changed` | `PasswordChanged` | the account | §12 users service |
| `password_reset_requested` | `PasswordResetRequested` | the account, or null for an unknown address | §12 password-reset service |
| `password_reset_completed` | `PasswordResetCompleted` | the account | §12 password-reset service |
| `email_verified` | `EmailVerified` | the account | §12 email-verification service |
| `mfa_enrolled` | `MfaEnrolled` | the account | §12 MFA service |
| `mfa_disabled` | `MfaDisabled` | the account | §12 MFA service |
| `passkey_registered` | `PasskeyRegistered` | the account | §12 passkeys service |
| `passkey_removed` | `PasskeyRemoved` | the account | §12 passkeys service |
| `api_key_created` | `ApiKeyCreated` | the owning account | §12 API-keys service |
| `api_key_revoked` | `ApiKeyRevoked` | the owning account | §12 API-keys service |
| `role_granted` | `RoleGranted` | the target account | `AuditActionFilter` |
| `role_revoked` | `RoleRevoked` | the target account | `AuditActionFilter` |
| `admin_user_updated` | `AdminUserUpdated` | the target account | `AuditActionFilter` |
| `admin_user_deleted` | `AdminUserDeleted` | **null** — see §4 | §12 users service |
| `signing_key_rotated` | `SigningKeyRotated` | null (no subject) | `SigningKeyManager.RotateAsync` (operator command; future job reuses it) |

The §12 feature services and `AuditActionFilter` now supply the producers in this table.
Service-owned events are emitted at the domain transition; attribute-owned events are
emitted only after a successful 2xx action.

---

## 3. Who writes an event, and how it is decided

Two mechanisms, and the choice between them is not stylistic.

**`AuditActionFilter` — for events that *are* the HTTP action.** Granting a role is exactly "this endpoint succeeded". The filter is registered globally and does nothing unless the action carries `[AuditEvent(AuditEventType.X)]`, so the mapping lives on the action rather than in a lookup table that a rename silently breaks. It records only on a successful 2xx action.

**`IAuditLogger` called from the service — for everything else.** A failed login is not an endpoint outcome (the endpoint answers 401 either way, deliberately); a token-reuse detection happens three layers below the action; account lockout has no endpoint at all. These events know things the filter cannot see, and they must be recorded even when the request ends in an error.

An action must never do both. Two writers, one event, two rows.

### Writes are independent of the caller's transaction

`AuditLogger` opens its **own** service scope, with its own `AppDbContext` and its own connection, and commits alone.

This looks like an over-complication and is not. The events most worth having are the ones whose surrounding transaction is about to roll back — `login_failed` has nothing else to commit, and `token_reuse_detected` fires on a path that revokes a session and may itself throw. An audit row enlisted in the caller's transaction vanishes exactly when an incident occurs. The accepted cost is that a row can exist for an operation that then failed; for a trail, "this was attempted" is the claim being made.

---

## 4. Two traps in the schema

**`AuditLogEntry.UserId` is a foreign key with `ON DELETE SET NULL`.** Deleting an account does not erase the record of what it did — the rows survive with a null subject. This is the one relationship in the model where a user delete deliberately leaves data behind (DataAccess.md §4).

The consequence is not obvious: **`admin_user_deleted` cannot carry the deleted user's id in `UserId`.** The row is written after the delete commits, and inserting a foreign key to a row that no longer exists violates the constraint — the one event nobody can afford to lose would be the one that fails to write. It is therefore recorded with a null subject and the deleted id in `Metadata`, from inside §12's deletion service. `AdminUsersController.Delete` deliberately carries no `[AuditEvent]` for this reason, and says so.

**Audit rows are append-only.** `AuditLogEntry` is deliberately not an `IAuditableEntity`: no `UpdatedAt`, because there is no update. There is no service method, no endpoint, and no admin route that modifies or deletes a row. Retention is the only deletion path, and it is a background job operating on the table.

---

## 5. What may be stored

The typed columns are `UserId`, `EventType`, `IpAddress`, `UserAgent`, `CorrelationId`, `OccurredAt`. None can carry a credential.

`Metadata` is free-form `jsonb`, and is the risk. Everything written to it passes through `AuditMetadataSerializer`, which walks the serialized tree and applies the rules in `SensitiveFieldNames` — the same list the Serilog destructuring policy uses, in one file so the two cannot drift:

- a field whose name contains `password`, `token`, `secret`, `hash`, `apikey`, `credential`, `cookie`, `authorization`, `recoverycode`, `privatekey` or `signature` is replaced with `[redacted]`, subtree and all;
- a field whose name contains `email` is masked to `n***@example.com`.

**The redaction is a backstop, not a licence.** Serializing a whole request object into `Metadata` and trusting the name filter is the pattern that puts a credential into durable storage under a field nobody thought to name. `AuditActionFilter` records route values and never the request body, for exactly this reason.

**The typed exception**: `IpAddress` and, where a service records it, the email address of the account, are stored in full — masked addresses would make every `login_failed` row identical and the trail useless for the question it exists to answer. That exception applies to the typed record of the event, not to free-form metadata.

---

## 6. Retention — 90 days

**Approved 2026-07-23 (P18): audit rows are deleted 90 days after `OccurredAt`.**

Ninety days covers the window an incident is realistically investigated in — a breach discovered in month two can still be traced to its origin — while bounding a table that grows with every login attempt in the system, successful or not.

`ExpiredAuthArtifactCleanupService` enforces this retention in bounded batches alongside
expired sessions, tokens and retired signing keys.

Deletion is by `OccurredAt` and nothing else — there is no "keep this row" flag and no exemption for severe events. A per-event retention table would be a policy nobody can state in one sentence, and the trail's value depends on an administrator being able to state what it holds.

---

## 7. Querying

`GET /api/v1/admin/audit-logs`, behind the `audit:read` permission. `IAuditQueryService` is the only read path; `IAuditLogger` cannot read at all.

| Parameter | Notes |
|---|---|
| `userId` | Excludes null-subject rows when set |
| `eventType` | Bound to the enum — an unknown value is a 400, not an empty page |
| `from` / `to` | Half-open: `from` inclusive, `to` exclusive, so adjacent ranges do not double-count |
| `correlationId` | Exact match; stitches to the operational log lines from the same request |
| `page` / `pageSize` | §10 caps the size |
| `sort` | `occurredAt` or `occurredAt:desc` only. Default is newest first |

Examples:

```http
### Everything that happened to one account, newest first
GET /api/v1/admin/audit-logs?userId=0191f2c4-...&pageSize=50

### Every reuse detection in a window — the query an incident starts with
GET /api/v1/admin/audit-logs?eventType=TokenReuseDetected&from=2026-07-01T00:00:00Z&to=2026-08-01T00:00:00Z

### One request, end to end: the audit rows it produced …
GET /api/v1/admin/audit-logs?correlationId=8f14e45fceea167a5a36dedd4bea2543
### … then the same value against the log platform for the lines around them.
```

Every filter above is served by an index declared in `Data/Configurations/AuditLogEntryConfiguration.cs`, each leading with its selective column and ending in `OccurredAt` so the ordering comes from the same index rather than from a sort. **Adding a filter without adding its index turns an incident query into a sequential scan of the largest table in the schema.**

---

## 8. Failure and rejection policy

`PATCH /api/v1/admin/users/{userId}` produces `admin_user_updated` after a successful 2xx
response through `AuditActionFilter`. Request bodies are never copied into audit metadata;
the actor, route and method provide the review trail without risking credential disclosure.

If the durable audit insert fails, `AuditLogger` emits a `Critical` operational event with
the event type, audit-entry id, subject, correlation id and occurrence time. It does not
throw after a possibly committed domain operation and never copies free-form audit metadata
to the fallback log.

Rate-limit rejections remain structured operational log events rather than durable audit
rows. Persisting one row per rejected request would let the flood being rejected amplify
itself into database writes. Threshold alerts aggregate the low-cardinality rejection signal
instead.
