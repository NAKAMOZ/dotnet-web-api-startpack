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

- [x] Options: `Configuration/LockoutOptions.cs`, bound and validated at startup, 5 failures / 15 min approved 2026-07-23.
- [x] Lockout state machine: `Services/Security/LockoutPolicy.cs` — **recorded deviation**, the arithmetic lives in its own type rather than inside `LoginService`, so §22 can assert boundaries without a database, a password hash and a full request per case. The decision to call it stays with the login service.
- [ ] `LockoutPolicy.RegisterFailure` body — the failure transition itself. Scaffolded with the three decisions it has to make; **currently throws**.
- [ ] Wire `LockoutPolicy` into `Services/Auth/LoginService` — blocked on §12, which owns that file.
- [x] Anti-enumeration sweep: every 🔓 endpoint's response pair audited for status, body, timing and side-effect parity — `Documentation/Security/Enumeration.md`. Written ahead of the services, as the contract §12 builds against. **Resolves the open `email_already_registered` question in `Documentation/Errors.md` §4 in favour of a `202` on the anonymous registration path.**
- [x] Data Protection persistence: `Data/Configurations/DataProtectionKeyConfiguration.cs`, `AppDbContext : IDataProtectionKeyContext`, `AddDataProtection().SetApplicationName(…).PersistKeysToDbContext<AppDbContext>()`, migration `20260723125006_AddDataProtectionKeys` applied. **ADR-0021.**
- [x] `Documentation/Security/ASVS-Checklist.md`: ASVS 4.0.3 L2, chapters V1–V14, each item met/designed/deferred/gap with a pointer; open items summarised with owners.
- [x] Dependabot config — `.github/dependabot.yml`, grouped version bumps plus daily security PRs, with the `Microsoft.OpenApi` 3.x ignore encoded so the rejected PR is never opened.

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

1. ~~Lockout policy 5 failures / 15 min — approve or adjust?~~ **Approved 2026-07-23**: 5 failures, 15-minute fixed window, counter resets on success, an expired lock grants a fresh allowance.
2. **Should admin operations require step-up MFA?** ASVS V4.3.1 asks for it; today admin endpoints are gated by `[RequirePermission]` alone. The mechanism already exists (`auth_time`, `RecentAuthenticationWindow`), so this is one attribute per admin action if approved. Recorded as a deliberate deferral in `Documentation/Security/ASVS-Checklist.md` until answered.
3. **`ProtectKeysWith*` for the Data Protection key ring.** ADR-0021 persists the ring to the database but leaves it unencrypted at rest — every provider is host-specific, so choosing one chooses a deployment target (P14). This is the largest known gap in §16 and is listed as such.
