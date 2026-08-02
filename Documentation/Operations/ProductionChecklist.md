# Production Go-Live Checklist

**Status:** Azure target and controls implemented as code; every unchecked item needs evidence
from an actual staging/production rollout attached to the release record.

## Release identity and change control

- [ ] Record the immutable image digest, source commit, migration-job execution id and
  approving change ticket. If the portable bundle is retained as release evidence, record
  its checksum too.
  Verify: `docker image inspect <image> --format '{{index .RepoDigests 0}}'`, the deployment
  workflow output, and optionally `sha256sum efbundle`.
- [ ] Confirm the release contains only additive/expand-contract migrations.
  Verify: review `dotnet ef migrations script <current> <target>` under the
  [migration policy](Migrations.md#9-expand-contract-policy).
- [ ] Confirm the previous image digest is still deployable before cutover.
- [ ] Confirm the exact built image digest passed the digest-pinned Trivy gate and its registry
  manifest carries SBOM and max-level provenance attestations.
  Verify: the deploy workflow scan output and `docker buildx imagetools inspect <image>`.

## Edge, TLS and public surface

- [ ] Terminate TLS 1.2+ at the selected edge; redirect HTTP to HTTPS.
  Verify: `curl -I http://<public-host>/health/live` returns a redirect and
  `curl -I https://<public-host>/health/live` succeeds.
- [ ] Verify HSTS on an HTTPS application response.
  Verify: `curl -sSI https://<public-host>/health/live | grep -i strict-transport-security`.
- [ ] Set `ReverseProxy__Enabled=true` and configure only the immediate trusted proxy IPs or
  CIDRs. Never enable `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, which clears the trust lists.
  Verify: startup succeeds with the real allowlist; the `ForwardedHeadersTests` suite passes;
  a direct request carrying forged `X-Forwarded-For` does not change the recorded client IP.
- [ ] Set the final exact CORS allowlists; no wildcard, path, query or fragment.
  Verify: `ConfigurationStartupTests` and a browser preflight from every approved origin.
- [ ] Verify Scalar and OpenAPI are absent in Production.
  Verify: `OpenApiContractTests.Production_DoesNotExposeOpenApiOrScalar`.

## Application safety

- [ ] Run with `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] Prove the development seeder is inert.
  Verify: `CompositionRootTests.ApplicationStarts`, plus
  `SELECT count(*) FROM auth."Users" WHERE "Email" LIKE '%@localhost.dev';` returns `0`.
- [ ] Confirm secure cookie prefixes, paths and `Secure` enforcement.
  Verify: `AuthCookieOptionsValidator` tests and a production login response once §12 lands.
- [ ] Review rate-limit values against the latest §23 load result and real edge topology.
  Verify Redis-backed counters remain one allowance across multiple app replicas.

## Database and migrations

- [ ] Use separate credentials: a migration role with DDL and a runtime role with DML only.
  Verify as runtime:

  ```sql
  BEGIN;
  CREATE TABLE auth.__permission_probe(id integer);
  ROLLBACK;
  ```

  The statement must fail. Normal `SELECT`, `INSERT`, `UPDATE` and `DELETE` on application
  tables must succeed; the runtime role also needs sequence use and writes to
  `auth."DataProtectionKeys"`.
- [ ] Run the one-shot Azure migration job (or portable EF bundle off Azure) with the
  migration role before promoting the new image.
  Verify: `SELECT * FROM auth."__EFMigrationsHistory" ORDER BY "MigrationId";`.
- [ ] Confirm `/health/ready` is `200 Healthy` only after the migration job completes.
- [ ] Test a PostgreSQL backup and restore into an isolated database, then run
  `/health/ready` and the §21 suite against the restore. Record recovery time and point.
- [ ] Confirm backup access and retention cover `SigningKeys` and `DataProtectionKeys`, and
  separately verify Key Vault key recovery because database backups contain only wrapped
  ring material.

## Keys and secrets

- [ ] Inject `ConnectionStrings__Postgres` and provider secrets through the platform secret
  channel; no secret appears in manifests, image history or workflow logs.
- [ ] Confirm one active ES256 signing key and a writable Data Protection table:

  ```sql
  SELECT "KeyId", "Status", "ActivatedAt" FROM auth."SigningKeys";
  SELECT count(*) FROM auth."DataProtectionKeys";
  ```

- [ ] Confirm the PostgreSQL-persisted Data Protection ring is wrapped by the versionless
  Key Vault RSA key and survives an API revision replacement.
- [ ] Rehearse [mass revocation](Runbooks/MassRevocation.md) and
  [key compromise](Runbooks/KeyCompromise.md) against staging.

## Logs, traces, metrics and alerts

- [ ] Name the structured-log destination and retention policy.
- [ ] Confirm `Telemetry__AzureMonitorExporterEnabled=true` and the Key Vault-sourced
  Application Insights connection; enable optional OTLP only when a second backend is owned.
- [ ] Confirm traces carry `service.name`, version, environment and `app.correlation_id`.
- [ ] Confirm every metric in [Monitoring.md](Monitoring.md) is visible and all five alert
  families route to an owned on-call destination.
- [ ] Confirm anonymous health responses contain only `Healthy` or `Unhealthy`.

## Deploy and rollback

1. Freeze schema-changing writes if the reviewed migration requires it.
2. Take/verify the required backup.
3. Run the one-shot migration job with the migration role; keep the current API image active.
4. Deploy the immutable image digest with the runtime role.
5. Wait for `/health/ready`; exercise login/refresh.
6. Shift traffic gradually and watch readiness, 5xx, latency and auth alerts.
7. Roll back by restoring the previous image digest. Do not run destructive `Down()`
   migrations during an incident; additive migrations remain compatible by policy.

The concrete commands and evidence list live in [AzureDeployment.md](AzureDeployment.md).
This checklist becomes signable only after the actual environment supplies that evidence.
