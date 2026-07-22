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

P2 (versioning) must be approved before route templates are written.

## Tasks

- [ ] `Controllers/ApiControllerBase.cs`.
- [ ] One file per controller (13 files) implementing exactly the inventory routes — recommended build order: `AuthController` → `SessionsController` → `EmailVerificationController` + `PasswordResetController` → `UsersController` → `MfaController` → `SocialAuthController` → `PasskeysController` → `ApiKeysController` → admin controllers → `WellKnownController`.
- [ ] `Extensions/ServiceCollectionExtensions.Api.cs`: controllers, versioning (P2), JSON options (camelCase, enums as strings, ignore-null).
- [ ] Architecture test (§20): no controller references `AppDbContext` directly; every action has `CancellationToken`; every action annotated.

## Expected Deliverables

13 controller files + base + API extension; endpoint inventory fully routable.

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

## Questions for the Project Owner

None beyond P2.
