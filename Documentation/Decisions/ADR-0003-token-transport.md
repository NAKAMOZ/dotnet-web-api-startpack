# ADR-0003: Dual Token Transport — Cookies and Bearer Headers

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, row *Token transport*
- **Affects:** §4 (token architecture), §14 (middleware/filters), §16 (hardening), §22 (security tests)

## Context

Browser clients and non-browser clients want opposite things from token transport.

A browser is safest when the token is in an `httpOnly` cookie, because JavaScript cannot read it and therefore XSS cannot exfiltrate it — but cookies are attached automatically by the browser, which is precisely what makes CSRF possible. A mobile app, CLI, or server has no cookie jar worth using and no CSRF exposure, and wants the token in a header it controls explicitly.

Picking one transport would either hand browser clients an XSS-readable token or force non-browser clients into cookie semantics that do not fit them.

## Decision

Support **both**, and let the client choose explicitly.

**Cookie mode (browsers):**

| Cookie | Contents | Attributes |
|---|---|---|
| `__Host-auth.access` | access token | `httpOnly`, `Secure`, `SameSite=Lax`, `Path=/` |
| `__Secure-auth.refresh` | refresh token | `httpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth/refresh` |
| `__Host-auth.csrf` | CSRF token | **not** `httpOnly`, `Secure` |

The refresh cookie uses the `__Secure-` prefix rather than `__Host-` because `__Host-` requires `Path=/`, which is incompatible with scoping the cookie to the refresh endpoint. Path scoping was judged the more valuable of the two properties: the browser then never attaches the refresh token to any request other than a refresh.

**Bearer mode (everything else):** both tokens are returned in the JSON response body; the client sends the access token as `Authorization: Bearer`.

**Selection:** the client sets `X-Auth-Transport: cookie|body` on login. Default is `body`. **The server never issues tokens in both places at once.**

**CSRF defence (cookie mode only):** double-submit. `GET /api/v1/auth/csrf` sets the readable `__Host-auth.csrf` cookie; state-changing requests authenticated *by cookie* must echo it in `X-CSRF-Token`. A filter enforces this and exempts bearer-authenticated requests, which are not reachable by CSRF.

## Alternatives considered

**Cookies only.** Clean CSRF story via `SameSite`, but forces non-browser clients into cookie handling and makes cross-origin API use awkward. Rejected on client-fit grounds.

**Bearer only.** Simple and CSRF-immune, but the browser must store the token somewhere JavaScript can reach — `localStorage` or memory — which converts any XSS into full token theft. Rejected: `httpOnly` is the single most effective mitigation available against that class of attack.

**Inferring transport from the request** (cookie present ⇒ cookie mode) instead of the `X-Auth-Transport` header. Rejected: implicit mode selection makes the security posture of a request depend on ambient state, and an attacker who can plant a cookie can influence which code path runs. An explicit header keeps the decision in one visible place. *(This was carried as an open question into §4 and **confirmed by the owner on 2026-07-22** — the header stays. No longer open.)*

**Separate endpoints per transport** (`/auth/login` returns a body, `/auth/login/cookie` sets cookies). Considered when the transport question was confirmed. The clearest possible separation, but it grows the endpoint inventory, duplicates every auth entry point, and means two sets of endpoint documentation in §19 for one operation. Rejected.

**Issuing tokens in both cookie and body simultaneously.** Rejected: it doubles the exfiltration surface for no benefit and makes it ambiguous which credential a client is actually relying on.

## Consequences

- Two authentication paths exist, so **both must be tested independently** — §22 needs CSRF-bypass attempts against cookie mode and header-injection attempts against bearer mode.
- The CSRF filter must exempt bearer requests correctly. Getting the exemption wrong in the permissive direction disables CSRF protection entirely; §22 must assert it fires for cookie-authenticated state-changing requests.
- `Secure` on every cookie means **cookie mode does not work over plain HTTP**, including local development against `http://localhost:5035`. Developers testing browser flows need the HTTPS profile.
- `SameSite=Strict` plus path scoping on the refresh cookie means browsers never attach the refresh token outside the refresh endpoint — narrowing its exposure to a single route.
- The cookie matrix is load-bearing security configuration. It belongs in typed options (`Configuration/CookieOptions.cs`, §4) validated at startup, not scattered across call sites.
