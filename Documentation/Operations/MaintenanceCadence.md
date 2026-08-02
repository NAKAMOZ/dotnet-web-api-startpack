# Maintenance Cadence

Every recurring action produces evidence: a change/review link, before/after versions or
key ids, validation output, and the owner who accepted the result. A calendar reminder
without evidence is not a control.

## Schedule

| Cadence | Action | Evidence and gate |
|---|---|---|
| Continuous | CI build, format, tests, coverage, dependency/secret/application-image scans and image smoke | Required green workflow |
| Weekly | Review Dependabot PRs and security advisories; triage failed auth/security alerts | Merged/closed PRs and incident links |
| Monthly | Apply supported .NET SDK/runtime patch; review base images and collector; restore the latest PostgreSQL backup into isolation | Version diff, full suite, restore record |
| Quarterly | Gracefully rotate the ES256 signing key; review CORS, proxy allowlist, rate limits, alert routing and access lists | Old/new `kid`, JWKS check, owner review |
| Semi-annually | Rehearse mass revocation and key compromise in staging; run ASVS delta review | Timed runbook record and checklist diff |
| Per release | Review expand-contract compatibility, configuration diff and production checklist | Signed release record |
| Annually | Plan major .NET/PostgreSQL/OpenTelemetry upgrades and v1 deprecation windows | Approved upgrade proposal/ADR |

Dependabot is the weekly dependency mechanism. .NET minor/patch upgrades are reviewed
monthly; major runtime, PostgreSQL and authentication-library upgrades are planned changes,
never unattended automation.

## Healthy signing-key rotation

This is not the compromise path. The old key must remain published for at least
`Jwt:KeyRetirementGrace`.

```bash
dotnet dotnet-web-api-startpack.dll operations rotate-signing-key
```

In a container, run the same immutable image with its normal database and Data Protection
configuration, replacing the server command with `operations rotate-signing-key`. Record the
new `kid`, confirm JWKS contains the new Active and previous Retiring keys, and confirm newly
issued tokens use the new key.

After the grace period:

```bash
dotnet dotnet-web-api-startpack.dll operations retire-signing-keys
```

Confirm the old `kid` is absent from JWKS. Rotate immediately through the
[compromise runbook](Runbooks/KeyCompromise.md) instead when confidentiality is uncertain.
Automating this command is a future item; v1 keeps an operator and review record in the loop.

## Argon2id parameter upgrade

1. Run the tuning harness and k6 login workload on production-equivalent hardware.
2. Choose parameters that meet the security floor without exceeding the concurrency budget.
3. Change `PasswordHashing` configuration; do not rewrite stored hashes offline.
4. Deploy. `NeedsRehash` identifies weaker encoded parameters and the login service re-hashes
   after a successful password verification when §12 lands.
5. Watch `auth.password_hash_duration` p50/p95/p99, CPU, allocation, login latency and errors.
6. Measure migration progress with a metadata-only query/parser that compares encoded
   `m`, `t` and `p`; never export hashes or put them in telemetry.
7. Retire the old minimum only after the agreed active-user coverage/window.

Rollback means restoring the old configured cost. New stronger hashes still verify because
their parameters are self-describing.

## Upgrade procedure

For every dependency/runtime upgrade:

1. Read upstream security, breaking-change and telemetry semantic-convention notes.
2. Update the central exact pin and, when architecture changes, add/supersede an ADR.
3. Restore with audit enabled; run build, all tests, coverage, image and Compose smoke.
4. For EF/Npgsql, generate and review migration SQL even when no model change is expected.
5. For OpenTelemetry/Npgsql, compare emitted names/tags against `Monitoring.md` and dashboard
   queries.
6. Deploy through staging, observe one full alert evaluation window, then promote.

## API evolution and deprecation

v1 changes are additive. Removing/renaming a route, field, enum value, authentication scheme
or semantic behavior is a breaking change and requires `/api/v2`.

- Publish migration notes in `Documentation/` and Scalar/OpenAPI.
- Add `Deprecation: true`, a standards-formatted `Sunset` date and a documentation `Link`
  header on deprecated v1 responses.
- Keep v1 and v2 live together for at least **180 days** and at least two normal client
  release cycles; a longer contractual window wins.
- Track traffic by version without user-identifying metric labels.
- Remove v1 only after usage is zero for the agreed window, the owner approves, and rollback
  artifacts remain available.

Emergency removal of a vulnerable behavior follows incident governance and documents why the
normal overlap could not be honored.
