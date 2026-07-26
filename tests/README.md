# Tests

Run the complete suite from the repository root:

```bash
dotnet test
```

`UnitTests` contains isolated validators, cryptographic wrappers, token issuers, policy
handlers, mappings, and architecture guards. It uses no database, network, or
`WebApplicationFactory`. `IntegrationTests` hosts the real pipeline and, for tests in the
`postgres-integration` collection, starts one PostgreSQL 18 Testcontainer, applies EF
migrations, and uses Respawn between tests.

Test names follow `Method_Condition_Expectation` when a method or transition is under test.
Guard tests may use a sentence-style invariant when that reads more clearly.

Time-dependent units receive `FakeTimeProvider`; tests must not sleep or call the wall clock
for expiry assertions. EF-dependent services are not tested with EF's in-memory provider,
because it does not reproduce PostgreSQL constraints, transactions, or `ExecuteUpdate`.

## Integration prerequisites

- Docker with a reachable daemon.
- Enough capacity for one `postgres:18-alpine` container.
- No fixed port is required: Testcontainers chooses a random host port, so the suite can
  run beside the Compose stack.

Run one flow suite:

```bash
dotnet test tests/IntegrationTests/IntegrationTests.csproj \
  --filter "FullyQualifiedName~TokenServiceIntegrationTests"
```

Run only the adversarial suite:

```bash
dotnet test tests/IntegrationTests/IntegrationTests.csproj \
  --filter "Category=Security"
```

The shared fixture replaces `TimeProvider` with one `FakeTimeProvider`. Tests reset its
timestamp and database state before changing either. The real §12 feature flows include
registration enumeration parity, login/refresh/replay, password-reset revocation, TOTP,
lockout/admin unlock, and API-key permission-intersection coverage.
