# 20. Unit Testing

## Objective

Fast, isolated coverage of every decision-bearing unit: validators, token logic, crypto wrappers, policy code, mappings.

## Scope

`tests/UnitTests` — no database, no network, no `WebApplicationFactory`.

## Architectural Decisions

- xUnit v3; folder structure mirrors the API project root (`UnitTests/Validators/Auth/…`, `UnitTests/Services/Tokens/…`).
- `TimeProvider` faked via `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` — all expiry math tested deterministically.
- EF-dependent services are integration-tested (§21), not unit-tested against in-memory providers (in-memory EF misrepresents relational behavior; explicitly avoided).
- Guard/architecture tests live here: DTO↔validator coverage, sensitive-property leak check, controller thinness rules (§11), audit-event enum↔catalog sync (§15).

## Technology Decisions Requiring Approval

None.

## Tasks

- [ ] Validator suites: every validator, accept/reject boundary per rule (~25 test files).
- [ ] `Argon2PasswordHasherTests`: hash/verify roundtrip, parameter versioning, `NeedsRehash` on parameter bump, timing-safe failure.
- [ ] `AccessTokenIssuerTests`: claims set, `kid` header, expiry from FakeTimeProvider, ES256 signature verifies against issuer's public key.
- [ ] `RefreshRotationStateMachineTests` (store faked): rotate, chain, reuse → session revocation, expiry boundaries, sliding-window math incl. absolute-cap edge.
- [ ] `TotpServiceTests`: window tolerance, replay rejection. `RecoveryCode` single-use.
- [ ] `PermissionPolicyProviderTests`, `RolePermissionMapTests`, `RecentAuthHandlerTests`.
- [ ] Mapping tests per feature + reflection guard test (§9).
- [ ] Architecture tests (§11 rules).

## Expected Deliverables

`tests/UnitTests` suite, runnable in seconds, wired into CI.

## Dependencies

Lands incrementally with §10–§12 (same-PR rule).

## Security Considerations

The rotation state machine and hasher tests are security tests in unit clothing — they pin the exact behaviors §4 promises.

## Testing Requirements

Coverage gate: ≥ 85% line coverage on `Services/Tokens/`, `Services/Crypto/`, `Validators/` (measured via coverlet in CI); no global vanity target.

## Documentation Requirements

`tests/README.md`: how to run, naming convention (`Method_Condition_Expectation`).

## Definition of Done

All suites green; coverage gate met; guard tests active.

## Questions for the Project Owner

None.
