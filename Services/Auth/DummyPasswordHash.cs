using Api.Services.Crypto;

namespace Api.Services.Auth;

/// <summary>One process-wide Argon2 hash used to equalize unknown-user login work.</summary>
public sealed class DummyPasswordHash(IPasswordHasher passwordHasher)
{
    public string Value { get; } = passwordHasher.Hash("dummy-password-never-accepted");
}
