# ADR-0005: Custom User Store Instead of ASP.NET Core Identity

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *User store*
- **Affects:** §5 (authorization), §6 (entities), §7 (EF configuration), §12 (services)

## Context

This project *is* an authentication system, not an application that happens to need login. That inverts the usual build-versus-adopt calculation.

ASP.NET Core Identity is the default answer for adding auth to an ASP.NET application, and it is a good one in that context. Here, though, the identity schema is the product's core domain model rather than a supporting concern — and the target schema is Better Auth–shaped, spanning sessions with device metadata, rotating refresh chains, external account links, passkey credentials, API keys, TOTP secrets, recovery codes, and a signing-key ring.

## Decision

**Custom entities**, modelled on the Better Auth schema, owned by this project. Thirteen entities, enumerated in `ROADMAP/00-overview.md`. **No ASP.NET Core Identity.**

What *is* reused from the framework, because reimplementing it would be strictly worse:

- ASP.NET Core `JwtBearer` middleware for access-token validation,
- custom `AuthenticationHandler` implementations for the cookie, API-key, and passkey schemes,
- policy-based authorization (`IAuthorizationHandler`, requirements, policies).

The line is: **the framework's authentication and authorization plumbing, none of its identity storage.**

## Alternatives considered

**ASP.NET Core Identity as-is.** Brings user management, password hashing, lockout, and token providers for free. Rejected on schema fit — its `IdentityUser`/`IdentityRole` model does not accommodate the session, passkey, API-key, and signing-key entities this project requires, and its extension points are designed for adding columns to an existing model rather than replacing it. Adopting it would mean fighting the framework in exactly the area that is this project's entire purpose.

**Identity for users and roles, custom entities for everything else.** A hybrid, and the tempting middle path. Rejected as the worst of both: two sources of truth for what a user is, two migration histories, and a permanent seam between the framework's conventions and ours running straight through the middle of the domain model.

**A third-party auth library or hosted identity provider.** Out of scope by definition — building this system is the project.

## Consequences

- Full control over the schema: `citext` emails, `SecurityStamp`, `LockoutEndsAt`, `FailedLoginCount`, and a nullable `PasswordHash` for social- and passkey-only users all follow naturally rather than being bolted on.
- **We own the security-critical code Identity would have provided.** Password hashing ([ADR-0006](ADR-0006-password-hashing.md)), lockout, and token generation are ours to get right — which is precisely why §22 exists as a dedicated security-testing workstream and why these ADRs record parameters rather than leaving them to code archaeology.
- No Identity UI scaffolding, no `UserManager`/`SignInManager` — every operation goes through our own services (§12), keeping the surface auditable.
- Reusing `JwtBearer` and policy-based authorization means standard `[Authorize]` attributes and policies work as any ASP.NET developer expects; the custom part is invisible at the controller layer.
- Migration away from this schema later would be a data migration, not a configuration change. That is accepted — the schema is the product.
