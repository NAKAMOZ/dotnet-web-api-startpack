# v1 Scope

**Status:** Approved 2026-07-22 · **Source:** `ROADMAP/00-overview.md`, approved-decisions row *v1 feature scope*

This document states what v1 delivers and — as importantly — what it does not. An item's absence from the in-scope list is a decision, not an oversight; deferred capabilities are listed below with a pointer to where their design work happens.

## What this is

A **headless authentication and authorization REST API**, architecturally inspired by Better Auth. No user interface ships with it, and no Better Auth source code is copied — the inspiration is schema and feature shape only.

## In scope for v1

Each capability lists the endpoints that implement it. Full route inventory with auth requirements: [`ROADMAP/00-overview.md`](../ROADMAP/00-overview.md).

### Email and password authentication
Registration, login, logout, and token refresh with rotation and reuse detection.
`POST /api/v1/auth/register` · `/auth/login` · `/auth/refresh` · `/auth/logout` · `GET /auth/csrf`

### Sessions
One session per login per device, with device metadata. Users can see where they are logged in and revoke individually or in bulk. Sliding 6-hour inactivity window plus a 7-day absolute cap ([ADR-0002](Decisions/ADR-0002-session-lifetime-and-model.md)).
`GET /api/v1/sessions` · `DELETE /sessions/{sessionId}` · `DELETE /sessions`

### Roles and authorization
Role assignment with policy-based authorization. **Permissions are code constants** mapped to roles in a static policy map — not database rows. This keeps the v1 schema lean; runtime-editable permissions are deferred (see below).
`POST /api/v1/admin/users/{userId}/roles` · `DELETE /admin/users/{userId}/roles/{roleId}`

### Email verification
`POST /api/v1/email-verification/send` · `/email-verification/confirm`

### Password reset
Request and confirm flows. A completed reset bumps `SecurityStamp` and revokes every session.
`POST /api/v1/password-reset/request` · `/password-reset/confirm`

### Multi-factor authentication (TOTP)
Enrollment, confirmation, disable, and single-use recovery codes. Login with MFA enrolled returns a short-lived MFA ticket rather than tokens.
`POST /api/v1/mfa/totp/enroll` · `/mfa/totp/confirm` · `DELETE /mfa/totp` · `POST /mfa/recovery-codes/regenerate` · `POST /auth/login/mfa`

### Social login
**Google and GitHub** at launch (P12). API-driven redirect flow (P13); the SPA-driven PKCE variant is deferred.
`GET /api/v1/auth/social/{provider}/authorize` · `/auth/social/{provider}/callback` · `GET /users/me/accounts` · `DELETE /users/me/accounts/{accountId}`

### Passkeys (WebAuthn / FIDO2)
Registration and authentication ceremonies, credential listing and removal.
`POST /api/v1/passkeys/registration/options` · `/registration/complete` · `/authentication/options` · `/authentication/complete` · `GET /passkeys` · `DELETE /passkeys/{credentialId}`

### API keys / personal access tokens
Scoped programmatic credentials with prefix-based lookup, authenticated by a dedicated scheme.
`POST /api/v1/api-keys` · `GET /api-keys` · `DELETE /api-keys/{keyId}`

### User self-service
Profile read and update, account deletion, password change (revokes all sessions), linked-account management.
`GET /api/v1/users/me` · `PATCH /users/me` · `DELETE /users/me` · `PUT /users/me/password`

### Administration
Paged and filterable user management, forced session revocation, and audit-log access.
`GET /api/v1/admin/users` · `/admin/users/{userId}` · `PATCH`/`DELETE` on the same · `DELETE /admin/users/{userId}/sessions` · `GET /admin/audit-logs`

### Supporting infrastructure
JWKS publication (`GET /.well-known/jwks.json`), liveness and readiness probes (`/health/live`, `/health/ready`), audit trail, structured logging, rate limiting, and RFC 9457 error responses throughout.

## Explicitly out of scope for v1

Every item below has a recorded reason. Design sketches and trigger conditions live in `Documentation/FutureWork.md`, produced by workstream §29.

| Deferred | Why |
|---|---|
| **Organizations / multi-tenancy** | Owner-excluded from v1. Would add org and membership entities, org-scoped roles, and an invitation flow — a second authorization dimension layered on top of one that does not exist yet. |
| **Machine-to-machine client credentials** | Owner-excluded from v1. Needs a client registry, the `client_credentials` grant, and a separate token audience. No consumer requires it today. |
| **Message broker / async messaging** (P11) | No cross-service asynchronous communication exists in this system. Adding a broker would be speculative infrastructure. |
| **Database-driven permissions** | v1 maps code constants to roles statically. DB-driven permissions become worthwhile only when roles need editing at runtime without a deploy. |
| **Redis scale-out** (P5, P6) | v1 uses in-memory `HybridCache` and in-memory rate-limit counters. Trigger for revisiting: a second application node, at which point both become incorrect rather than merely suboptimal. |
| **SPA-driven PKCE social flow** (P13, deferred half) | The API-driven redirect flow ships first; the PKCE variant follows if a browser client needs it. |
| **`Idempotency-Key` support** | Trigger: any endpoint where a duplicate submission has a cost — billing-like operations. None in v1. |
| **Automated signing-key rotation** | v1 rotates quarterly via a documented admin procedure ([ADR-0004](Decisions/ADR-0004-signing-key-management.md)). Automating a process that can invalidate in-flight tokens if mistimed is not the first automation to build. |
| **Webhooks / auth event notifications** | A Better Auth parity feature. Needs signed payloads, delivery retries, and an endpoint registry — a subsystem, not a feature. |
| **SCIM provisioning** | Enterprise directory sync. No consumer. |
| **WebAuthn conditional UI (passkey autofill)** | A client-side enhancement on top of shipped passkey support; noted for later. |
| **Hangfire / Quartz job scheduling** (P9) | v1's two cleanup jobs are served by plain `BackgroundService`. A scheduler is infrastructure for a problem not yet present. |

## Non-goals

- **No user interface.** No login pages, no admin console, no Identity scaffolding. Consumers build their own.
- **No copied Better Auth source.** Schema shape and feature set are the inspiration; the implementation is original.
- **Not a general-purpose identity provider.** No OIDC provider role, no SAML, no federation *outbound* — the API consumes external providers for social login but does not act as one for third parties.

## Related documents

- [`Decisions/`](Decisions/README.md) — the architectural decision records behind each choice above.
- [`../ROADMAP/00-overview.md`](../ROADMAP/00-overview.md) — entity model, full endpoint inventory, pending decisions.
- [`../ROADMAP/29-maintenance-and-future-extensibility.md`](../ROADMAP/29-maintenance-and-future-extensibility.md) — where the deferred list becomes a groomed backlog.
