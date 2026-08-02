# Future Work

These items are deliberately outside v1. Each re-enters technology consultation when its
trigger occurs; this page records enough intent to avoid rediscovering the first security
questions under delivery pressure.

## Organizations and multi-tenancy

**Trigger:** the product needs one user to act in more than one customer/security boundary.
Add organizations, memberships, invitations and organization-scoped role assignments. Every
query, unique key, audit event and authorization requirement must carry an organization
context, with tests for cross-tenant object references. Decide tenant isolation (row,
schema or database) before entities land; retrofitting it after unscoped data exists is unsafe.

## Machine-to-machine client credentials

**Trigger:** a non-human service needs unattended API access. Add a client registry, hashed
client secrets or asymmetric client assertions, explicit scopes and the `client_credentials`
grant. Service tokens require a separate audience, lifetime and revocation/audit policy and
must never be accepted by user/session endpoints or satisfy recent-human-authentication.

## Database-driven permissions

**Trigger:** roles or permissions must be edited without a deployment. Replace the static map
with versioned database assignments and an invalidation strategy. Preserve deny-by-default,
validate unknown permissions, audit every mutation and define how already-issued JWT role
claims converge after a change.

## SPA-driven PKCE social flow

**Trigger:** a browser SPA must own redirect initiation. Add authorization-code + PKCE state
with one-time verifier storage, exact redirect allowlists, nonce/state binding and short
expiry. Never return provider tokens to the SPA; the API still exchanges and validates them.

## Idempotency keys

**Trigger:** the first billing-like or externally side-effecting write. Persist a hash of
caller, route, key and request body beside the result, with atomic first-writer semantics and
expiry. Reuse with a different body is a conflict. Bound key length/cardinality and never use
the key as authentication.

## Automated signing-key rotation

**Trigger:** quarterly manual execution misses its SLO or multiple deployments make manual
coordination unreliable. A single-leader `BackgroundService` invokes the existing key manager,
audits the new `kid`, retires only after grace and tolerates concurrent nodes through database
constraints. Alert on no Active key, multiple failed rotations and overdue age.

## Webhooks and auth event notifications

**Trigger:** downstream systems need near-real-time security events. Use an outbox committed
with the source transaction, asynchronous delivery, signed timestamped payloads, replay
protection, per-subscriber secrets/keys, retries with dead-letter visibility and strict payload
minimization. Never send credentials or password/token material.

## SCIM provisioning

**Trigger:** enterprise identity providers must create/deactivate users and groups. Implement
SCIM 2.0 resource/version semantics, tenant-bound bearer credentials, idempotent external ids,
bulk limits and full audit coverage. Deprovisioning must revoke sessions and API keys
atomically enough to meet the agreed access-removal SLO.

## WebAuthn conditional UI

**Trigger:** a first-party browser client adopts passkey autofill. Enable discoverable
credentials and conditional mediation without weakening RP ID/origin checks, challenge
one-time use or user-verification policy. Test account-selection privacy and fallback behavior
across supported browsers.

## Additional candidates

- Stable fine-grained Azure Managed Redis ACLs when the custom-access-string API leaves preview.
- Regional/multi-primary data design only after measured availability requirements justify
  its consistency and key-management cost.

Backlog grooming records promote/drop decisions in an issue or ADR; this file is not approval
to implement a new dependency or protocol.
