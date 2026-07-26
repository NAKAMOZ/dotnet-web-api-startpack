# ADR-0002: Session Lifetime and Multi-Device Session Model

- **Status:** Partially superseded by ADR-0026 — lifetime and session-model decisions stand
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, rows *Session lifetime* and *Session model*; **resolves pending decision P1**
- **Affects:** §4 (token architecture), §6 (entities), §12 (services), §17 (cleanup jobs)

## Context

Sessions need a lifetime policy that is neither hostile to users nor generous to attackers, and users need visibility into where they are logged in.

A single global session per user — the simplest model — means logging in on a phone silently kills the desktop session. That is the wrong behaviour for a system whose v1 scope already includes mobile, CLI, and browser clients.

Separately, the roadmap left the *value* of the absolute session cap open as pending decision **P1**.

## Decision

**Lifetime: two independent bounds, both of which must hold.** A session is valid while:

```
now < LastActiveAt + 6 hours     (sliding inactivity window)
AND
now < AbsoluteExpiresAt          (login time + 7 days)
```

**P1 is resolved: the absolute cap is 7 days.** Refreshing beyond either bound fails and forces a fresh login. A successful refresh slides `LastActiveAt`; it never extends `AbsoluteExpiresAt`.

**Model: one `Session` row per login, per device.** Each row records `IpAddress`, `UserAgent`, `DeviceLabel`, `LastActiveAt`, `AbsoluteExpiresAt`, `RevokedAt`, and `RevocationReason`. Three endpoints operate on them: list own sessions, revoke one, revoke all except the current.

**Original password-change decision:** both deliberate change and reset bumped
`User.SecurityStamp` and revoked all sessions. ADR-0026 supersedes this paragraph for a
deliberate authenticated change; password reset still revokes every session.

## Alternatives considered

**Sliding window only, no absolute cap.** A session used daily would live forever, and so would a refresh-token chain stolen from it. Rejected — an unbounded credential has no worst case.

**Absolute cap only, no sliding window.** Idle sessions would stay live for the full 7 days. The sliding window is what makes an abandoned session on a shared machine expire in hours rather than days.

**Cap values other than 7 days (P1).** 24 hours was considered and rejected as hostile to mobile clients, which would face daily re-authentication. 30 and 90 days were rejected as widening the exposure window of an undetected stolen refresh chain past the point the sliding window can compensate for. 7 days puts the worst case at one week while keeping re-login roughly weekly — and the 6-hour inactivity window still kills idle sessions long before the cap matters.

**Single global session per user.** Rejected: no multi-device support, and no way to answer "where am I logged in?" — which is itself a security feature, since it is how a user notices a session they did not create.

## Consequences

- Users can enumerate and individually revoke their sessions, making unauthorised access visible to the person best placed to spot it.
- Two expiry bounds mean two failure modes to test and to communicate: §22 must cover both, and the error responses must distinguish them so clients can tell "you were idle" from "your session aged out".
- Session rows accumulate. Expired and revoked rows need a cleanup worker (`BackgroundServices/`, per P9) or the table grows without limit.
- ADR-0026 preserves the current session after a deliberate password change and revokes
  its siblings; reset retains this ADR's revoke-every-session behaviour.
- The 7-day cap is the outer bound on any single credential chain's usefulness. It should be revisited if session-hijack telemetry ever suggests it is too generous.
- `AbsoluteExpiresAt` is written once at login and is immutable thereafter — implementations must not "helpfully" extend it on refresh, which would silently defeat the cap.
