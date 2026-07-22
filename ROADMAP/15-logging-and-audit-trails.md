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
- Retention: P18 (recommend 90 days → cleanup job §12).

## Technology Decisions Requiring Approval

P18.

## Tasks

- [ ] `Logging/SerilogSetup.cs` (bootstrap + host wiring), `CorrelationIdEnricher.cs`, `UserIdEnricher.cs`, `SensitiveDataDestructuringPolicy.cs`.
- [ ] `Services/Audit/IAuditLogger.cs` + `AuditLogger.cs`; `Models/Enums/AuditEventType.cs` covering the catalog above.
- [ ] Wire audit calls into every §12 service path listed in the event catalog (checklist-driven).
- [ ] `GET /api/v1/admin/audit-logs` filtering: by user, event type, date range, correlation ID.
- [ ] `Documentation/Architecture/AuditTrail.md`: event catalog, retention, query examples.

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

1. Approve 90-day audit retention (P18)?
