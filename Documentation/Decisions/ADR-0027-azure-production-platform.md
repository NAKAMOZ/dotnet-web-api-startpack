# ADR-0027: Azure Container Apps production platform and Key Vault secret boundary

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** Project owner, through the explicit implementation directive
- **Source:** Resolves **P7** and **P14**
- **Affects:** §16, §25, §26, §27, §29

## Context

The target-agnostic image, migration and health contracts were complete, but no platform
owned networking, secrets, rollout or runtime identities. A production design also needs
separate DDL and DML credentials and must keep Data Protection keys decryptable across
container replacement without storing a wrapping secret in configuration.

## Decision

Deploy to Azure Container Apps from Bicep. Azure Database for PostgreSQL Flexible Server is
private-networked; a one-shot Container Apps job applies EF migrations and idempotently
provisions a least-privilege runtime role before the app revision is health-gated. Azure
Container Registry tags builds by source commit SHA; Container Apps receives the resolved
immutable digest reference.

Use one user-assigned managed identity for ACR pull, Key Vault secret reads, Data Protection
key wrap/unwrap and Azure Managed Redis authentication. Key Vault stores database/SMTP
secrets and an RSA wrapping key; public access is disabled and a private endpoint/private DNS
path serves the Container Apps VNet. The Data Protection ring itself remains in PostgreSQL
and is wrapped with an explicitly constructed versionless Key Vault key identifier.

GitHub Actions authenticates to Azure with OIDC. Staging deploys from `main`; production is
manual and uses GitHub Environment approval. The API runtime receives only DML credentials.

## Alternatives considered

- Kubernetes: rejected for this workload because cluster operation adds no application
  capability and Container Apps already supplies revisions, probes, jobs and autoscaling.
- A VM or Docker host: rejected because identity, secret rotation, health-gated revisions
  and private service networking would become custom operations work.
- App Service: viable, but Container Apps jobs make migration ownership explicit and the
  container/revision model matches the existing artifact more directly.
- Long-lived Azure service-principal secrets: rejected in favor of OIDC and managed identity.

## Consequences

- `infra/azure/` and `.github/workflows/deploy.yml` are the deploy definition of record.
- Actual subscription deployment, environment reviewers, DNS and runbook rehearsal remain
  release evidence; a successful Bicep compile is not a claim that Azure accepted a rollout.
- The generated Container Apps hostname is the initial RP ID/issuer. A custom domain needs a
  separately reviewed certificate and binding change; setting a hostname string alone is not
  treated as a deployment.
- Key Vault and PostgreSQL availability are part of the authentication availability boundary.
