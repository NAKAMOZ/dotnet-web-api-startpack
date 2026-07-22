# 25. Configuration and Secret Management

## Objective

Every tunable in a typed, validated options class; every secret outside the repo; fail-fast on misconfiguration.

## Scope

Options classes, validation-on-start, configuration layering, secret channels per environment.

## Architectural Decisions

- One options class per concern in `Configuration/` (one file each): `JwtOptions`, `SessionOptions`, `AuthCookieOptions`, `PasswordHashingOptions`, `LockoutOptions`, `RateLimitOptions`, `EmailOptions`, `SocialProviderOptions` (per-provider client id/secret), `CorsOptions`, `CleanupOptions`.
- All registered via `AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`; cross-field rules via `IValidateOptions<T>` implementations (e.g. access TTL < sliding window < absolute cap).
- Layering: `appsettings.json` (safe defaults, no secrets) → `appsettings.{Environment}.json` → user-secrets (Development) → environment variables (containers/prod). Secret channel per environment documented; committed appsettings files contain placeholder-free structure only.
- Prod secret store: env vars now, vault decision deferred with P7/P14.

## Technology Decisions Requiring Approval

P7.

## Tasks

- [ ] 10 options files + `IValidateOptions` classes for cross-field rules.
- [ ] `Extensions/ServiceCollectionExtensions.Options.cs` registering all with `ValidateOnStart`.
- [ ] `dotnet user-secrets init`; `Documentation/Operations/Configuration.md`: full key reference (key, type, default, secret? yes/no, env var name).
- [ ] Startup misconfiguration test (§21): missing signing-key config → host fails to start with a clear message.
- [ ] Sweep: no connection strings, client secrets, or keys in any committed file (CI grep gate for known patterns).

## Expected Deliverables

`Configuration/` complete, options extension, config reference doc, CI secret-pattern gate.

## Dependencies

Consumed by nearly every workstream; lands incrementally from §4 onward, consolidated here.

## Security Considerations

`ValidateOnStart` turns config drift into deploy-time failure instead of runtime auth bypass (e.g. an empty JWT audience would otherwise validate everything). Secret-pattern CI gate is a tripwire, not a guarantee — review remains the control.

## Testing Requirements

Unit tests for each `IValidateOptions`; startup-failure integration test.

## Documentation Requirements

Configuration reference doc as above.

## Definition of Done

App refuses to start with incomplete/inconsistent config; reference doc covers every key; no secret material in git history.

## Questions for the Project Owner

1. Confirm env-vars-now / vault-later (P7), or name a vault target today?
