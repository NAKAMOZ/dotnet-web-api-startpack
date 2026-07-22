# 11. Controller Architecture

## Objective

Thin, feature-split controllers — one resource responsibility each, no business logic, fully annotated for OpenAPI.

## Scope

The 13 controllers from the endpoint inventory plus a shared base class. Service calls only.

## Architectural Decisions

- `Controllers/ApiControllerBase.cs`: `[ApiController]`, versioned route prefix, helpers (`CurrentUserId`, `CurrentSessionId` from claims) — nothing else.
- Hard thinness rule: an action = map request → single service call → map result to response/status. Target ≤ ~20 lines; any branching beyond status mapping belongs in the service.
- Admin controllers split by sub-responsibility (`AdminUsersController`, `AdminUserRolesController`, `AdminUserSessionsController`, `AdminAuditLogsController`) per the mandate against controllers accumulating unrelated operations.
- Every action: explicit `[ProducesResponseType]` set (success + each failure), `[RequirePermission]`/`[AllowAnonymous]`, `CancellationToken` parameter flowing to the service and EF.
- Status conventions: 201 + `Location` for creation, 204 for deletes/revocations, 202 for MFA-challenge login step, 401 vs 403 semantics documented in §13.

## Technology Decisions Requiring Approval

✅ **None outstanding.** P2 (versioning) is resolved — URL-segment `/api/v1/…` via `Asp.Versioning.Mvc`, approved 2026-07-22 (`Documentation/Decisions/ADR-0015-api-versioning.md`). Route templates use `[Route("api/v{version:apiVersion}/…")]`; `/.well-known/jwks.json` and `/health/*` stay unversioned.

## Tasks

- [x] `Controllers/ApiControllerBase.cs`.
- [x] One file per controller — **14, not 13**: the count in this file's Scope omits `WellKnownController`, which the endpoint inventory does list. It does not inherit `ApiControllerBase`, because `/.well-known/jwks.json` is fixed by RFC 8615 and versioning it into `/api/v1/…` would make it undiscoverable to every standard client.
- [x] `Extensions/ServiceCollectionExtensions.Api.cs`: controllers, versioning (P2), JSON options (camelCase, enums as strings, ignore-null).
- [x] Architecture tests: no controller references `AppDbContext`; every action takes a `CancellationToken`; every action carries `[ProducesResponseType]`; every action declares an authorization posture; action IL stays small.

## Decisions taken here

1. **A placeholder authentication scheme was added** — `Handlers/Authentication/PlaceholderAuthenticationHandler.cs`, registered as the default in `AddAuthenticationServices`, and `app.UseAuthentication()` / `app.UseAuthorization()` are now in the pipeline.

   This is scope beyond §11 and is flagged for review. The reason it could not wait: ASP.NET Core does not return 401 for an `[Authorize]` endpoint when no challenge scheme is registered — it **throws**, so all 30 protected routes would have answered **500**, and §11's Definition of Done ("all inventory routes respond") would have been unverifiable. The handler returns `AuthenticateResult.NoResult()` unconditionally, so it is fail-closed: if it survives into §12 the symptom is that nobody can log in anywhere.

   **`FallbackPolicy` is still not set** — that remains §12's one-line change. It applies to requests matching no endpoint as well, so activating it now would turn every 404 into a 401.

2. **Every action returns `501 Not Implemented`** via `ApiControllerBase.NotImplementedYet()`. The DoD sanctions stubs; a fabricated success would make an unwritten endpoint look finished to every client and every test.

3. **`PasswordResetController.RequestReset`, not `Request`** — an action named `Request` hides `ControllerBase.Request`. Here it was a compile error; in a controller that used both it would be a subtle bug.

## Expected Deliverables

14 controller files + base + API extension + placeholder scheme; endpoint inventory fully routable.

## Dependencies

§5 (attributes), §9 (DTOs), §10 (filter), §12 (services — controllers and services land per feature slice together).

## Security Considerations

Controllers never read tokens or headers directly (middleware/handlers own that); `me` routes derive identity solely from validated claims.

## Testing Requirements

Architecture tests above; behavioral coverage via §21.

## Documentation Requirements

Each controller lands with its endpoint docs (§19) in the same PR — DoD-gated.

## Definition of Done

All inventory routes respond (even if service returns stub in early slices); architecture tests green; zero business logic in controllers by review.

**Status: met.** Verified against the running app (`dotnet run`, PostgreSQL container live):

- The OpenAPI document lists **43 operations across 37 paths** — exactly the endpoint inventory, no more and no less.
- Anonymous routes answer `501` (`/.well-known/jwks.json`, `/auth/csrf`, `/auth/register`).
- Protected routes answer `401`, not `500` (`/sessions`, `/users/me`, `/admin/users`, `/mfa/totp`).
- An unmatched path still answers `404` — the fallback policy is correctly still inactive.
- Six architecture tests green; 76 in the suite.

This also closed §10's outstanding half. `POST /api/v1/auth/register` with `{"email":"nope","password":"short"}` returns RFC 9457:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors":     { "Email": ["Email address is not valid."], "Password": ["Password must be at least 12 characters."] },
  "errorCodes": { "Email": ["email_invalid"],               "Password": ["password_too_short"] }
}
```

## Questions for the Project Owner

1. **`POST /auth/register` currently declares `409 Conflict` for a duplicate email — which is an account-enumeration oracle.** §16 requires register, reset and login to be enumeration-safe, but §12's exception list contains `EmailAlreadyRegisteredException`, so the roadmap points both ways. The alternatives: keep the 409 and accept that registration discloses which addresses exist, or make registration always return `202 Accepted` and send either a welcome or a "someone tried to register your address" email. The second is what the reset flow already does. Worth deciding before §12 writes the service — it changes the response contract, not just the implementation.
