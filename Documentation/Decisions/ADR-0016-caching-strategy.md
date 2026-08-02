# ADR-0016: In-Memory `HybridCache` as the v1 Caching Layer

- **Status:** Superseded by ADR-0029 for multi-node deployments; local L1 decision remains
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decision P5** (`ROADMAP/00-overview.md`)
- **Affects:** §12 (services), §17 (rate limiting), §24 (compose), §29 (Redis scale-out backlog)

## Context

Parts of the request path are read-heavy and change rarely: the JWKS key ring, role-to-permission mappings, and API-key prefix lookups all get hit far more often than they change. Reading them from PostgreSQL on every request wastes the database's capacity on data that could sit in memory.

The tension is that caching is where premature distribution usually enters a system. Standing up Redis on day one adds a container, a connection-failure mode, a serialisation format, and an eviction policy — before there is any evidence a single node cannot cope.

v1 targets a single application node. That makes the honest question not "in-memory or distributed?" but "how do we cache now without making the distributed version a rewrite?"

## Decision

**`HybridCache`** (`Microsoft.Extensions.Caching.Hybrid` 10.8.0), **in-memory only** for v1. No Redis, no distributed backplane.

`HybridCache` is a two-level abstraction: an L1 in-process cache and an optional L2 distributed cache behind one API. v1 configures L1 only. Adding Redis later is a **registration change at the composition root**, not a change to any call site — which is the property that makes deferring the distributed tier safe rather than merely cheap.

Rate limiting (§17) uses the built-in ASP.NET Core `RateLimiter` with in-memory counters on the same reasoning (P6, still pending — this ADR does not resolve it).

## Alternatives considered

**`HybridCache` with Redis from day one.** Correct for a multi-node deployment and avoids a later migration. Rejected for v1: no second node exists, and the deployment target itself is still undecided (P14). It would add a required container to local development and CI, plus a new failure mode — cache unavailable — for a benefit nothing currently needs.

**Plain `IMemoryCache`.** No new package, and adequate for L1. Rejected because it has no stampede protection: on a cache miss under concurrent load, every request performs the expensive lookup. `HybridCache` coalesces concurrent misses for the same key into one. It also offers no path to a distributed tier without rewriting every call site, which is precisely the migration cost this decision exists to avoid.

**`IDistributedCache` against an in-memory implementation.** Gives the distributed-shaped API immediately. Rejected: it forces serialisation on every access even when nothing is distributed, and has no L1 tier, so it is slower than `IMemoryCache` for the single-node case it would be serving.

**No caching in v1.** Defensible — correctness first, optimise on evidence. Rejected narrowly because the JWKS key ring is read on essentially every token validation, and it changes quarterly.

## Consequences

- No cache infrastructure in v1: nothing to run locally, nothing to fail in CI, nothing to operate.
- **Cache state is per-process.** With a single node this is invisible. The moment a second node is added it becomes a correctness question — two nodes can hold divergent views of the same key until each entry expires. That is the trigger condition, and it is recorded in the §29 backlog rather than left to be discovered.
- Cached entries must be **invalidated on write**, not merely allowed to expire, wherever staleness has a security consequence. A retired signing key or a revoked API key that lingers in cache is a live credential past its revocation. Any cached security-relevant value needs an explicit invalidation path and a TTL short enough to bound the failure if invalidation is missed.
- **Never cached:** session validity and refresh-token state. Both are revocation-sensitive and already read at refresh time only ([ADR-0001](ADR-0001-token-strategy.md)); caching them would reintroduce the staleness window that stateless access tokens were bounded to avoid.
- Migration to Redis is a composition-root change plus a container. Call sites are unaffected — that is the whole reason `HybridCache` was chosen over `IMemoryCache`.
- P6 (rate-limit store) remains open and is decided on §17's schedule. It is expected to follow the same in-memory-first reasoning, but it is not decided here.
