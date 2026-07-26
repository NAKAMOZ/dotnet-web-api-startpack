# Production Go-Live Checklist

**Status:** target-agnostic controls implemented; deployment-specific verification waits on
P14. Every unchecked item needs evidence attached to the release record.

## Release identity and change control

- [ ] Record the immutable image digest, source commit, migration-bundle checksum and
  approving change ticket.
  Verify: `docker image inspect <image> --format '{{index .RepoDigests 0}}'` and
  `sha256sum efbundle`.
- [ ] Confirm the release contains only additive/expand-contract migrations.
  Verify: review `dotnet ef migrations script <current> <target>` under the
  [migration policy](Migrations.md#9-expand-contract-policy).
- [ ] Confirm the previous image digest is still deployable before cutover.

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
  Record the chosen per-node capacity and whether the deployment has more than one app node.

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
- [ ] Run the migration bundle with the migration role before starting the new image.
  Verify: `SELECT * FROM auth."__EFMigrationsHistory" ORDER BY "MigrationId";`.
- [ ] Confirm `/health/ready` is `200 Healthy` only after the bundle completes.
- [ ] Test a PostgreSQL backup and restore into an isolated database, then run
  `/health/ready` and the §21 suite against the restore. Record recovery time and point.
- [ ] Confirm backup access and retention cover `SigningKeys` and `DataProtectionKeys`
  according to the same incident boundary; P7/P14 must decide whether sharing that boundary
  is acceptable.

## Keys and secrets

- [ ] Inject `ConnectionStrings__Postgres` and provider secrets through the platform secret
  channel; no secret appears in manifests, image history or workflow logs.
- [ ] Confirm one active ES256 signing key and a writable Data Protection table:

  ```sql
  SELECT "KeyId", "Status", "ActivatedAt" FROM auth."SigningKeys";
  SELECT count(*) FROM auth."DataProtectionKeys";
  ```

- [ ] Resolve P7/P14 and configure encryption for the Data Protection key ring at rest.
  Until then, record acceptance of the known ASVS gap; do not mark this checklist complete.
- [ ] Rehearse [mass revocation](Runbooks/MassRevocation.md) and
  [key compromise](Runbooks/KeyCompromise.md) against staging.

## Logs, traces, metrics and alerts

- [ ] Name the structured-log destination and retention policy.
- [ ] Set `Telemetry__OtlpExporterEnabled=true` and the approved
  `Telemetry__OtlpEndpoint`; configure exporter credentials through the secret channel.
- [ ] Confirm traces carry `service.name`, version, environment and `app.correlation_id`.
- [ ] Confirm every metric in [Monitoring.md](Monitoring.md) is visible and all five alert
  families route to an owned on-call destination.
- [ ] Confirm anonymous health responses contain only `Healthy` or `Unhealthy`.

## Deploy and rollback

1. Freeze schema-changing writes if the reviewed migration requires it.
2. Take/verify the required backup.
3. Run the migration bundle with the migration role.
4. Deploy the immutable image digest with the runtime role.
5. Wait for `/health/ready`; exercise login/refresh when §12 exists.
6. Shift traffic gradually and watch readiness, 5xx, latency and auth alerts.
7. Roll back by restoring the previous image digest. Do not run destructive `Down()`
   migrations during an incident; additive migrations remain compatible by policy.

P14 supplies the actual platform definition, rollout command, health-gate primitive and
rollback command. Until that target exists, this checklist is reviewable but not signable.
