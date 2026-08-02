# Continuous Integration

`.github/workflows/ci.yml` runs for every pull request and every push to `main`. It grants
the workflow token read-only repository access and uses no deployment secrets.

## Stage map

| Job | Gates |
|---|---|
| `frontend` | frozen pnpm install, high-severity advisory gate, Biome, TypeScript, Vitest, production prerender, and checked-in static-artifact synchronization |
| `quality` | restore, format verification, Release build with warnings as errors, transitive vulnerability audit, known-secret pattern scan |
| `unit` | xUnit unit suite, Cobertura artifact, scoped 85% crypto/validator line threshold |
| `integration` | real PostgreSQL and Redis, migrations, security/docs/OpenAPI suites, plus an 85% `Services/Tokens` line-coverage gate |
| `image` | digest-pinned multi-stage non-root build, digest-pinned Trivy HIGH/CRITICAL gate (including findings without a current fix), Compose/readiness, SPA deep-link and streamed 64 KiB body-limit smoke |
| `migration-bundle` | model/snapshot drift rejection plus a self-contained Linux x64 EF migration bundle artifact |

`.github/workflows/deploy.yml` is the CD path. A trusted `main` push can reach staging only
after its CI workflow succeeds and the source SHA is still the current `main` head; this
prevents an older, slower CI run from rolling back a newer deployment. Manual production
dispatch remains available and is gated by the repository's `production` GitHub Environment
reviewers. CD performs Azure OIDC login,
ACR build/push with SBOM and max-level provenance, scans and deploys the exact pushed digest, applies
idempotent Bicep (with bounded retries for first-use RBAC/private-link propagation), runs the
one-shot migration job, promotes with readiness rollback, then runs staging ZAP.

`.github/workflows/performance.yml` runs manually or weekly against staging. It temporarily
raises only controlled single-source rate limits, warms horizontal replicas, executes all k6
budgets, and restores the normal settings in an `always()` step.

Deployment credentials and load-test credentials live only in GitHub Environments; CI itself
continues to require no secrets.

The integration suite runs as a single `dotnet test` invocation. Its tests share one
non-parallel collection fixture, so filtering `Category=Security` into a second run would
start the Testcontainers PostgreSQL image and replay the migration chain twice; the trait
is already recorded per test in the `.trx`.

Every external GitHub Action is pinned to an immutable commit SHA with a readable version
comment; Dependabot tracks those pins weekly. The actions use the current Node 24 major lines.
NuGet packages are restored from the centrally pinned manifest and cached by the
manifest/project hash. SDK installation and that cache live in the composite action at
`.github/actions/dotnet-setup`, which every job uses — a .NET version bump is one edit rather
than four.

Two 85% gates cover decision-bearing code rather than a global vanity percentage: the unit
job measures crypto + validators, while the integration job measures `Services/Tokens`
under real PostgreSQL flows. The latter measured 94.23% on 2026-08-02. ADR-0022 records why
EF-dependent token behavior is measured there instead of against an in-memory provider.

The frontend lockfile is monitored by Dependabot and `pnpm audit --audit-level high` gates
direct and transitive build/runtime dependencies. The sync step removes TanStack's build-time route timestamp before copying the
prerendered SPA. CI then requires a clean `wwwroot/playground` status, so identical source and
lockfiles must produce the exact committed artifact rather than a timestamp-only diff.

## Reproduce locally

```bash
dotnet restore
dotnet format dotnet-web-api-startpack.slnx --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore

dotnet test tests/UnitTests/UnitTests.csproj --configuration Release --no-restore
dotnet test tests/IntegrationTests/IntegrationTests.csproj --configuration Release --no-restore

dotnet list package --vulnerable --include-transitive --no-restore
bash scripts/check-secrets.sh

cd playground-ui
pnpm install --frozen-lockfile
pnpm audit --audit-level high
cd ..

docker build --tag dotnet-web-api-startpack:local .
docker run --rm --volume /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy@sha256:cffe3f5161a47a6823fbd23d985795b3ed72a4c806da4c4df16266c02accdd6f \
  image --exit-code 1 --severity HIGH,CRITICAL --scanners vuln \
  --no-progress --skip-version-check dotnet-web-api-startpack:local
docker compose up --detach --no-build
curl --fail http://127.0.0.1:5035/health/ready
docker compose down

dotnet tool restore
dotnet restore --runtime linux-x64
dotnet build --configuration Release --no-restore \
  /p:SkipPlaygroundBuild=true
dotnet ef migrations has-pending-model-changes --no-build --configuration Release
mkdir -p artifacts
ConnectionStrings__Postgres="Host=localhost;Database=bundle;Username=bundle;Password=bundle" \
  dotnet ef migrations bundle --no-build --self-contained --target-runtime linux-x64 \
  --configuration Release --output artifacts/efbundle
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
