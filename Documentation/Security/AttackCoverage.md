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
| Sensitive-object log redaction | `SensitiveDataDestructuringPolicyTests.*` | Covered at logger boundary |
| Recent auth reads `auth_time`, not `iat`; API keys cannot pass | `RecentAuthAuthorizationHandlerTests.*` | Covered at policy boundary |
| Sort fields are allow-listed | `QueryValidatorTests.*` and `AuditQueryService` fixed expression map | Covered at validator/unit boundary |
| Registration/reset/login enumeration equality and timing | `FeatureServiceIntegrationTests.Registration_DuplicateIsByteIdenticalAndCreatesOneAccount`; `FeatureAttackTests.EnumerationPaths_ExposeTheSamePublicOutcomes` | Covered |
| Lockout over HTTP, reset on login success, admin unlock | `FeatureAttackTests.Lockout_IsInvisibleAndAdminUnlockRestoresLogin` | Covered |
| Refresh rotation/replay over HTTP | `FeatureServiceIntegrationTests.LoginRefreshAndReplay_RunThroughTheHttpContract` | Covered |
| Every admin endpoint as User → 403; every protected endpoint anonymous → 401 | `AuthorizationAttackTests.*` (8 admin + 30 protected route cases) | Covered |
| API-key scope enforcement and current-role intersection | `FeatureAttackTests.ApiKeyScopes_AreIntersectedWithTheOwnersCurrentRoles` | Covered |
| Oversized request bodies | Global request-size policy not yet defined (§16/§27) | Blocked |
| Full-flow scalar secret scan over captured logs | Capturing sink not yet present | Blocked |

“Blocked” is intentionally not represented by skipped or trivially green tests. Each row
becomes an executable named test in the same change that supplies its production service.
