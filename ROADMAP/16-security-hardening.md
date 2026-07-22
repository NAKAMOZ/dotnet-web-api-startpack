# 16. Security Hardening

## Objective

Systematic closure of the attack surface beyond token design: lockout, enumeration resistance, transport security, dependency hygiene — mapped to OWASP ASVS.

## Scope

Account lockout, anti-enumeration, security headers review, secret hygiene, dependency scanning, ASVS checklist.

## Architectural Decisions

- **Lockout**: 5 consecutive failures → 15-minute lock (`User.LockoutEndsAt`); counter resets on success; lockout response externally identical to invalid credentials; `account_locked` audited; admin unlock via `PATCH /admin/users/{id}`. Per-IP throttling is §17's job — lockout is per-account and complements it.
- **Anti-enumeration**: register with existing email → same 202-shaped response as success (verification email says "you already have an account"); reset request → always 202; login → single `invalid_credentials` code for unknown-user/wrong-password/locked; §12's dummy-hash equalizes timing.
- **Secrets in memory**: TOTP secrets encrypted at rest via ASP.NET Core Data Protection; Data Protection keys persisted to DB (`DataProtectionKeys` table — required for multi-instance cookie/CSRF survival, §27).
- Dependency hygiene: `dotnet list package --vulnerable --include-transitive` gate in CI (§26); Dependabot/Renovate config.

## Technology Decisions Requiring Approval

None.

## Tasks

- [ ] Lockout logic in `Services/Auth/LoginService` + options (`Configuration/LockoutOptions.cs`).
- [ ] Anti-enumeration sweep: audit every 🔓 endpoint's response pair (exists vs not) for shape, code, and timing parity; document in `Documentation/Security/Enumeration.md`.
- [ ] Data Protection persistence: `Data/Configurations/DataProtectionKeyConfiguration.cs` + `AddDataProtection().PersistKeysToDbContext<AppDbContext>()`.
- [ ] `Documentation/Security/ASVS-Checklist.md`: ASVS L2 items relevant to auth APIs, each marked met/deferred with pointer.
- [ ] Renovate/Dependabot config file.

## Expected Deliverables

Lockout implementation, enumeration doc + fixes, Data Protection persistence, ASVS checklist, update-bot config.

## Dependencies

§12, §15.

## Security Considerations

This workstream *is* security; its output is the ASVS traceability doc — every deferred item needs an owner-visible justification.

## Testing Requirements

§22 covers lockout boundaries, enumeration parity (body + timing), and header presence.

The constraints these place on the login flow are recorded in `Documentation/Architecture/Authentication.md` §5 — identical response shape and code for unknown-user / wrong-password / locked, dummy-hash timing parity, and counter reset on success.

## Documentation Requirements

`Documentation/Security/` populated (Enumeration, ASVS checklist).

## Definition of Done

ASVS L2 checklist complete with no unexplained gaps; §22 hardening tests green.

## Questions for the Project Owner

1. Lockout policy 5 failures / 15 min — approve or adjust?
