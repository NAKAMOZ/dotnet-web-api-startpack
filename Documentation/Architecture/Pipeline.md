# Request Pipeline

**Status:** Written 2026-07-23 · **Workstream:** §14 · **Source of truth for:** middleware order, security headers, CORS, CSRF enforcement

The pipeline is assembled in exactly one place: `Extensions/ApplicationBuilderExtensions.Pipeline.cs`. `Program.cs` calls `app.UseApiPipeline()` and nothing else.

**Order is load-bearing.** Every position below is a decision, not a convention, and three of them are security properties rather than preferences. §22 asserts the observable consequences.

---

## 1. The order

```text
   ┌─ request ──────────────────────────────────────────────────────────────┐
   │                                                                        │
 1 │  ForwardedHeaders            when ReverseProxy:Enabled (§27)           │
 2 │  CorrelationIdMiddleware     adopt or mint X-Correlation-Id            │
 3 │  Serilog request logging     (§15)                                     │
 4 │  UseExceptionHandler         → ExceptionHandlingMiddleware (§13 map)   │
   │  UseStatusCodePages          bodies for bare status codes (§13)        │
   │  MapOpenApi / Scalar         .AllowAnonymous() (§18)                   │
   │  /health/live, /health/ready plain text, anonymous (§28)               │
 5 │  UseHsts / UseHttpsRedirection                                         │
 6 │  SecurityHeadersMiddleware   response-starting callbacks               │
 7 │  Rate limiter                (§17)                                     │
 8 │  UseCors                     OriginAwareCorsPolicyProvider             │
 9 │  UseAuthentication → UseAuthorization                                  │
10 │  MapControllers                                                        │
   │      └── MVC filters: CsrfProtectionFilter → ValidationFilter → action │
   └────────────────────────────────────────────────────────────────────────┘
```

| # | Stage | Why here |
|---|---|---|
| 1 | Forwarded headers | Everything below that reads a scheme or a client IP — HTTPS redirection, rate limiting, audit rows — reads the proxy's values otherwise. Runs when `ReverseProxy:Enabled`, which `ReverseProxyOptionsValidator` requires outside Development and Testing |
| 2 | Correlation id | Above the exception handler, so a failure anywhere below still carries an id |
| 3 | Request logging | After the id so the log line carries it; above the handler so 500s are logged too |
| 4 | Exception handling | Everything below is covered; the two stages above are the two that cannot meaningfully throw |
| 5 | HSTS / HTTPS redirect | A redirect is a normal response, not an error path |
| 6 | Security headers | Registered as `OnStarting` callbacks — see §3 |
| 7 | Rate limiting | **Before authentication**, so an unauthenticated flood is throttled before it costs a database read or an Argon2 verification |
| 8 | CORS | **Before authentication**: a preflight `OPTIONS` carries no credentials by design and would answer 401 behind deny-by-default — which a browser reports as an opaque CORS failure |
| 9 | Authentication → authorization | Identity is established before it is judged. Reversed, every check runs against an anonymous principal and denies everything |
| 10 | Endpoints | The CSRF filter runs as an MVC **authorization filter**, after authentication, because it must know which scheme the request used |

The documentation and health endpoints are mapped between stages 4 and 5 rather than with
`MapControllers`. They are not controllers, so deny-by-default covers them and each needs
an explicit `.AllowAnonymous()`; mapping them above HSTS keeps an orchestrator's plain-HTTP
probe from being answered with a redirect.

**The health probes are the one recorded exemption from RFC 9457.** `/health/live` and
`/health/ready` answer `text/plain` — `Healthy` or `Unhealthy` — because that is what
orchestrator probes read, and a probe is not an API client. Nothing else in the surface may
answer a non-2xx without a Problem Details body and a stable `errorCode`; see
`Documentation/Errors.md` §1.

---

## 2. Correlation ids

`Middleware/CorrelationIdMiddleware.cs`, constants and policy in `Middleware/CorrelationId.cs`.

- Inbound `X-Correlation-Id` is **adopted, never trusted**. It must be 1–64 characters of `[A-Za-z0-9._-]`; anything else is silently replaced with a fresh id.
- Silently, not with a 400: rejecting turns the header into a probe that reports what the server parses.
- The bound is not cosmetic. This value reaches log lines, audit rows and response bodies — unbounded, it is a free channel into the log pipeline.
- The resolved id lives at `HttpContext.Items["CorrelationId"]`, is echoed on the response, and appears in every Problem Details body as `correlationId`.

`CorrelationIdEnricher` attaches the resolved value to Serilog events. It is an enricher
rather than a `LogContext` push so its sibling `UserIdEnricher` can resolve the principal
after authentication without splitting request context across two different mechanisms.

---

## 3. Security headers

`Middleware/SecurityHeadersMiddleware.cs`.

| Header | Value | Closes |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | A JSON body echoing user input being sniffed into HTML |
| `Referrer-Policy` | `no-referrer` | API URLs (ids, tokens in links) leaking through `Referer` |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` | Everything. This origin serves JSON and loads nothing |
| `X-Frame-Options` | `DENY` | Clickjacking on clients that ignore `frame-ancestors` |
| `Cross-Origin-Resource-Policy` | `same-origin` | Cross-origin embedding of responses (Spectre-class reads) |
| `Permissions-Policy` | camera, microphone, geolocation … `=()` | Capabilities this origin will never use |
| `Cache-Control` | `no-store` | An intermediary caching a caller-specific — sometimes show-once — response |

**Both this middleware and the correlation id write their headers from a `Response.OnStarting` callback, and that is not incidental.** `UseExceptionHandler` clears the response — status, headers and body — before writing the problem document. Headers written on the way in would therefore be present on every 2xx and missing on every 5xx: exactly the responses an attacker probes and a support engineer reads. `OnStarting` callbacks are held by the response feature, survive the clear, and run immediately before the first byte. `PipelineTests.AnUnhandledExceptionBecomesAProblemDocumentWithTheHeadersIntact` fails if this is "simplified".

The **only** relaxation is the `/scalar` prefix (§18), which gets a `'self'` policy with inline script and style. It contains **no CDN host**: a default Scalar setup that loads its bundle from jsdelivr will render blank, which is the intended failure — adding a third-party script host to a CSP is an §18 decision with an ADR behind it, not something this middleware pre-approves.

---

## 4. Exception handling

`Middleware/ExceptionHandlingMiddleware.cs` — an `IExceptionHandler`, despite the file name the roadmap gives it. `UseExceptionHandler` already owns the hard parts (pipeline state, not double-writing a started response, re-throwing when unclaimed); reimplementing them is how a pipeline acquires a second, subtly different error path.

It maps nothing itself. `Exceptions/ExceptionToProblemDetailsMap.cs` is the single table (§13); this class decides only how the result is logged and written.

| Case | Behaviour |
|---|---|
| Status ≥ 500 | Logged with the exception and the correlation id |
| Status < 500 | Logged at information level, **without** a stack trace — a trace for "that email is already registered" is noise that hides real faults |
| `499` (client gone) | Debug log, status set, **no body written** — the socket is closed, and a cancelled request must not inflate the error rate |
| Response already started | Logged, then re-thrown: an honest truncated response beats a valid-looking body appended to a half-written one |

The body is written through `IProblemDetailsService`, never serialized directly — that is what runs §13's `CustomizeProblemDetails`, which attaches `correlationId` and `traceId` and strips the framework's `exception` extension outside Development.

---

## 5. CORS

`Configuration/ApiCorsOptions.cs` (section `Cors`), `Handlers/Cors/OriginAwareCorsPolicyProvider.cs`.

Two allowlists:

| Option | Meaning |
|---|---|
| `AllowedOrigins` | May call with a **bearer token**. Never receives `Access-Control-Allow-Credentials` |
| `CookieModeOrigins` | May call in **cookie mode**. The only origins that receive `Access-Control-Allow-Credentials: true` |

Both default to empty, so an unconfigured deployment allows no cross-origin browser call at all. CORS constrains browsers, not servers — a server-to-server client needs no entry.

**Why a custom `ICorsPolicyProvider` and not one policy.** `AllowCredentials` is a property of a built `CorsPolicy`, not of an origin, so one policy must answer identically for every origin in its allowlist. Merging the lists would emit `Access-Control-Allow-Credentials: true` to bearer-mode origins as well — harmless the day it ships, and precisely the header an XSS on one of those origins needs to start making authenticated calls with the victim's cookies. The provider picks the credentialed policy only when the `Origin` matches `CookieModeOrigins` exactly (ordinal — the browser compares byte for byte too).

Methods and headers are enumerated rather than wildcarded; `X-Correlation-Id` is in `ExposedHeaders`, because a front end that cannot read it cannot put it in a bug report.

---

## 6. CSRF enforcement

`Filters/CsrfProtectionFilter.cs` (global authorization filter), `Services/Security/CsrfTokenService.cs`. Design: [Authentication.md §3](Authentication.md).

A request is challenged when **all** of these hold:

1. The method is not `GET`/`HEAD`/`OPTIONS`/`TRACE`.
2. The caller is authenticated.
3. The request authenticated **by cookie** — `HttpContext.Items` carries `AuthTransport.CookieAuthenticatedItemKey`, set by `ConfigureJwtBearerOptions` at the moment it reads the access cookie.

Then both halves must pass:

- **Double submit** — `X-CSRF-Token` equals the `__Host-auth.csrf` cookie, compared in constant time. Proves the caller can read a cookie for this origin.
- **Session binding** — the token's tag authenticates the pair `(sessionId, nonce)` against the `sid` claim of the request. Proves the token was minted for *this* session.

Binding is what makes this more than a double submit: an attacker who can write a cookie for the site — a compromised sibling subdomain is enough, since cookies ignore port and scheme boundaries — can set both halves to a value they control. Their token is authentic; it is just not this session's.

Failure is `403` with `errorCode: csrf_validation_failed`, distinct from `forbidden` because the client should fetch a token and retry rather than give up.

> **The exemption is the dangerous half.** Widening it — exempting by default, or inferring "no `Authorization` header, therefore bearer" — disables CSRF protection API-wide while every happy-path test stays green. §22 asserts the filter fires for cookie-authenticated state-changing requests.

### Recorded deviation from Authentication.md §3

The document writes the tag as `HMAC(key, sessionId || nonce)`. The implementation produces the same authenticated binding through an `ITimeLimitedDataProtector` — encrypt-then-MAC over the same payload — because of key management, not cryptography. A raw HMAC needs a secret that must be configured, distributed to every instance and rotated by hand, and the obvious shortcut of generating one per process breaks the moment a second instance exists, as intermittent CSRF failures under load. Data Protection already provides a shared, rotating, ADR-0020-protected key ring, and token expiry comes free. Token shape (`base64url(nonce).base64url(tag)`) and the session-binding property are unchanged.

---

## 7. Filters

| Filter | Scope | Stage |
|---|---|---|
| `CsrfProtectionFilter` | Global | Authorization — before model binding, so a forged request is rejected before its body is read |
| `ValidationFilter` (§10) | Global | Action — the single producer of `400`s |
| `AuditActionFilter` | Global, opt-in by `[AuditEvent]` | Action — writes only after a successful attributed action |
| `EmailTargetRateLimitFilter` | Global, active only for `email-sending` | Action after validation — enforces the per-target-account half |

The global filters are registered through `MvcOptions` in `AddPipelineServices`, not on
individual controllers: a filter applied per controller is one a new controller forgets,
and the omission is invisible until someone exploits it. The email filter resolves as a
singleton because its in-memory partitions must survive across requests.

---

## 8. What §14 left for later

| Item | Owner | Reason |
|---|---|---|
| Feature-service audit producers | §12/§15 | Most catalog events have no service path yet |
| Audit retention worker | §12/§15 | Implemented by the shared bounded cleanup worker with the approved 90-day period |

Serilog request logging/enrichers, the rate limiter, `AuditActionFilter`, Scalar's
self-hosted bundle/CSP relaxation, and `ForwardedHeadersMiddleware` with its validated
proxy allowlist have now landed in their owning workstreams.
