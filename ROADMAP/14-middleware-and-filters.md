# 14. Middleware and Filters

## Objective

Cross-cutting pipeline: correlation IDs, centralized exception translation, security headers, audit filter — in an explicit, documented order.

## Scope

Middleware classes, filters, pipeline extension method.

## Architectural Decisions

Pipeline order (in `Extensions/ApplicationBuilderExtensions.Pipeline.cs`, documented inline):

1. `ForwardedHeadersMiddleware` (prod, §27)
2. `CorrelationIdMiddleware` — accept inbound `X-Correlation-Id` (validated format) or generate; response header; Serilog `LogContext` push
3. Serilog request logging
4. `ExceptionHandlingMiddleware` — typed exceptions → ProblemDetails via §13 map; unknown → 500 generic
5. HTTPS redirection / HSTS (non-dev)
6. `SecurityHeadersMiddleware` — `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy` minimal, restrictive `Content-Security-Policy` (API-appropriate: `default-src 'none'`; relaxed only on the Scalar route)
7. Rate limiter (§17)
8. CORS (allowlist from options, credentials only for cookie-mode origins)
9. Authentication → Authorization
10. Endpoints

Filters: `ValidationFilter` (§10) global; `Filters/AuditActionFilter.cs` on admin controllers (records actor, action, target, correlation ID via `IAuditLogger`); CSRF enforcement filter (§4) global with bearer exemption.

## Technology Decisions Requiring Approval

None.

## Tasks

- [x] `Middleware/CorrelationIdMiddleware.cs`, `SecurityHeadersMiddleware.cs`, `ExceptionHandlingMiddleware.cs` (as `IExceptionHandler`).
- [x] `Filters/CsrfProtectionFilter.cs` — plus `Services/Security/CsrfTokenService.cs`, which the filter cannot verify without. §12 wires `GET /api/v1/auth/csrf` to the same service.
- [x] `Filters/AuditActionFilter.cs` — deferred to §15, which defines `IAuditLogger`, and **landed there**. Writing it here would have meant defining that interface ahead of the workstream that owns it.
- [x] `Extensions/ApplicationBuilderExtensions.Pipeline.cs` with the ordered pipeline + ordering comment block.
- [x] `Configuration/ApiCorsOptions.cs` + `Handlers/Cors/OriginAwareCorsPolicyProvider.cs` + wiring in `AddPipelineServices`.

### Recorded deviations

- `CorsOptions` → **`ApiCorsOptions`**. `Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions` is used in the very file that configures ours — the same collision that renamed `CookieOptions` to `AuthCookieOptions`.
- `ExceptionHandlingMiddleware` is an `IExceptionHandler`, not a middleware class. The file name is the roadmap's; the shape is what the roadmap's own parenthetical asks for.
- The CSRF tag is produced by an `ITimeLimitedDataProtector` rather than a raw HMAC. Same authenticated binding, no new secret to distribute — see `Documentation/Architecture/Pipeline.md` §6.
- CORS needs a custom `ICorsPolicyProvider`: `AllowCredentials` is a property of a built policy, so "credentials only for cookie-mode origins" is not expressible in a single policy.
- `AuditActionFilter` is registered **globally and opts in by attribute** (`[AuditEvent(AuditEventType.X)]`), not applied to admin controllers by hand. Same reasoning as the CSRF filter: a per-controller filter is one a new controller forgets, and an action that is silently never audited looks exactly like an action nobody performed.

## Expected Deliverables

3 middleware files, 2 filters, pipeline extension.

## Dependencies

§13 (map), §4 (CSRF design).

## Security Considerations

Order is load-bearing: correlation before exception handling (errors carry IDs); rate limiting before auth (unauthenticated abuse throttled); CSRF filter must run after authentication (needs to know the scheme used).

## Testing Requirements

§21: header assertions on every response (security headers, correlation echo); CSRF matrix (cookie mode without header → 403; bearer mode without header → 200).

## Documentation Requirements

`Documentation/Architecture/Pipeline.md`: ordered diagram + rationale per stage.

## Definition of Done

Pipeline assembled solely via the extension method; header/CSRF tests green; `Program.cs` unchanged in size.

## Questions for the Project Owner

None.
