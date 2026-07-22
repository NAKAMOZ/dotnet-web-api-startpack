# 5. Authorization and Permissions

## Objective

Policy-based authorization with role→permission mapping, enforceable per endpoint with one attribute, plus resource-ownership checks.

## Scope

Roles, permission constants, policy provider, authorization handlers, `[RequirePermission]` attribute, recent-auth (step-up) requirement.

## Architectural Decisions

- Two seeded roles: `Admin`, `User`. Permissions are string constants (`users:read:any`, `users:write:any`, `audit:read`, `sessions:revoke:any`, …) in `Handlers/Authorization/Permissions.cs`; a static `RolePermissionMap` assigns them. DB-driven permissions deferred (§29).
- `[RequirePermission("users:read:any")]` attribute (in `Attributes/`) → dynamic policy via `PermissionPolicyProvider` → `PermissionAuthorizationHandler` checks the `roles` claim against the map. No magic strings at call sites — constants only.
- Resource ownership (`/users/me`, own sessions, own passkeys/keys) enforced by route design (`me` routes resolve from `sub` claim) — no IDOR surface because own-resource routes never take a user id.
- **Step-up/recent-auth**: destructive self-service actions (disable MFA, delete account, regenerate recovery codes) require an access token whose session authenticated `< 10 min` ago or a fresh password confirmation; implemented as `RecentAuthRequirement` + handler reading `auth_time`-equivalent from the session.
- Scheme composition: a policy scheme (`"Smart"`) selects JwtBearer (bearer header or access cookie) vs `ApiKey` scheme per request; authorization policies are scheme-agnostic.

## Technology Decisions Requiring Approval

None — uses built-in ASP.NET Core authorization.

## Tasks

- [ ] `Handlers/Authorization/Permissions.cs` (constants), `RolePermissionMap.cs`.
- [ ] `Attributes/RequirePermissionAttribute.cs`.
- [ ] `Handlers/Authorization/PermissionPolicyProvider.cs`, `PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`.
- [ ] `Handlers/Authorization/RecentAuthRequirement.cs` + handler.
- [ ] `Extensions/ServiceCollectionExtensions.Authorization.cs` wiring provider, handlers, fallback policy = authenticated (anonymous endpoints opt out explicitly with `[AllowAnonymous]`).
- [ ] `Documentation/Architecture/Authorization.md`: permission catalog table, role map, step-up rules.

## Expected Deliverables

Files above; every §11 controller action carries `[RequirePermission]` or `[AllowAnonymous]` — no implicit access.

## Dependencies

§4 (claims shape). Blocks §11.

## Security Considerations

Deny-by-default fallback policy means a forgotten attribute fails closed, not open. API-key scopes intersect with the owning user's permissions — a key can never exceed its creator's rights.

## Testing Requirements

Unit: policy provider resolves constants; handler grants/denies per map; recent-auth boundary cases. Integration (§21): 403 matrix per role.

## Documentation Requirements

Permission catalog in `Documentation/Architecture/Authorization.md`; each endpoint doc (§19) states its required permission.

## Definition of Done

Fallback policy active; all endpoint inventory rows mapped to a permission or `[AllowAnonymous]`; matrix test green.

## Questions for the Project Owner

1. Is the 10-minute recent-auth window acceptable for step-up actions?
