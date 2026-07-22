# 10. Validation

## Objective

Every incoming DTO validated by a dedicated FluentValidation validator before any handler logic runs; failures return RFC 9457 `ValidationProblemDetails`.

## Scope

Validators for all request DTOs, the pipeline filter, shared rule sets.

## Architectural Decisions

- One validator per request DTO in `Validators/<Feature>/`, mirroring `DTOs/`.
- Validation runs in `Filters/ValidationFilter.cs` (an `IAsyncActionFilter` resolving `IValidator<T>` per action argument) — the deprecated FluentValidation auto-validation package is not used; this keeps async validators available and the behavior explicit.
- Shared rules in `Validators/Common/`: `PasswordRules` (length ≥ 12, no composition-class nonsense per current NIST guidance, deny-list of top breached passwords — static top-1k list in v1), `EmailRules`, `PaginationRules` (pageSize ≤ 100).
- Validators are structural only (format, ranges, required). Business checks that need the DB (email uniqueness, token validity) live in services and return domain errors — keeps validators fast, side-effect-free, and unit-testable.

## Technology Decisions Requiring Approval

None (FluentValidation approved).

## Tasks

- [ ] `Filters/ValidationFilter.cs` + registration in `Extensions/ServiceCollectionExtensions.Validation.cs` (assembly-scanned validator registration).
- [ ] `Validators/Common/PasswordRules.cs`, `EmailRules.cs`, `PaginationRules.cs`.
- [ ] One validator per request DTO (~25 files), e.g. `Validators/Auth/RegisterRequestValidator.cs`, `LoginRequestValidator.cs`, `Validators/ApiKeys/CreateApiKeyRequestValidator.cs` (scopes must be known constants), `Validators/Admin/AdminUserListQueryValidator.cs` (sort whitelist).
- [ ] Error message catalog: stable `errorCode` per rule (e.g. `password_too_short`) surfaced in the ProblemDetails `errors` payload for client i18n.

## Expected Deliverables

`Validators/` tree, validation filter, registration extension.

## Dependencies

§9. Blocks §11.

## Security Considerations

Password policy enforced in exactly one place (`PasswordRules`) so register, reset, and change can never drift apart. Sort/filter whitelists in query validators prevent ordering-by-arbitrary-column probing.

## Testing Requirements

§20: every validator has a unit test file covering accept/reject boundaries; the CI suite fails if a request DTO exists without a corresponding validator (reflection guard test).

## Documentation Requirements

Validation rules section of each endpoint doc (§19) copied from the validator, reviewed in the same PR.

## Definition of Done

Guard test proves full validator coverage; invalid input on any endpoint yields RFC 9457 400 with field-level errors and stable codes.

## Questions for the Project Owner

1. Minimum password length 12 — acceptable, or align to a different policy?
