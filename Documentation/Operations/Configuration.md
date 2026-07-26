# Configuration Reference

Configuration precedence is the ASP.NET Core default:

`appsettings.json` → `appsettings.{Environment}.json` → Development user-secrets →
environment variables → command-line arguments.

Committed settings contain safe defaults only. Development secrets use user-secrets;
containers and current production planning use environment variables. P7/P14 still block a
provider-specific vault choice.

## Required infrastructure

| Key | Type / default | Secret | Environment variable |
|---|---|---:|---|
| `ConnectionStrings:Postgres` | connection string; **required** | Yes | `ConnectionStrings__Postgres` |

The process fails during service registration when the connection string is absent. It
fails during host startup when any validated option is incomplete or inconsistent.

## Authentication and sessions

| Key | Type / default | Secret | Environment variable |
|---|---|---:|---|
| `Jwt:Issuer` | absolute URI / `https://localhost:7052` | No | `Jwt__Issuer` |
| `Jwt:Audience` | string / `dotnet-web-api-startpack` | No | `Jwt__Audience` |
| `Jwt:AccessTokenLifetime` | duration / `00:15:00` | No | `Jwt__AccessTokenLifetime` |
| `Jwt:ClockSkew` | duration / `00:00:30` | No | `Jwt__ClockSkew` |
| `Jwt:Algorithm` | fixed / `ES256` | No | `Jwt__Algorithm` |
| `Jwt:KeyRetirementGrace` | duration / `00:20:00` | No | `Jwt__KeyRetirementGrace` |
| `Session:InactivityWindow` | duration / `06:00:00` | No | `Session__InactivityWindow` |
| `Session:AbsoluteLifetime` | duration / `7.00:00:00` | No | `Session__AbsoluteLifetime` |
| `Session:RecentAuthenticationWindow` | duration / `00:05:00` | No | `Session__RecentAuthenticationWindow` |
| `Session:MfaTicketLifetime` | duration / `00:05:00` | No | `Session__MfaTicketLifetime` |
| `Session:WebAuthnChallengeLifetime` | duration / `00:05:00` | No | `Session__WebAuthnChallengeLifetime` |
| `AuthCookies:AccessCookieName` | string / `__Host-auth.access` | No | `AuthCookies__AccessCookieName` |
| `AuthCookies:RefreshCookieName` | string / `__Secure-auth.refresh` | No | `AuthCookies__RefreshCookieName` |
| `AuthCookies:CsrfCookieName` | string / `__Host-auth.csrf` | No | `AuthCookies__CsrfCookieName` |
| `AuthCookies:RefreshCookiePath` | path / `/api/v1/auth/refresh` | No | `AuthCookies__RefreshCookiePath` |
| `AuthCookies:TransportHeaderName` | string / `X-Auth-Transport` | No | `AuthCookies__TransportHeaderName` |
| `AuthCookies:CsrfHeaderName` | string / `X-CSRF-Token` | No | `AuthCookies__CsrfHeaderName` |
| `AuthCookies:RequireSecure` | bool / `true` | No | `AuthCookies__RequireSecure` |

Cross-field rules pin ES256, require a retirement grace at least access lifetime plus
clock skew, require inactivity shorter than the absolute cap, and enforce cookie prefixes
and endpoint-scoped refresh paths.

## Passwords, lockout, and abuse controls

| Key | Type / default | Secret | Environment variable |
|---|---|---:|---|
| `PasswordHashing:PasswordMemoryKib` | int / `65536` | No | `PasswordHashing__PasswordMemoryKib` |
| `PasswordHashing:PasswordIterations` | int / `3` | No | `PasswordHashing__PasswordIterations` |
| `PasswordHashing:PasswordParallelism` | int / `1` | No | `PasswordHashing__PasswordParallelism` |
| `PasswordHashing:SecretMemoryKib` | int / `8192` | No | `PasswordHashing__SecretMemoryKib` |
| `PasswordHashing:SecretIterations` | int / `1` | No | `PasswordHashing__SecretIterations` |
| `PasswordHashing:SecretParallelism` | int / `1` | No | `PasswordHashing__SecretParallelism` |
| `PasswordHashing:SaltLength` | int / `16` | No | `PasswordHashing__SaltLength` |
| `PasswordHashing:HashLength` | int / `32` | No | `PasswordHashing__HashLength` |
| `Lockout:MaxFailedAttempts` | int / `5` | No | `Lockout__MaxFailedAttempts` |
| `Lockout:LockoutDuration` | duration / `00:15:00` | No | `Lockout__LockoutDuration` |
| `Lockout:Enabled` | bool / `true` | No | `Lockout__Enabled` |
| `RateLimiting:AuthStrictPermitLimit` | int / `10` | No | `RateLimiting__AuthStrictPermitLimit` |
| `RateLimiting:AuthStrictWindow` | duration / `00:01:00` | No | `RateLimiting__AuthStrictWindow` |
| `RateLimiting:EmailSendingIpPermitLimit` | int / `5` | No | `RateLimiting__EmailSendingIpPermitLimit` |
| `RateLimiting:EmailSendingIpWindow` | duration / `01:00:00` | No | `RateLimiting__EmailSendingIpWindow` |
| `RateLimiting:EmailSendingAccountPermitLimit` | int / `3` | No | `RateLimiting__EmailSendingAccountPermitLimit` |
| `RateLimiting:EmailSendingAccountWindow` | duration / `01:00:00` | No | `RateLimiting__EmailSendingAccountWindow` |
| `RateLimiting:RegistrationPermitLimit` | int / `5` | No | `RateLimiting__RegistrationPermitLimit` |
| `RateLimiting:RegistrationWindow` | duration / `01:00:00` | No | `RateLimiting__RegistrationWindow` |
| `RateLimiting:GeneralPermitLimit` | int / `100` | No | `RateLimiting__GeneralPermitLimit` |
| `RateLimiting:GeneralWindow` | duration / `00:01:00` | No | `RateLimiting__GeneralWindow` |
| `RateLimiting:GeneralSegmentsPerWindow` | int / `6` | No | `RateLimiting__GeneralSegmentsPerWindow` |

The password and machine-secret profiles are deliberately separate. See the Argon2 tuning
procedure before changing either.

## Email and social providers

| Key | Type / default | Secret | Environment variable |
|---|---|---:|---|
| `Email:Host` | string / `localhost` | No | `Email__Host` |
| `Email:Port` | int / `1025` | No | `Email__Port` |
| `Email:FromAddress` | email / `auth@localhost.dev` | No | `Email__FromAddress` |
| `Email:UseTls` | bool / `false` | No | `Email__UseTls` |
| `Email:Username` | nullable string | Usually | `Email__Username` |
| `Email:Password` | nullable string | **Yes** | `Email__Password` |
| `SocialProviders:{Google|GitHub}:Enabled` | bool / `false` | No | `SocialProviders__Google__Enabled`, `SocialProviders__GitHub__Enabled` |
| `SocialProviders:{Google|GitHub}:ClientId` | nullable string | Treat as sensitive | `SocialProviders__Google__ClientId`, `SocialProviders__GitHub__ClientId` |
| `SocialProviders:{Google|GitHub}:ClientSecret` | nullable string | **Yes** | `SocialProviders__Google__ClientSecret`, `SocialProviders__GitHub__ClientSecret` |

Enabling a social provider without both credentials fails startup.

## CORS, cleanup, proxy trust, and telemetry

| Key | Type / default | Secret | Environment variable |
|---|---|---:|---|
| `Cors:AllowedOrigins` | string array / empty | No | `Cors__AllowedOrigins__0`, … |
| `Cors:CookieModeOrigins` | string array / empty | No | `Cors__CookieModeOrigins__0`, … |
| `Cors:AllowedHeaders` | string array / documented app headers | No | `Cors__AllowedHeaders__0`, … |
| `Cors:ExposedHeaders` | string array / correlation + retry | No | `Cors__ExposedHeaders__0`, … |
| `Cors:PreflightMaxAge` | duration / `00:10:00` | No | `Cors__PreflightMaxAge` |
| `Cleanup:Interval` | duration / `01:00:00` | No | `Cleanup__Interval` |
| `Cleanup:AuditRetention` | duration / `90.00:00:00` | No | `Cleanup__AuditRetention` |
| `Cleanup:BatchSize` | int / `1000` | No | `Cleanup__BatchSize` |
| `ReverseProxy:Enabled` | bool / `false`; required outside Development/Testing | No | `ReverseProxy__Enabled` |
| `ReverseProxy:ForwardLimit` | int / `1` | No | `ReverseProxy__ForwardLimit` |
| `ReverseProxy:KnownProxies` | IP array / empty | No | `ReverseProxy__KnownProxies__0`, … |
| `ReverseProxy:KnownNetworks` | CIDR array / empty | No | `ReverseProxy__KnownNetworks__0`, … |
| `Telemetry:ServiceName` | string / `dotnet-web-api-startpack` | No | `Telemetry__ServiceName` |
| `Telemetry:OtlpExporterEnabled` | bool / `false` | No | `Telemetry__OtlpExporterEnabled` |
| `Telemetry:OtlpEndpoint` | absolute HTTP(S) URI / unset | No | `Telemetry__OtlpEndpoint` |

CORS accepts exact HTTP(S) origins only. Wildcards, paths, query strings, and fragments
fail startup. Cleanup options are bound now; the §12 maintenance worker remains pending.

Production-like environments fail startup until forwarded headers are enabled with at least
one exact proxy IP or CIDR. The framework defaults are cleared before this allowlist is
applied; an empty list never means trust all. OTLP export is similarly fail-fast: enabling it
without an absolute endpoint is invalid. Exporter authentication headers, if introduced by
P10, are secrets even though the endpoint itself is not.

## Secret channels

Development:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "<local connection string>"
dotnet user-secrets set "SocialProviders:Google:ClientSecret" "<secret>"
```

Compose uses only the obvious fake password in `.env.example`; `.env` is ignored. Current
production planning uses platform-injected environment variables. Never place a secret in
an `appsettings*.json`, `.http`, workflow, Compose file, or command committed to Git.

The CI pattern scan catches common private-key and provider-token formats. It is a tripwire,
not a substitute for review or provider-side secret scanning.
