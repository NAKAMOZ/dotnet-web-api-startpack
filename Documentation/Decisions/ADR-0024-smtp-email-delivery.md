# ADR-0024: SMTP email delivery with Mailpit in development

- **Status:** Accepted
- **Date:** 2026-07-26
- **Workstreams:** §12, §24
- **Resolves:** P8

## Context

Registration, verification and password reset need email without making HTTP latency or
success reveal whether an account exists. Development also needs a deterministic local
inbox. No v1 feature requires a provider-specific API.

## Decision

Keep delivery behind `IEmailSender`, render embedded HTML templates, and enqueue messages
to a bounded in-process worker. Deliver through SMTP using `EmailOptions`. Development uses
Mailpit on port 1025; production changes connection settings and credentials only.
Responses never wait for SMTP and never depend on send success.

## Alternatives considered

- Provider SDK: rejected because it couples the application to a vendor without adding a
  required capability.
- Synchronous SMTP: rejected because latency and failures become observable enumeration
  signals.
- Durable broker/outbox: deferred; v1 has no broker and email loss during process failure is
  an accepted limitation.

## Consequences

- Mail delivery is provider-neutral and locally inspectable.
- A process crash can lose queued mail; a future reliability requirement adds a durable
  outbox behind the same interface.
- A full queue preserves messages already accepted and emits a Critical overflow event;
  incoming overflow is discarded without blocking a request or evicting older mail.
- Queue logs exclude recipients, bodies, tokens and secrets, including overflow logs.
