# Runbook: Mass Session Revocation

Use when refresh tokens may be exposed, a global authentication reset is required, or an
incident commander decides every user must authenticate again. This procedure invalidates
all sessions and refresh tokens immediately; already-issued access tokens remain valid only
until their short expiry unless signing keys are also retired.

## Authority and prerequisites

- Incident commander approval and an incident identifier.
- A fresh database backup and access through the migration/incident role.
- The current image available to regenerate a signing key if access tokens must also die.
- Announce that every client will be logged out.

## Execute

Run as one transaction. Replace `INCIDENT-ID`; do not put secrets in the metadata.

```sql
BEGIN;

UPDATE auth."Users"
SET "SecurityStamp" = replace(gen_random_uuid()::text, '-', ''),
    "UpdatedAt" = now();

UPDATE auth."Sessions"
SET "RevokedAt" = COALESCE("RevokedAt", now()),
    "RevocationReason" = COALESCE("RevocationReason", 'AdminRevoked'),
    "UpdatedAt" = now()
WHERE "RevokedAt" IS NULL;

UPDATE auth."RefreshTokens"
SET "ExpiresAt" = LEAST("ExpiresAt", now()),
    "UpdatedAt" = now()
WHERE "ExpiresAt" > now();

INSERT INTO auth."AuditLogEntries"
    ("Id", "EventType", "Metadata", "OccurredAt")
VALUES
    (gen_random_uuid(), 'SessionRevoked',
     '{"scope":"global","reason":"incident","incident":"INCIDENT-ID"}'::jsonb, now());

COMMIT;
```

If the incident also requires immediate invalidation of every access token, follow the
retire-and-regenerate procedure in [KeyCompromise.md](KeyCompromise.md) with scope `all`.
Normal mass logout does not rotate keys: revoking a refresh credential is not evidence that
the signing key was exposed.

## Verify

```sql
SELECT count(*) AS live_sessions
FROM auth."Sessions"
WHERE "RevokedAt" IS NULL AND "AbsoluteExpiresAt" > now();

SELECT count(*) AS live_refresh_tokens
FROM auth."RefreshTokens"
WHERE "UsedAt" IS NULL AND "ExpiresAt" > now();
```

Both counts must be zero. Then:

- `/health/ready` remains `200 Healthy`;
- a known pre-incident refresh token is rejected;
- a new login can create a new session once §12 is implemented;
- `auth.active_sessions` converges to zero within one sample interval (one minute);
- the incident audit row and database command log are preserved.

## Recovery and communication

Do not reverse revocation fields or restore old security stamps. Recovery is re-authentication.
If the SQL transaction fails, roll it back and diagnose before retrying. Record start/end
times, affected user count, verification evidence and whether signing keys were retired.
