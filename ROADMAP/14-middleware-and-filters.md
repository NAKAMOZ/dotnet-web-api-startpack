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

- [ ] `Middleware/CorrelationIdMiddleware.cs`, `SecurityHeadersMiddleware.cs`, `ExceptionHandlingMiddleware.cs` (as `IExceptionHandler`).
- [ ] `Filters/AuditActionFilter.cs`, `Filters/CsrfProtectionFilter.cs`.
- [ ] `Extensions/ApplicationBuilderExtensions.Pipeline.cs` with the ordered pipeline + ordering comment block.
- [ ] `Configuration/CorsOptions.cs` + wiring.

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
