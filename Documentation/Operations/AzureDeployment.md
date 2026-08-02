# Azure Deployment Runbook

The production target is Azure Container Apps (ADR-0027). `infra/azure/foundation.bicep`
creates ACR first; `infra/azure/main.bicep` owns the application environment, network,
PostgreSQL, Azure Managed Redis, Key Vault, Application Insights, identities, API and
migration job.

## Topology and trust boundaries

- Container Apps terminates TLS and runs immutable image-digest references whose ACR tags
  record the source commit SHA.
- PostgreSQL Flexible Server has no public network access and uses a delegated subnet.
- Azure Managed Redis disables access keys and public access. The app connects over a
  private endpoint with its user-assigned managed identity.
- Key Vault disables public access and resolves through a private endpoint/private DNS zone.
  It uses RBAC, soft delete and purge protection. The app identity can read secrets and
  wrap/unwrap Data Protection keys; it cannot administer the vault.
- The migration job receives the PostgreSQL administrator connection. API replicas receive
  only the DML runtime connection created by that job.
- Log Analytics receives Container Apps logs; workspace-based Application Insights receives
  OpenTelemetry traces and metrics.

## GitHub Environment configuration

Create `staging` and `production` GitHub Environments. Require reviewers on `production`.
Both environments need these secrets:

| Secret | Purpose |
|---|---|
| `AZURE_CLIENT_ID` | federated deployment identity application/client id |
| `AZURE_TENANT_ID` | Entra tenant |
| `AZURE_SUBSCRIPTION_ID` | target subscription |
| `POSTGRES_ADMIN_PASSWORD` | deployment-only database administrator password |
| `POSTGRES_RUNTIME_PASSWORD` | DML runtime role password |
| `SMTP_PASSWORD` | SMTP credential |

Staging performance additionally needs `LOAD_TEST_EMAIL` and `LOAD_TEST_PASSWORD` for a
dedicated verified, non-admin test account.

Environment variables:

| Variable | Required/default |
|---|---|
| `WORKLOAD_NAME` | optional, `startpack` |
| `AZURE_LOCATION` | optional, `westeurope` |
| `SMTP_HOST` | required |
| `SMTP_PORT` | optional, `587` |
| `SMTP_FROM_ADDRESS` | required |
| `SMTP_USERNAME` | required |
| `POSTGRES_HIGH_AVAILABILITY` | `Disabled` or `ZoneRedundant` |
| `API_CPU` / `API_MEMORY` | optional, `2.0` / `4Gi` |
| `API_MIN_REPLICAS` | optional, staging `1`, production `5` |
| `API_MAX_REPLICAS` | optional, `10` |

The GitHub OIDC subject must be restricted to this repository and the relevant Environment.
Grant the deployment identity the narrowest resource-group/subscription permissions that can
create the template's resources and role assignments; it needs no stored client secret.

## Deployment sequence

A successful CI workflow for a trusted push to `main` deploys staging only while that source
SHA is still the current `main` head. This stale-run guard executes before Azure OIDC login.
Production is a manual workflow dispatch:

1. OIDC login and resource-group creation/update.
2. Idempotent ACR foundation deployment.
3. Build and push the commit-SHA-tagged image with SBOM and max-level provenance, resolve its
   registry digest, then reject it if the digest-pinned Trivy scan finds any HIGH/CRITICAL
   vulnerability, including one without a current fix. Every later step uses the digest reference.
4. Read the currently active API image, then apply the main Bicep template while keeping that
   image on the API. The new digest reference is assigned only to the migration job. On the
   first deployment, when no API image exists yet, both necessarily start from the new image
   but no prior traffic exists to cut over.
5. Start the manual migration job and poll its named execution to `Succeeded`.
6. Promote the new digest reference to the API only after migration succeeds.
7. Gate on `/health/ready`, which includes PostgreSQL/migrations and Redis. A failed gate
   automatically restores the previous API image when one exists; additive schema changes
   remain in place by design.
8. On staging, scan `/openapi/v1.json` with ZAP.

The Bicep files are compiler-validated in local/release checks. A compile proves template
shape, not subscription quotas, region availability, DNS or RBAC propagation; the first
staging deployment is the platform acceptance test.

## Scale and Argon2 capacity

Each default API replica has 2 vCPU and 4 GiB. HTTP concurrency target `2` drives scale to a
maximum of ten replicas. Production keeps five warm by default because password verification
is deliberately CPU/memory-hard and the initial budget is 50 login RPS at p95 below 500 ms.
Do not reduce Argon2 parameters to compensate for undersized infrastructure.

Redis makes rate-limit counters and HybridCache L2 consistent across replicas. It is a
readiness dependency and has high availability enabled in production. Staging disables Redis
HA to reduce non-production cost; that difference must not be used for availability claims.

Run `.github/workflows/performance.yml` after staging provisioning and before release. It
temporarily raises single-source test limits, warms replicas, runs all k6 profiles and restores
normal limits even after a failed threshold.

## Rollback

Container images are deployed by immutable digest. To roll back, dispatch/deploy the last
known-good source SHA and retain its resulting digest while leaving additive migrations in
place. Do not execute destructive `Down()` migrations
during an incident.

If the migration job fails, the workflow never promotes the new API image. If readiness fails
after promotion, the workflow restores the previously active image when one exists; inspect
Container Apps revision logs, Redis private DNS/access assignment, Key Vault RBAC and
PostgreSQL migration history before retrying. This application rollback is safe only because
release migrations follow the expand-contract policy and remain compatible with the prior
image. Follow the mass-revocation or key-compromise runbook only when credential integrity—not
deployment health—is in question.

## First-deployment evidence

Record these outside the repository's static template:

- Bicep deployment names and successful outputs;
- image digest and source commit;
- migration job execution id and migration history;
- runtime-role DML success plus DDL denial;
- Key Vault wrap/unwrap and secret-reference success;
- Redis and Key Vault private DNS resolution, Entra connection and readiness/failure
  recovery rehearsal;
- staging ZAP and k6 reports;
- production Environment approver, dashboard/alert owner and backup restore RPO/RTO.
