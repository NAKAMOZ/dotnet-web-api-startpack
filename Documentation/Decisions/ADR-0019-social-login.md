# ADR-0019: Social Login — Google and GitHub via an API-Driven Redirect Flow

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decisions P12 and P13** (`ROADMAP/00-overview.md`)
- **Affects:** §4 (token architecture), §6 (`Account` entity), §12 (services), §22 (security tests)

## Context

Social login lets a user authenticate with an identity they already have. Two questions had to be answered before §4 could draw the callback sequence: **which providers ship at launch**, and **who drives the OAuth dance** — the browser client or the API.

The second question is the security-relevant one. In an OAuth authorization-code exchange, whoever performs the token exchange must hold the client secret. If the SPA drives the flow, the secret cannot travel with it, so the flow must use PKCE and the API becomes a code-receiver rather than a redirect participant. If the API drives it, the secret never leaves the server.

## Decision

**Providers at launch (P12): Google and GitHub.** Both packages are already pinned in `Directory.Packages.props` ([ADR-0013](ADR-0013-package-manifest.md)) — `Microsoft.AspNetCore.Authentication.Google` and `AspNet.Security.OAuth.GitHub`.

**Flow (P13): API-driven redirect.**

- `GET /api/v1/auth/social/{provider}/authorize` returns **302** to the provider's authorization endpoint, carrying a **signed, short-lived `state`** value.
- `GET /api/v1/auth/social/{provider}/callback` validates `state`, exchanges the code **server-side**, then links or creates the `Account` + `User` and issues a session exactly as password login does.

The SPA-driven PKCE variant is **deferred to the §29 backlog**, not rejected outright.

**The two providers are not equivalent, and the difference is load-bearing:**

| | Google | GitHub |
|---|---|---|
| Protocol | OpenID Connect | OAuth 2.0 only |
| Email in the identity response | Yes, with a verified flag | No — requires a separate `/user/emails` call |
| Email verification status | Asserted by the provider | **Not guaranteed** |

**Consequently:** a Google login with a provider-asserted verified email may set `User.EmailVerified`. A **GitHub login may not** — the address must be treated as unverified until this system verifies it through its own email-verification flow (§12), unless GitHub's `/user/emails` response explicitly marks it verified and primary.

**Account linking** is by `(Provider, ProviderAccountId)`, never by email address alone.

## Alternatives considered

**SPA-driven PKCE (the other half of P13).** More natural for a browser SPA, which already owns navigation, and avoids the API issuing redirects. Rejected for v1 in favour of shipping one flow well; PKCE is the natural second flow when a browser client needs it, and the backlog entry records that.

**Supporting both flows immediately.** Rejected: two authentication paths per provider doubles the negative-test surface in §22 for no v1 consumer.

**Microsoft Entra ID at launch.** Rejected — no package in the pinned manifest, so it would need a new dependency and its own ADR, and no consumer requires it.

**Shipping no provider initially** (abstract flow only, concrete provider later). Rejected: an OAuth integration written without a concrete provider is untested design, and the provider differences above are exactly the kind of detail that only surfaces on contact with a real one.

**Linking accounts by email address.** Rejected on security grounds, and worth stating explicitly because it is the tempting shortcut: if a provider asserts an email this system has not verified, matching on it lets an attacker who controls that address at the provider take over an existing local account. Linking is by provider-scoped account id, and any email-based merge requires a verification step.

## Consequences

- Client secrets for both providers stay server-side and are configuration, never repository content (§25, P7).
- The `state` parameter is a signed, short-lived, single-use value. Without it the callback is open to CSRF-style forgery, so §22 must include a callback replay and a forged-`state` test.
- `User.PasswordHash` is nullable ([ADR-0005](ADR-0005-custom-user-store.md)) precisely so a social-only user is representable. Every password code path must handle its absence.
- **GitHub-sourced emails do not confer `EmailVerified`.** This is an easy detail to get wrong in a way that silently weakens account recovery, since a verified-email flag gates password reset.
- Callback issues a session through the same path as password login — same `Session` row, same rotating refresh chain, same cookie/bearer transport ([ADR-0003](ADR-0003-token-transport.md)). `amr` records the social provider.
- Adding a third provider later is a package, a configuration block, and an ADR — the flow itself is provider-agnostic once these two are working.
- Unlinking the last authentication method must be prevented: a user with no password, no passkey, and one linked account cannot be allowed to unlink it and lose all access. §12 enforces this on `DELETE /users/me/accounts/{accountId}`.
