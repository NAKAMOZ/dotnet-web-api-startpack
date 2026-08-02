# ADR-0030: No message broker in v1

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** Project owner, through the explicit implementation directive
- **Source:** Resolves **P11**
- **Affects:** §12, §27, §29

## Context

All v1 operations are request/response, database transactions, SMTP delivery or bounded
in-process maintenance. No cross-service event consumer exists. Introducing a broker would
create delivery, retry, poison-message and operating semantics without a producer/consumer
contract that needs them.

## Decision

Deploy no message broker in v1. SMTP delivery remains behind `IEmailSender`; authentication
cleanup remains a `BackgroundService`. Any future webhook or multi-service event flow must
start with an outbox and a new ADR rather than reuse an unowned broker.

## Alternatives considered

- Azure Service Bus now: rejected as speculative infrastructure.
- Redis Pub/Sub: rejected because Redis is cache/rate-limit state, not a durable event log.
- Fire-and-forget tasks: rejected; they have weaker delivery behavior than either current
  synchronous work or a designed queue.

## Consequences

- The Azure topology has one fewer failure mode and no implied eventual-consistency contract.
- A future need for durable asynchronous delivery is new scope, not a configuration switch.
