# Authorization and Permissions

**Status:** Written 2026-07-22 · **Workstream:** §5 · **Consumed by:** §11 (controllers), §12 (services)

How the API decides what an authenticated caller may do. Authentication — proving *who* — is [`Authentication.md`](Authentication.md).

---

## 1. The model in one paragraph

Two seeded roles. Permissions are **string constants in code**, mapped to roles by a static table. An endpoint declares one permission with an attribute; a policy provider turns that into a policy on demand; a handler checks the caller's `roles` claim against the map. Self-service access carries no permission at all — those routes never take a user id, so there is nothing to authorize beyond being authenticated.

Database-driven permissions are deliberately deferred to §29. They become worthwhile only when roles need editing at runtime without a deploy.

---

## 2. Roles

| Role | Purpose |
|---|---|
| `Admin` | Full administrative access — every permission below |
| `User` | Ordinary account — **no** permissions |

`User` holding no permissions is the design, not an oversight. Every permission in this system is cross-user by construction (§3), so granting one to `User` would give every account administrative reach.

---

## 3. Permission catalog

Naming is `resource:action[:scope]`. The `:any` suffix means *across all users* — it is what makes a permission administrative.

| Permission | Grants | Endpoints |
|---|---|---|
| `users:read:any` | Read any user | `GET /admin/users`, `GET /admin/users/{userId}` |
| `users:write:any` | Modify any user, including admin unlock | `PATCH /admin/users/{userId}` |
| `users:delete:any` | Delete any user | `DELETE /admin/users/{userId}` |
| `roles:assign` | Grant a role | `POST /admin/users/{userId}/roles` |
| `roles:revoke` | Revoke a role | `DELETE /admin/users/{userId}/roles/{roleId}` |
| `sessions:revoke:any` | Revoke another user's sessions | `DELETE /admin/users/{userId}/sessions` |
| `audit:read` | Read the security audit trail | `GET /admin/audit-logs` |

Constants live in `Handlers/Authorization/Permissions.cs`; the role assignment is `RolePermissionMap.cs`.

**The catalog and the map are validated against each other at startup.** A constant granted to no role, or a map entry naming a constant that no longer exists, throws before the host starts. Both are otherwise silent: the first presents as an endpoint that denies everyone, the second as configuration that reads like it still grants something.

---

## 4. Declaring a requirement

```csharp
[RequirePermission(Permissions.UsersReadAny)]
public Task<ActionResult<PagedResponse<UserResponse>>> ListUsers(...)
```

Always a constant, never a literal. A literal is a magic string that the startup validation cannot check.

`RequirePermissionAttribute` encodes the permission into a policy name — `perm:users:read:any` — which `PermissionPolicyProvider` parses back out and materialises into a policy. Nothing has to be registered per permission; adding one to the catalog is enough.

An **unknown** permission throws rather than producing a policy nobody can satisfy. That would fail closed, which is safe, but presents as a permanent unexplained 403 — the kind of bug found weeks later.

---

## 5. Resource ownership — why there is no IDOR surface

Self-service routes resolve the subject from the `sub` claim and **never accept a user id**:

```text
GET    /api/v1/users/me            not  /api/v1/users/{id}
GET    /api/v1/sessions            not  /api/v1/users/{id}/sessions
DELETE /api/v1/passkeys/{credentialId}
```

The third takes an id, and it is the shape to be careful with: the handler must scope the lookup to the caller (`WHERE CredentialId = @id AND UserId = @sub`), not fetch by id and then compare. Fetch-then-compare is the classic IDOR: it works until someone refactors the check away, and it leaks existence through timing and error codes even while it works.

The same applies to `DELETE /api/v1/api-keys/{keyId}` and `DELETE /api/v1/sessions/{sessionId}`.

---

## 6. Step-up (recent authentication)

Three destructive self-service operations additionally require recent authentication:

| Endpoint | Attribute |
|---|---|
| `DELETE /api/v1/mfa/totp` | `[RequireRecentAuth]` |
| `POST /api/v1/mfa/recovery-codes/regenerate` | `[RequireRecentAuth]` |
| `DELETE /api/v1/users/me` | `[RequireRecentAuth]` |

The window is **5 minutes** (`AuthSessionOptions.RecentAuthenticationWindow`), measured against the `auth_time` claim — never `iat`. Full rationale in [`Authentication.md` §14](Authentication.md); the short version is that `iat` moves forward on every refresh, so a stolen session would satisfy the check forever.

`PUT /api/v1/users/me/password` deliberately carries **no** step-up requirement: it requires the current password, which is the stronger proof.

**API keys can never satisfy step-up** — they carry no `auth_time` because no human authenticated.

---

## 7. API-key scopes

An API key carries scopes drawn from the same permission constants. Two rules:

1. **A key can never exceed its creator's permissions.** The effective permission set is the *intersection* of the key's scopes and the owning user's role-granted permissions, evaluated at request time — not at creation time. A user who loses the `Admin` role must not leave behind a key that still acts as an admin.
2. Scope checks run through the same `PermissionRequirement`, so an endpoint's `[RequirePermission]` works identically for a key and for a user.

---

## 8. Deny by default

The fallback policy requires an authenticated user for any endpoint carrying no authorization metadata. A forgotten attribute fails closed.

Anonymous endpoints opt out **explicitly**:

```csharp
[AllowAnonymous]
public Task<ActionResult<LoginResponse>> Login(...)
```

> ⚠️ **The fallback policy is defined but not yet activated.** `AuthorizationPolicies.DenyByDefault` exists and is unit-tested, but it is not assigned to `AuthorizationOptions.FallbackPolicy` until §12 registers an authentication scheme.
>
> The reason is worth knowing: calling `AddAuthorization` makes minimal hosting insert the authorization middleware automatically, and that middleware applies the fallback to every request — including requests matching no endpoint. With no scheme registered there is nothing to challenge with, so every request becomes a 500. This was found by the §3 composition-root smoke test.
>
> Activation is a single line, marked in `ServiceCollectionExtensions.Authorization.cs` and tracked as §5's one open Definition-of-Done item.

---

## 9. Scheme composition

A policy scheme selects the concrete authentication scheme per request — `JwtBearer` for a bearer header or an access cookie, `ApiKey` for `ak_`-prefixed credentials. **Authorization policies are scheme-agnostic**: `[RequirePermission]` behaves the same regardless of how the caller authenticated.

Registered in §12 alongside the schemes themselves.

---

## 10. Handler conventions

Two rules that matter more than they look:

**Never call `context.Fail()`.** Failure is sticky — it vetoes the entire authorization pass even if another handler would have succeeded. Not calling `Succeed` is what denies, and it composes correctly when an endpoint carries several requirements.

**Unknown roles grant nothing, silently at the policy level but not at the log level.** A role present in a token but absent from the map means a token was issued against a role this build does not know about — §15 logs it.

---

## 11. What §22 tests

| Scenario | Expected |
|---|---|
| 👑 endpoint as `User` | 403 |
| 🔐 endpoint anonymous | 401 |
| API key exceeding its scopes | 403 |
| Step-up endpoint outside the window | 403 |
| Step-up after repeated refresh (no re-auth) | **403** — `auth_time` did not move |
| Endpoint with no attribute, anonymous | 401 via the fallback *(once §12 activates it)* |
| Own-resource route with another user's resource id | 404, not 403 — existence is not disclosed |
