# Runbook: Signing-Key Compromise

Use when an ES256 private signing key may be known outside the service boundary. Retirement
is immediate: the normal grace period is for healthy rotation and is unsafe for a compromised
key.

## 1. Contain and identify

Record the incident id and compromised `kid`. Restrict access to the database, secret channel
and deployment control plane. Query without selecting private material:

```sql
SELECT "KeyId", "Status", "ActivatedAt", "RetiringAt", "RetiredAt"
FROM auth."SigningKeys"
ORDER BY "ActivatedAt" DESC;
```

Never copy `PrivateKeyProtected` into the ticket or logs.

## 2. Retire immediately

For one known key, replace `COMPROMISED-KID`. For uncertainty about the whole ring, omit the
`KeyId` predicate and retire every Active/Retiring row.

```sql
BEGIN;

UPDATE auth."SigningKeys"
SET "Status" = 'Retired',
    "RetiredAt" = now(),
    "UpdatedAt" = now()
WHERE "KeyId" = 'COMPROMISED-KID'
  AND "Status" IN ('Active', 'Retiring');

COMMIT;
```

If the retired key was Active, the ring temporarily has no active key. Trigger generation
through any healthy instance:

```bash
curl --fail --silent https://<public-host>/.well-known/jwks.json
```

`GetOrCreateActiveKeyAsync` creates exactly one replacement under the database uniqueness
constraint. If a healthy Active key already existed, no replacement is needed.

## 3. Verify

```bash
curl --fail --silent https://<public-host>/.well-known/jwks.json
```

Confirm the compromised `kid` is absent and one new/current key is present. In SQL, confirm
exactly one Active row:

```sql
SELECT "Status", count(*) FROM auth."SigningKeys" GROUP BY "Status";
```

Validate that a token signed by the compromised key now fails and a newly issued token
validates. Monitor `auth.logins`, `auth.refreshes`, 401s and support traffic.

## 4. Expand the response when needed

- If refresh tokens or the user database may also be exposed, run
  [MassRevocation.md](MassRevocation.md).
- If the Data Protection key ring or full database was exposed, assume the protected private
  key envelopes can be unwrapped. Rotate platform credentials and the Data Protection
  protector selected by P7/P14 before issuing replacement keys. Do **not** simply delete
  `DataProtectionKeys`: that orphans every protected payload.
- If only healthy quarterly rotation is due, use
  `dotnet dotnet-web-api-startpack.dll operations rotate-signing-key`; the previous key stays
  Retiring for the configured grace period. Later run `operations retire-signing-keys`.

Record the old/new kids, exact retirement time, affected token window, root cause and every
credential boundary rotated. Never record key material.
