using Api.Services.Crypto;

namespace UnitTests.Services;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _generator = new();

    [Fact]
    public void ProducesTokensWithAtLeast256BitsOfEntropy()
    {
        var token = _generator.NewOpaqueToken();

        // 32 bytes base64url-encoded, unpadded. These values are unguessable only by virtue
        // of their entropy — no rate limit protects a refresh-token lookup the way lockout
        // protects a password.
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void ProducesUrlSafeTokens()
    {
        // They travel in cookies, URLs and headers. A '+' or '/' would be re-encoded
        // somewhere in that path and the token would stop matching its own hash.
        var tokens = Enumerable.Range(0, 200).Select(_ => _generator.NewOpaqueToken());

        Assert.All(tokens, token => Assert.DoesNotContain(token, character => character is '+' or '/' or '='));
    }

    [Fact]
    public void NeverRepeats()
    {
        var tokens = Enumerable.Range(0, 1_000).Select(_ => _generator.NewOpaqueToken()).ToHashSet();

        Assert.Equal(1_000, tokens.Count);
    }

    [Fact]
    public void HashesDeterministically()
    {
        var token = _generator.NewOpaqueToken();

        // Lookup is by hash, so the same token must always hash to the same value — a salted
        // hash here would make the refresh lookup impossible.
        Assert.Equal(_generator.Hash(token), _generator.Hash(token));
    }

    [Fact]
    public void HashesDifferentTokensDifferently() =>
        Assert.NotEqual(_generator.Hash("token-one"), _generator.Hash("token-two"));

    [Theory]
    [InlineData("identical", "identical", true)]
    [InlineData("identical", "different", false)]
    [InlineData("short", "much longer value", false)]
    public void ComparesInConstantTime(string left, string right, bool expected) =>
        Assert.Equal(expected, _generator.FixedTimeEquals(left, right));
}
