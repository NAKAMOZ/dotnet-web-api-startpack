# Attack Coverage

Concrete regression coverage for the design claims in
[`Authentication.md` §16](../Architecture/Authentication.md). Security tests carry the
`Category=Security` trait and run in the normal integration job.

| Attack / claim | Executable test | Status |
|---|---|---|
| `alg: none` rejected | `JwtAttackTests.JwtAttack_IsRejected(alg-none)` | Covered |
| HS256 with published EC key rejected | `JwtAttackTests.JwtAttack_IsRejected(algorithm-confusion)` | Covered |
| Tampered payload rejected | `JwtAttackTests.JwtAttack_IsRejected(tampered-payload)` | Covered |
| Expired token rejected through shared-clock advance | `JwtAttackTests.ExpiredToken_IsRejectedAfterAdvancingTheSharedClock` | Covered |
| Wrong issuer / audience rejected | `JwtAttackTests.JwtAttack_IsRejected(wrong-issuer/wrong-audience)` | Covered |
| Unknown `kid` rejected without fallback | `JwtAttackTests.JwtAttack_IsRejected(unknown-kid)` | Covered |
| Retiring key validates; retired `kid` does not | `JwtAttackTests.RetiringKey_ValidatesUntilGraceThenRetiredKidIsRejected` | Covered |
| Refresh replay revokes the session, logs out the successor holder, and audits | `TokenServiceIntegrationTests.RefreshRotation_ReplayRevokesSessionAndAuditsBothEvents` | Covered |
| Idle and absolute refresh bounds | `TokenServiceIntegrationTests.RefreshRotation_EnforcesIdleAndAbsoluteSessionBounds` | Covered at service boundary |
| Missing/wrong/cross-session CSRF token rejected | `CsrfAttackTests.CookieStateChange_RequiresMatchingTokenBoundToAuthenticatedSession` | Covered |
| Correlation-header injection | `PipelineTests.AMalformedInboundCorrelationIdIsReplaced` | Covered |
| Rate-limit exhaustion | `RateLimitingTests.*` | Covered |
| Sensitive-object log redaction | `SensitiveDataDestructuringPolicyTests.*`; `LogRedactionAttackTests.FullCredentialFlow_LeaksNoIssuedOrPresentedSecretIntoLogs` | Covered at logger boundary and over real credential flows |
| Recent auth reads `auth_time`, not `iat`; API keys cannot pass | `RecentAuthAuthorizationHandlerTests.*` | Covered at policy boundary |
| Sort fields are allow-listed | `QueryValidatorTests.*` and `AuditQueryService` fixed expression map | Covered at validator/unit boundary |
| Registration/reset/login enumeration equality and timing | `FeatureServiceIntegrationTests.Registration_DuplicateIsByteIdenticalAndCreatesOneAccount`; `FeatureAttackTests.EnumerationPaths_ExposeTheSamePublicOutcomes` | Covered |
| Lockout over HTTP, reset on login success, admin unlock | `FeatureAttackTests.Lockout_IsInvisibleAndAdminUnlockRestoresLogin` | Covered |
| Refresh rotation/replay over HTTP | `FeatureServiceIntegrationTests.LoginRefreshAndReplay_RunThroughTheHttpContract` | Covered |
| TOTP/recovery replay, including concurrent TOTP reuse | `MfaServiceIntegrationTests.TotpAndRecoveryCodes_AreSingleUseIncludingConcurrentReplay` | Covered against PostgreSQL atomic update |
| WebAuthn challenge replay and cloned/decreasing signature counter | `PasskeyCeremonyIntegrationTests.SoftwareAuthenticator_RegistersAuthenticatesRejectsReplayAndDeletes` | Covered with a software ES256 authenticator |
| Every admin endpoint as User → 403; every protected endpoint anonymous → 401 | `AuthorizationAttackTests.*` (8 admin + 30 protected route cases) | Covered |
| API-key scope enforcement and current-role intersection | `FeatureAttackTests.ApiKeyScopes_AreIntersectedWithTheOwnersCurrentRoles` | Covered |
| Oversized request bodies | `InputAbuseAttackTests.OversizedBody_IsRejectedWithProblemDetailsBeforeParsing`; CI Compose smoke sends an oversized chunked body | Covered by the streamed/known-length 64 KiB limit |
| Full-flow scalar secret scan over captured logs | `LogRedactionAttackTests.FullCredentialFlow_LeaksNoIssuedOrPresentedSecretIntoLogs` | Covered with the integration capturing sink |

Every cataloged row has an executable named test; none is represented by a skip or a
trivially-green placeholder.
