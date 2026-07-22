# 5. Authorization and Permissions

## Objective

Policy-based authorization with role→permission mapping, enforceable per endpoint with one attribute, plus resource-ownership checks.

## Scope

Roles, permission constants, policy provider, authorization handlers, `[RequirePermission]` attribute, recent-auth (step-up) requirement.

## Architectural Decisions

- Two seeded roles: `Admin`, `User`. Permissions are string constants (`users:read:any`, `users:write:any`, `audit:read`, `sessions:revoke:any`, …) in `Handlers/Authorization/Permissions.cs`; a static `RolePermissionMap` assigns them. DB-driven permissions deferred (§29).
- `[RequirePermission("users:read:any")]` attribute (in `Attributes/`) → dynamic policy via `PermissionPolicyProvider` → `PermissionAuthorizationHandler` checks the `roles` claim against the map. No magic strings at call sites — constants only.
- Resource ownership (`/users/me`, own sessions, own passkeys/keys) enforced by route design (`me` routes resolve from `sub` claim) — no IDOR surface because own-resource routes never take a user id.
- **Step-up/recent-auth**: destructive self-service actions (disable MFA, delete account, regenerate recovery codes) require an access token whose session authenticated **< 5 min** ago; implemented as `RecentAuthRequirement` + handler reading the `auth_time` claim. *(The 10 minutes originally written here conflicted with §4's 5; the owner confirmed **5 minutes** on 2026-07-22.)*
- Scheme composition: a policy scheme (`"Smart"`) selects JwtBearer (bearer header or access cookie) vs `ApiKey` scheme per request; authorization policies are scheme-agnostic.

## Technology Decisions Requiring Approval

None — uses built-in ASP.NET Core authorization.

## Tasks

- [x] `Handlers/Authorization/Permissions.cs` (constants), `RolePermissionMap.cs` — plus `Roles.cs`, since role names were equally magic-string-prone.
- [x] `Attributes/RequirePermissionAttribute.cs` — plus `RequireRecentAuthAttribute.cs`.
- [x] `Handlers/Authorization/PermissionPolicyProvider.cs`, `PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`.
- [x] `Handlers/Authorization/RecentAuthRequirement.cs` + `RecentAuthAuthorizationHandler.cs`.
- [x] `Extensions/ServiceCollectionExtensions.Authorization.cs` wiring provider and handlers. **Fallback policy is defined and tested but not yet assigned** — see Definition of Done.
- [x] `Documentation/Architecture/Authorization.md`: permission catalog table, role map, step-up rules.
- [x] Unit tests: 20 across the map, policy provider, and step-up handler.

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

- [x] Permission catalog, role map, attributes, policy provider and both handlers written; build clean, 21 tests green.
- [x] Every 👑 row in the endpoint inventory has a named permission (`Documentation/Architecture/Authorization.md` §3).
- [ ] **Fallback policy *active*** — blocked on §12. Defined as `AuthorizationPolicies.DenyByDefault` and unit-tested; assignment is one marked line in `ServiceCollectionExtensions.Authorization.cs`.
- [ ] **All inventory rows carry an attribute** — blocked on §11; controllers do not exist yet.
- [ ] **Matrix test green** — blocked on §11 and §21.

### Discovered dependency: §5 → §12 (not in the original Dependencies)

Deny-by-default cannot be switched on until an authentication scheme exists.

`AddAuthorization` causes minimal hosting to insert the authorization middleware automatically, and that middleware applies the fallback policy to **every** request, including ones matching no endpoint. With no scheme registered there is nothing to challenge with, so setting `FallbackPolicy` today turns every request — 404s included — into a 500. The §3 composition-root smoke test caught it immediately.

The policy is therefore defined and verified in isolation, with activation deferred to §12 and marked at the exact line.

### Also landed (not in the original list)

- `Roles.cs` — role-name constants. `RolePermissionMap` keyed on literals would have the same typo problem the permission constants exist to prevent.
- `AuthorizationPolicies.cs` — so the deny-by-default policy is a named, testable value rather than an inline builder call that can only be exercised through the pipeline.
- **Startup validation of the catalog against the map.** A permission granted to no role, or a map entry naming a deleted constant, throws before the host starts. Both fail silently otherwise.
- `TimeProvider` registered in `AddApiServices`. ADR-0011 mandates injecting it, but nothing had registered it — the step-up handler was the first consumer, and the smoke test surfaced the gap.
- `AuthSessionOptions` bound to configuration with `ValidateDataAnnotations().ValidateOnStart()`. Strictly §25's job, but an unbound options class silently serves defaults and gives no sign that `appsettings` was ignored.

### Deviation

**`Configuration/SessionOptions` was renamed `AuthSessionOptions`** — it collided with `Microsoft.AspNetCore.Builder.SessionOptions`, the same trap that forced `AuthCookieOptions` in §4. Two of the three options classes the roadmap named collide with framework types; the `Auth` prefix is now the convention.

## Questions for the Project Owner

1. ~~Is the 10-minute recent-auth window acceptable for step-up actions?~~ ✅ **Resolved 2026-07-22: 5 minutes**, reconciling the conflict with §4. `AuthSessionOptions.RecentAuthenticationWindow`.

None outstanding.
