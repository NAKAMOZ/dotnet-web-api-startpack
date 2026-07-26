# Rate limits

All limits are configured under `RateLimiting` and use ASP.NET Core's in-memory rate-limiter
primitives. Queues are disabled: excess work is rejected immediately with RFC 9457
`rate_limited`, status `429`, and `Retry-After`.

| Policy | Endpoints | Partition | Default |
|---|---|---|---|
| `auth-strict` | login, MFA login, refresh, passkey authentication complete | client IP | 10 per minute |
| `email-sending` | password-reset request, verification resend | client IP and target account | 5 per IP per hour; 3 per account per hour |
| `registration` | registration | client IP | 5 per hour |
| `general` | every endpoint, as the global default | authenticated subject when already established; otherwise client IP | 100 per minute, six sliding segments |

The middleware half runs before authentication so credential floods are stopped before a
database lookup or Argon2id verification. Consequently, ordinary API traffic partitions by
client IP at that stage; a principal set by trusted upstream middleware may use its subject.
The email target-account half is an MVC filter after authentication and validation, where it
can use the verified subject or normalized request address. Addresses are SHA-256 hashed
before becoming limiter keys and never written to rejection logs.

`RemoteIpAddress` is the only IP input. Raw `X-Forwarded-For` is never trusted. Production
must configure forwarded headers with known proxies (§27) before rate limiting; otherwise all
traffic behind a proxy intentionally shares the proxy's partition instead of accepting
caller-forged addresses.

The store is process-local. These limits are correct for the approved single-node deployment.
A multi-node deployment must replace them with a distributed limiter before scale-out, or
each node independently grants the full allowance.
