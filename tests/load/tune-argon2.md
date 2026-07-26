# Argon2id tuning procedure

1. Run on the same CPU/memory class as production, with no debugger and Release binaries.
2. Execute the focused harness:

   ```bash
   dotnet test tests/UnitTests/UnitTests.csproj --configuration Release \
     --filter "FullyQualifiedName~Argon2TuningHarnessTests"
   ```

3. Run at least three times after one warm-up. Record the median verification time, CPU,
   memory, runtime version, and `PasswordHashing` parameters in
   `Documentation/Operations/PerformanceBaseline.md`.
4. Tune memory first (never below 64 MiB without an explicit security review), then
   iterations, targeting approximately 100 ms verification on production-class hardware.
5. Run `login.js` at the expected concurrency. A per-hash result does not expose aggregate
   memory pressure: 100 simultaneous 64 MiB hashes can allocate roughly 6.4 GiB.
6. A measured verification time below 50 ms, or lowering the current defaults, requires
   project-owner sign-off.

The harness is a measurement aid, not a microbenchmark framework. It avoids adding
BenchmarkDotNet to the shipped dependency graph and measures the same production wrapper
and configured defaults that login uses.
