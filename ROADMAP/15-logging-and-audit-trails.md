# 15. Logging and Audit Trails

## Objective

Structured operational logging (Serilog) plus a tamper-evident security audit trail (DB) — two distinct systems with distinct audiences.

## Scope

Serilog configuration, enrichment, redaction rules, `IAuditLogger` + event catalog, retention.

## Architectural Decisions

- Two-stage Serilog init (bootstrap logger before host build); config from `appsettings.json` (`Serilog` section); console sink (JSON in non-dev, readable in dev).
- Enrichers in `Logging/`: `CorrelationIdEnricher`, `UserIdEnricher` (from claims when authenticated); machine/environment enrichment standard.
- **Redaction policy (hard rules)**: never log passwords, tokens (access, refresh, reset, MFA, API keys), TOTP secrets, `Authorization` headers, cookie values. Emails logged masked (`n***@example.com`) except inside the audit table itself. Enforced by destructuring policy + review checklist.
- Audit events (DB via `IAuditLogger`, not just log lines — queryable, survives log rotation): `user_registered`, `login_succeeded`, `login_failed`, `mfa_challenge_issued`, `mfa_failed`, `account_locked`, `token_refreshed`, `token_reuse_detected`, `session_revoked`, `password_changed`, `password_reset_requested`, `password_reset_completed`, `email_verified`, `mfa_enrolled`, `mfa_disabled`, `passkey_registered`, `passkey_removed`, `api_key_created`, `api_key_revoked`, `role_granted`, `role_revoked`, `admin_user_deleted`, `signing_key_rotated`.
- Retention: **90 days** — P18 approved 2026-07-23. Deletion job is §12's cleanup worker.

## Technology Decisions Requiring Approval

~~P18~~ — resolved 2026-07-23: 90-day retention, then delete.

## Tasks

- [x] `Logging/SerilogSetup.cs` (bootstrap + host wiring), `CorrelationIdEnricher.cs`, `UserIdEnricher.cs`, `SensitiveDataDestructuringPolicy.cs` — plus `SensitiveFieldNames.cs`, the one list both the log policy and the audit metadata serializer read.
- [x] `Services/Audit/IAuditLogger.cs` + `AuditLogger.cs`; `Models/Enums/AuditEventType.cs` covering the catalog above (the enum landed with the data layer in §6).
- [ ] Wire audit calls into every §12 service path listed in the event catalog (checklist-driven) — **blocked**: 20 of the 23 events have no producer because their services do not exist. `role_granted`, `role_revoked` and the admin `session_revoked` are wired via `AuditActionFilter`.
- [x] `GET /api/v1/admin/audit-logs` filtering: by user, event type, date range, correlation ID — `IAuditQueryService` + `Mappings/AuditMappingExtensions.cs`; the second live endpoint in the project after JWKS.
- [x] `Documentation/Architecture/AuditTrail.md`: event catalog, retention, query examples.
- [ ] Retention job — **blocked** on §12's cleanup background worker. The period is decided; nothing deletes yet.

### Recorded deviations

- **No `Serilog.Enrichers.Environment`.** The two properties it supplies (`MachineName`, `EnvironmentName`) are set with `Enrich.WithProperty`, which needs no package and therefore no ADR.
- **`CreateLogger()`, not `CreateBootstrapLogger()`, paired with `preserveStaticLogger: true`.** The reloadable-logger upgrade path assumes one host per process; xUnit builds several in parallel and the second freeze throws `"The logger is already frozen."`. The two-stage benefit is unchanged — the bootstrap logger still covers everything before `Build()`.
- **Correlation id reaches log events through an enricher, not a `LogContext` push in §14's middleware.** Its sibling `UserIdEnricher` cannot be a push (the user id does not exist five stages above authentication), and one pushed property beside one enriched property reads like a defect.
- **`Logging/SensitiveFieldNames.cs` is shared with the audit path.** The audit table's `Metadata` column is durable, exempt from log rotation and readable over HTTP — two copies of the never-logged list would agree until one was extended.
- **`admin_user_deleted` is not wired through `AuditActionFilter`.** `AuditLogEntry.UserId` is a foreign key; the filter runs after the action, when the referenced user no longer exists, so the insert would violate the constraint. §12's deletion service records it with a null subject.
- **No `admin_user_updated` event.** `PATCH /admin/users/{userId}` produces no row: the catalog is closed and has no member for it. Flagged for the owner rather than invented — see `AuditTrail.md` §8.

## Expected Deliverables

`Logging/` (4 files), audit service, admin query endpoint live, audit doc.

## Dependencies

§12 (service call sites), §14 (correlation).

## Security Considerations

Audit rows are append-only from the app's perspective (no update/delete API); the redaction policy is tested (§22 asserts a log-capture run contains no token material after a full auth flow).

## Testing Requirements

§21: each catalog event asserted present after its triggering flow; redaction test as above.

## Documentation Requirements

AuditTrail doc; event list kept in sync with enum by a guard test.

## Definition of Done

Full login→refresh→revoke flow produces the expected audit sequence; redaction test green; retention job wired.

## Questions for the Project Owner

1. ~~Approve 90-day audit retention (P18)?~~ — approved 2026-07-23.
2. `PATCH /api/v1/admin/users/{userId}` changes an account and produces no audit row, because the catalog has no `admin_user_updated` member. Add one, or accept the gap?
3. `AuditLogger.HandleWriteFailure` is unimplemented pending a decision: when the audit row cannot be written, does the request still succeed?
