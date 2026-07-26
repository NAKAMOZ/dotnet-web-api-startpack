# 22. Security Testing

## Objective

Adversarial test coverage: prove the designed defenses hold against the specific attacks they were designed for.

## Scope

Negative-path test suite (part of `IntegrationTests/Security/`), CI dependency audit, optional DAST.

## Architectural Decisions

- Attack suite lives beside integration tests (same harness), tagged `[Trait("Category","Security")]` for separate CI reporting.
- Every §4 "Security Considerations" claim maps 1:1 to at least one test here (traceability table in the doc).

## Technology Decisions Requiring Approval

Optional OWASP ZAP baseline scan in CI — `Pending Decision` (recommend: add once staging exists, §27).

## Tasks

- [x] JWT attacks: `alg: none`; HS256-signed token using the public key as HMAC secret (algorithm confusion); tampered payload; expired token (time advance via `TimeProvider`); wrong `iss`/`aud`; unknown `kid`; retired-key `kid`.
  - The retired-`kid` test is the regression guard that matters most: adding a "try every key" fallback looks like a robustness improvement and silently makes key retirement meaningless.
- [x] Refresh attacks: replay rotation, successor-holder logout, session revocation and audit at the PostgreSQL service boundary; rotation/replay also covered over HTTP.
- [x] CSRF: cookie-mode state changes without/with-wrong `X-CSRF-Token`; CSRF token bound to session (a token minted for session A must fail on session B, even when cookie and header agree).
- [x] Enumeration: duplicate registration/reset public equality and login status/code/timing parity with a documented best-effort bound.
- [x] Lockout: boundary, reset-on-success, HTTP invisibility and admin unlock.
- [x] AuthZ: admin/protected endpoint matrix, recent-auth/API-key step-up denial and API-key scope/current-role intersection.
  - Step-up must be tested against `auth_time`, **not** `iat`: assert that refreshing a session repeatedly does *not* restore step-up eligibility. An implementation reading `iat` passes every happy-path test while providing no protection (Authentication.md §14).
  - API keys can never satisfy step-up — they carry no `auth_time`.
- [ ] Input abuse: oversized bodies (request size limits), malformed JSON, header injection into correlation ID (format validation), sort-field injection.
- [ ] Redaction test: capture all logs during a full flow run; assert zero occurrences of any issued token/password/secret material (§15).
- [x] CI: `dotnet list package --vulnerable --include-transitive` failing gate (§26).

## Expected Deliverables

`IntegrationTests/Security/` suites; traceability table in `Documentation/Security/AttackCoverage.md`.

**Current status (2026-07-26):** 47 `Category=Security` cases are separately selectable
and green. `AttackCoverage.md` maps covered and genuinely blocked claims; enumeration,
HTTP lockout/admin unlock, API-key scopes, oversized-body policy, and the full-flow scalar
log scan remain tied to missing §12/§27 production behavior.

## Dependencies

§21 harness; §4 design (the claims under test).

**§4 side is ready** (2026-07-22): `Documentation/Architecture/Authentication.md` §16 maps all 20 attacks below to a designed defence. That review closed six design gaps — most importantly step-up authentication, which had no defined mechanism at all. This workstream turns that table into `Documentation/Security/AttackCoverage.md` with concrete test names.

## Security Considerations

This suite is regression armor: it exists so future contributors cannot weaken a defense without a red build.

## Testing Requirements

Runs in every CI build (not a nightly afterthought) — auth is the product.

## Documentation Requirements

AttackCoverage traceability table (§4 claim → test name).

## Definition of Done

Every §4 claim has a mapped green test; vulnerability gate active in CI.

## Questions for the Project Owner

1. Add ZAP baseline scanning once a staging environment exists?
