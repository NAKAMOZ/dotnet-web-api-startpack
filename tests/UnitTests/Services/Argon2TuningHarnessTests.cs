using System.Diagnostics;
using Api.Configuration;
using Api.Logging;
using Api.Services.Crypto;
using Microsoft.Extensions.Options;

namespace UnitTests.Services;

public sealed class Argon2TuningHarnessTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void MeasureDefaultPasswordVerificationCost()
    {
        var hasher = new Argon2PasswordHasher(
            Options.Create(new PasswordHashingOptions()),
            new AuthMetrics());
        const string password = "Benchmark-only!River-Stone-Cobalt-47";
        var hash = hasher.Hash(password);

        // Warm the implementation and native-code paths before collecting samples.
        Assert.True(hasher.Verify(password, hash));

        var samples = new List<double>();
        for (var index = 0; index < 7; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            Assert.True(hasher.Verify(password, hash));
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var median = samples[samples.Count / 2];
        output.WriteLine(
            "Argon2id verify median: {0:F1} ms; samples: {1}",
            median,
            string.Join(", ", samples.Select(sample => sample.ToString("F1"))));

        // §23's security floor. The target is ~100 ms; only the lower bound is a test
        // because slow shared CI hardware must not make an otherwise-correct build flaky.
        Assert.True(
            median >= 50,
            $"Default Argon2id verification measured {median:F1} ms, below the 50 ms approval floor.");
    }
}
