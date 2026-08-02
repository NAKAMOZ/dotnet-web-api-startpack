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

- [x] Validator suites: the rejection matrix exercises every request validator and 52
  invalid-field/boundary cases; options validators cover cross-field and environment rules.
- [x] `Argon2PasswordHasherTests`: hash/verify roundtrip, parameter versioning, `NeedsRehash` on parameter bump, corrupt/wrong input failure, and distinct password/secret profiles.
- [x] `AccessTokenIssuerTests`: claims set, `kid` header, expiry from FakeTimeProvider, ES256 signature verifies against the issuer's public key, and a rotation-between-header-and-signature race retries rather than issuing an invalid token.
- [x] Refresh rotation state machine: moved to real-PostgreSQL integration coverage per the
  workstream rule; rotate/chain/reuse, session revocation, idle and absolute caps are green.
- [x] TOTP/recovery state: real-service integration covers window behavior, atomic replay
  rejection including concurrency, and single-use recovery codes.
- [x] `PermissionPolicyProviderTests`, `RolePermissionMapTests`, `RecentAuthHandlerTests`.
- [x] Mapping tests for the only live mapping extension (`AuditMappingExtensions`) plus the §9 DTO reflection guards. Remaining feature mappings land with their services.
- [x] Architecture tests (§11 rules).

### Recorded deviations

- `RefreshTokenService`, `SessionService` and `MfaTicketService` depend directly on `AppDbContext`; no separately fakeable store abstraction exists. In keeping with this workstream's own rule against EF in-memory doubles, their transaction/rotation boundaries move to §21's PostgreSQL integration suite instead of being unit-tested against behavior the production provider does not have.
- TOTP and recovery services are EF-dependent; their single-use guarantees are tested against
  PostgreSQL rather than a fake store that cannot represent the conditional updates.

## Expected Deliverables

`tests/UnitTests` suite, runnable in seconds, wired into CI.

## Dependencies

Lands incrementally with §10–§12 (same-PR rule).

## Security Considerations

The rotation state machine and hasher tests are security tests in unit clothing — they pin the exact behaviors §4 promises.

## Testing Requirements

Coverage gate: ≥ 85% line coverage on `Services/Tokens/`, `Services/Crypto/`, `Validators/` (measured via coverlet in CI); no global vanity target.

Verified 2026-08-02: the real-PostgreSQL integration run measures `Services/Tokens` at
94.23% line coverage; the crypto/validator unit gate remains above 85%.

## Documentation Requirements

`tests/README.md`: how to run, naming convention (`Method_Condition_Expectation`).

## Definition of Done

All suites green; coverage gate met; guard tests active.

## Questions for the Project Owner

None.
