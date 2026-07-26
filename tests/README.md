# Tests

Run the complete suite from the repository root:

```bash
dotnet test
```

`UnitTests` contains isolated validators, cryptographic wrappers, token issuers, policy
handlers, mappings, and architecture guards. It uses no database, network, or
`WebApplicationFactory`. `IntegrationTests` hosts the real application pipeline and owns
OpenAPI/document synchronization; database-backed feature flows arrive with §21.

Test names follow `Method_Condition_Expectation` when a method or transition is under test.
Guard tests may use a sentence-style invariant when that reads more clearly.

Time-dependent units receive `FakeTimeProvider`; tests must not sleep or call the wall clock
for expiry assertions. EF-dependent services are not tested with EF's in-memory provider,
because it does not reproduce PostgreSQL constraints, transactions, or `ExecuteUpdate`.
