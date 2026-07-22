using Api.Configuration;
using Api.Services.Crypto;
using Microsoft.Extensions.Options;

namespace UnitTests.Services;

/// <summary>
/// Password hashing: round-trip, rejection, and the re-hash-on-login migration path
/// (ADR-0006).
/// </summary>
public class Argon2PasswordHasherTests
{
    /// <summary>
    /// Cheap parameters throughout — these tests assert behaviour, not cost, and the real
    /// 64 MiB profile would make the suite take minutes.
    /// </summary>
    private static Argon2PasswordHasher HasherWith(int memoryKib = 1024, int iterations = 1) =>
        new(Options.Create(new PasswordHashingOptions
        {
            PasswordMemoryKib = memoryKib,
            PasswordIterations = iterations,
            PasswordParallelism = 1,
            SecretMemoryKib = 1024,
            SecretIterations = 1,
            SecretParallelism = 1,
        }));

    [Fact]
    public void VerifiesItsOwnHash()
    {
        var hasher = HasherWith();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void RejectsAWrongPassword()
    {
        var hasher = HasherWith();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.False(hasher.Verify("correct horse battery stapler", hash));
    }

    [Fact]
    public void ProducesADifferentHashEachTime()
    {
        // Distinct salts. Identical hashes for identical passwords would let an attacker
        // group accounts sharing a password from the database alone.
        var hasher = HasherWith();

        Assert.NotEqual(hasher.Hash("same password here"), hasher.Hash("same password here"));
    }

    [Fact]
    public void ReturnsFalseForACorruptStoredHash()
    {
        // An authentication failure, not a 500. Throwing would turn one corrupt row into an
        // outage for that account — and would tell the caller their guess reached something
        // unusual.
        var hasher = HasherWith();

        Assert.False(hasher.Verify("any password at all", "not-an-argon2-hash"));
    }

    [Fact]
    public void EmbedsItsParametersInTheHash() =>
        // Self-describing hashes are what make gradual migration possible: verification
        // reads the cost from the stored value, so raising the configured cost does not
        // invalidate a single existing hash.
        Assert.StartsWith("$argon2id$v=19$m=1024,t=1,p=1$", HasherWith().Hash("a long enough password"));

    [Fact]
    public void DoesNotWantARehashAtCurrentParameters()
    {
        var hasher = HasherWith(memoryKib: 2048, iterations: 2);

        Assert.False(hasher.NeedsRehash(hasher.Hash("a long enough password")));
    }

    [Fact]
    public void WantsARehashWhenTheConfiguredCostRises()
    {
        var oldHash = HasherWith(memoryKib: 1024, iterations: 1).Hash("a long enough password");
        var stronger = HasherWith(memoryKib: 4096, iterations: 3);

        // The fleet migrates itself as users log in — the one moment the plaintext exists.
        Assert.True(stronger.NeedsRehash(oldHash));
    }

    [Fact]
    public void WantsARehashForAnUnparseableHash() =>
        // If we cannot tell how it was made, re-hashing with current parameters is the safe
        // answer.
        Assert.True(HasherWith().NeedsRehash("$argon2id$garbage"));

    [Fact]
    public void SecretsUseTheCheapProfileAndPasswordsDoNot()
    {
        // The two profiles must stay distinguishable in the stored value — §22 asserts the
        // password path uses the slow one, and it can only do that if they differ visibly.
        var hasher = HasherWith(memoryKib: 8192, iterations: 4);

        Assert.Contains("m=8192,t=4", hasher.Hash("a user chosen password"));
        Assert.Contains("m=1024,t=1", hasher.HashSecret("ak_machine_generated_secret"));
    }
}
