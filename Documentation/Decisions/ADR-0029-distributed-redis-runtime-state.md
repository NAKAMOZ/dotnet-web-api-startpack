# ADR-0029: Azure Managed Redis for distributed cache and rate limits

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** Project owner, through the explicit implementation directive
- **Source:** Resolves **P6**; supersedes the in-memory-only boundary in ADR-0016
- **Affects:** §12, §17, §23, §25, §27, §28, §29

## Context

The 50 RPS login profile demonstrated that Argon2 capacity requires horizontal replicas.
ADR-0016 deliberately named a second active node as the trigger for a Redis backplane. With
multiple replicas, in-memory rate counters multiply an attacker's allowance by replica count
and cache invalidation does not propagate across the fleet.

Azure Cache for Redis is in retirement; a new production design must not adopt it.

## Decision

Use Azure Managed Redis as `HybridCache` L2 and as the authoritative rate-limit counter
store whenever `Redis:Enabled` is true. Local single-node development keeps L1/in-memory
limiters and requires no Redis container.

Fixed and segmented sliding windows execute atomically in Lua and use Redis server time.
Partition identifiers are SHA-256 hashed before becoming Redis keys. Counter wrappers expose
idle duration so ASP.NET can evict inactive local partition objects without resetting the
authoritative Redis window.

Azure access uses Microsoft Entra ID with the app's managed identity, TLS, disabled access
keys, a private endpoint and disabled public network access. Redis is a readiness dependency;
an outage does not silently switch the fleet to independent or unlimited counters.

## Alternatives considered

- Continue per-node counters: rejected because documented limits would be false after scale-out.
- PostgreSQL counters: rejected because every anonymous request would contend on the durable
  database and turn abuse prevention into a new database denial-of-service path.
- Retiring Azure Cache for Redis: rejected for a new 2026 deployment.
- Require Redis in every developer stack: rejected because the single-node behavior is
  correct and the real store is exercised with Testcontainers integration tests.

## Consequences

- Adds `Microsoft.Azure.StackExchangeRedis`, the Microsoft Redis distributed-cache provider
  and `Testcontainers.Redis`.
- Session, refresh-token and audit truth remain PostgreSQL; losing Redis may reduce cache hit
  rate but cannot resurrect a credential.
- The dedicated cache currently gives the app full data-plane access to that cache. Its
  private network and single-workload scope bound the blast radius; a stable custom ACL API
  should replace the default policy when Azure Managed Redis exposes one.
