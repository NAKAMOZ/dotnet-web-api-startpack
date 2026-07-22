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

- [x] `Filters/ValidationFilter.cs` + registration in `Extensions/ServiceCollectionExtensions.Validation.cs` (assembly-scanned validator registration).
- [x] `Validators/Common/PasswordRules.cs`, `EmailRules.cs`, `PaginationRules.cs`.
- [x] One validator per request DTO — **20 files**, not ~25: the count follows from §9's 20 request/query DTOs.
- [x] Error message catalog: `Validators/Common/ValidationErrorCodes.cs`, surfaced in the Problem Details `errorCodes` extension.

## Decisions taken here

1. **The password policy is length-first, per NIST SP 800-63B.** Minimum 12, maximum 256, deny list, predictable-pattern rejection — and deliberately **no composition classes**. Composition rules push users toward `Password1!`, which is why several such passwords are in this project's own deny list.

2. **The deny list is a seed, not a corpus, and the reason matters.** A conventional top-1k breach list is almost entirely passwords under 12 characters, which the length rule already rejects — so it would add nearly nothing. `Validators/Common/CommonPasswords.txt` (~160 entries) instead targets what survives a 12-character minimum: padded classics, keyboard runs, and composition-rule artefacts. It is embedded in the assembly, and loading throws if the resource is missing rather than validating passwords without it.

3. **Login and change-password do not apply the full policy to the *presented* password** — presence and length bounds only. Grading it would reject users whose password predates a policy change, on the very endpoint that exists to fix that, and would tell an attacker a guess cannot be the stored value before any credential check runs.

4. **`SuppressModelStateInvalidFilter = true`**, so the validation filter is the single producer of 400s — one body shape, one set of codes. The cost: malformed JSON and unbindable values now fall to the exception handler, which §13/§14 own. Until they land those requests produce an unshaped 400.

5. **The maximum password length is a cost control, not a security one.** Argon2id hashes whatever it is given, so an unbounded password is unbounded deliberate work on an anonymous endpoint.

## Expected Deliverables

`Validators/` tree (25 files: 20 validators + 4 shared rule classes + the deny list), validation filter, registration extension, 3 test files.

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

**Status: coverage half met in full; the wire-format half waits on §11.** 70 tests green. Three guards plus boundary tests:

- **Every request DTO has a validator** — reflection over `Api.DTOs`, with `PagedQuery` exempt as a base type the derived query validators cover.
- **Every validator resolves from the container.** This one earned its keep immediately: FluentValidation's assembly scanner registers only *public* validators by default, and every validator here is `internal`. Without `includeInternalTypes: true` the scan found nothing, no validator resolved, and the filter skipped every argument — validation would have silently stopped happening while every endpoint kept returning 200. Nothing else in the suite would have noticed.
- **Every rule carries an explicit error code.** FluentValidation otherwise falls back to its own rule class name (`NotEmptyValidator`), which is a leaked implementation detail that changes on library upgrades.

Not yet demonstrable: an actual 400 over HTTP. No controllers exist until §11, so the filter is verified by construction and unit tests rather than end to end. §21 asserts the response shape once there is an endpoint to call.

## Questions for the Project Owner

1. **Minimum password length 12** — implemented as `PasswordRules.MinimumLength`. Acceptable, or align to a different policy? Raising it later is safe; lowering it weakens every account created in between.
