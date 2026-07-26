# Continuous Integration

`.github/workflows/ci.yml` runs for every pull request and every push to `main`. It grants
the workflow token read-only repository access and uses no deployment secrets.

## Stage map

| Job | Gates |
|---|---|
| `quality` | restore, format verification, Release build with warnings as errors, transitive vulnerability audit, known-secret pattern scan |
| `unit` | xUnit unit suite, Cobertura artifact, scoped 85% crypto/validator line threshold |
| `integration` | real PostgreSQL migrations, Respawn-backed integration suite, separately reported `Category=Security` attack suite, docs/OpenAPI synchronization |
| `image` | multi-stage non-root image build with BuildKit cache, full Compose startup, `/health/ready` smoke |
| `migration-bundle` | self-contained Linux x64 EF migration bundle artifact |
| `cd` | disabled skeleton; P14 must define a target and GitHub Environment reviewers |

GitHub Actions and Docker actions use the current Node 24 major lines and Dependabot
tracks them weekly. NuGet packages are restored from the centrally pinned manifest and
cached by the manifest/project hash.

The current 85% gate covers crypto and validators. ADR-0022 records why the whole
`Services/Tokens` threshold remains open until all EF-backed token paths are exercised by
the integration suite; weakening the target to make incomplete code green was rejected.

## Reproduce locally

```bash
dotnet restore
dotnet format dotnet-web-api-startpack.slnx --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore

dotnet test tests/UnitTests/UnitTests.csproj --configuration Release --no-restore
dotnet test tests/IntegrationTests/IntegrationTests.csproj --configuration Release --no-restore

dotnet list package --vulnerable --include-transitive
bash scripts/check-secrets.sh

docker build --tag dotnet-web-api-startpack:local .
docker compose up --detach --no-build
curl --fail http://127.0.0.1:5035/health/ready
docker compose down

dotnet tool restore
dotnet restore --runtime linux-x64
mkdir -p artifacts
ConnectionStrings__Postgres="Host=localhost;Database=bundle;Username=bundle;Password=bundle" \
  dotnet ef migrations bundle --self-contained --runtime linux-x64 --output artifacts/efbundle
```

The integration suite needs a reachable Docker daemon. Testcontainers allocates a random
PostgreSQL host port, so Compose can remain running.

## Repository settings

After the repository is pushed to GitHub, an owner must configure:

1. A branch rule for `main` requiring pull requests.
2. Required status checks: `quality`, `unit`, `integration`, `image`, and
   `migration-bundle`.
3. Force pushes and branch deletion disabled.
4. At least one approving review and dismissal of stale approvals.
5. GitHub secret scanning and push protection, when available for the repository.

These settings are external state and cannot be proven by the workflow file. Record the
date and owner here when applied.

## Pipeline acceptance

Before calling §26 complete, verify once on synthetic branches that each gate rejects:
format drift, a compiler warning, a failing unit test, a failing PostgreSQL/security test,
an advisory, a seeded fake token matching the scanner, an unhealthy image, and a broken
migration. Delete the synthetic branches after review; do not weaken a gate merely to
demonstrate it.
