# 17. Rate Limiting and Abuse Prevention

## Objective

Throttle credential-stuffing, token-grinding, and email-bombing without harming legitimate clients.

## Scope

Built-in ASP.NET Core `RateLimiter` policies, partition strategy, 429 semantics.

## Architectural Decisions

- Policies (named, in `Extensions/ServiceCollectionExtensions.RateLimiting.cs`):
  - `auth-strict` — fixed window, 10/min per IP: login, MFA verify, passkey auth complete, refresh.
  - `email-sending` — 5/hour per IP **and** 3/hour per target account: reset request, verification resend (per-account partition stops distributed email-bombing of one victim).
  - `registration` — 5/hour per IP.
  - `general` — sliding window, 100/min per user id (per IP when anonymous).
- Partition key: authenticated user id where available, else client IP honoring `ForwardedHeaders` config (§27) — never raw `X-Forwarded-For` trust.
- 429 responses: RFC 9457 body + `Retry-After`; rejections logged with partition key, audited on `auth-strict` (`rate_limit_exceeded` metadata on relevant events).
- Store: in-memory (P6) — correct for single node; Redis-backed limiter is a §29 item tied to P5 scale-out.

## Technology Decisions Requiring Approval

P6.

## Tasks

- [ ] `Configuration/RateLimitOptions.cs` (all numbers configurable, defaults as above).
- [ ] `Extensions/ServiceCollectionExtensions.RateLimiting.cs` + `[EnableRateLimiting("policy")]` attributes across controllers per the matrix above.
- [ ] 429 ProblemDetails integration (limiter `OnRejected` → §13 envelope).
- [ ] `Documentation/Security/RateLimits.md`: policy matrix (endpoint → policy → limits).

## Expected Deliverables

Options, extension, annotated controllers, policy matrix doc.

## Dependencies

§13 (envelope), §14 (pipeline position).

## Security Considerations

Per-account email-sending caps are the defense the per-IP limiter can't provide (botnets rotate IPs; the victim's mailbox is the fixed point). Limits apply before authentication runs — unauthenticated flood never reaches Argon2id verification (CPU-exhaustion defense).

## Testing Requirements

§21/§22: exceed each policy in-test → 429 + `Retry-After`; verify partition independence (two accounts, one IP; one account, two IPs).

## Documentation Requirements

RateLimits matrix; each endpoint doc names its policy.

## Definition of Done

All inventory endpoints carry an explicit policy (or documented `general` default); limit tests green.

## Questions for the Project Owner

1. Approve the default limit numbers, or provide expected client traffic profiles to tune against?
