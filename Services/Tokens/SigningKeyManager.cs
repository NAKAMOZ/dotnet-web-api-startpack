using System.Security.Cryptography;
using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Tokens;

/// <inheritdoc cref="ISigningKeyManager"/>
public sealed class SigningKeyManager(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<SigningKeyManager> logger,
    IAuditLogger auditLogger) : ISigningKeyManager
{
    /// <summary>
    /// Data Protection purpose string. Changing it makes every stored key undecryptable —
    /// it is part of the key derivation, not a label.
    /// </summary>
    private const string ProtectorPurpose = "Api.SigningKeys.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<SignatureResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var key = await GetOrCreateActiveKeyAsync(cancellationToken);

        using var ecdsa = ImportPrivateKey(key);

        // IeeeP1363FixedFieldConcatenation produces the raw R‖S form JWS requires. The
        // default here is DER, which validators reject — and the failure looks like a bad
        // key rather than a bad encoding, which is why it is spelled out.
        var signature = ecdsa.SignData(
            payload.Span,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return new SignatureResult(key.KeyId, signature);
    }

    public async Task<string> GetActiveKeyIdAsync(CancellationToken cancellationToken) =>
        (await GetOrCreateActiveKeyAsync(cancellationToken)).KeyId;

    public async Task<IReadOnlyList<PublicSigningKey>> GetPublishableKeysAsync(CancellationToken cancellationToken)
    {
        // Bootstraps the ring if it is empty. JWKS is often the first thing a client
        // fetches — before any token has been issued — and answering with an empty key set
        // tells it this issuer signs nothing, which is indistinguishable from a
        // misconfiguration it cannot recover from.
        await GetOrCreateActiveKeyAsync(cancellationToken);

        // Active and Retiring only. Retired keys are omitted, and that omission is what
        // makes retirement mean anything — a token whose kid resolves to nothing is
        // rejected rather than retried against the rest of the ring.
        var keys = await dbContext.SigningKeys
            .AsNoTracking()
            .Where(key => key.Status == SigningKeyStatus.Active || key.Status == SigningKeyStatus.Retiring)
            .OrderByDescending(key => key.ActivatedAt)
            .ToListAsync(cancellationToken);

        return [.. keys.Select(ToPublicKey)];
    }

    public async Task<string> RotateAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var current = await dbContext.SigningKeys
            .SingleOrDefaultAsync(key => key.Status == SigningKeyStatus.Active, cancellationToken);

        if (current is not null)
        {
            // Demoted, not retired. It must keep validating until the grace period elapses,
            // or every token it signed — up to 15 minutes of live traffic — stops working.
            current.Status = SigningKeyStatus.Retiring;
            current.RetiringAt = now;
        }

        var replacement = CreateKey(now);
        dbContext.SigningKeys.Add(replacement);

        // Demotion and promotion in one SaveChanges, so the partial unique index on
        // Status = 'Active' can never see two active keys. If this were split, the index
        // would reject the second write and leave the ring with none.
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Signing key rotated. New kid {KeyId}, previous {PreviousKeyId} now retiring.",
            replacement.KeyId,
            current?.KeyId ?? "(none)");
        await auditLogger.LogAsync(
            AuditEventType.SigningKeyRotated,
            null,
            new { replacement.KeyId },
            cancellationToken);

        return replacement.KeyId;
    }

    public async Task<int> RetireElapsedKeysAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - _jwt.KeyRetirementGrace;

        var elapsed = await dbContext.SigningKeys
            .Where(key => key.Status == SigningKeyStatus.Retiring
                          && key.RetiringAt != null
                          && key.RetiringAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var key in elapsed)
        {
            key.Status = SigningKeyStatus.Retired;
            key.RetiredAt = timeProvider.GetUtcNow();
        }

        if (elapsed.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Retired {Count} signing key(s) past the grace period.", elapsed.Count);
        }

        return elapsed.Count;
    }

    /// <summary>
    /// Resolves a <c>kid</c> to a validating public key, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <b>Exact match, with no fallback.</b> A resolver that responds to an unresolvable
    /// <c>kid</c> by trying every key in the ring defeats the entire point of <c>kid</c>-based
    /// rotation: a retired key would keep validating, so retirement would stop meaning
    /// anything and a leaked old key would stay useful indefinitely (Authentication.md §12).
    /// <para>
    /// This is the method most likely to be "improved" by someone adding a fallback for
    /// robustness. §22 tests both the unknown-kid and retired-kid cases.
    /// </para>
    /// </remarks>
    public async Task<ECDsa?> ResolveValidationKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        var key = await dbContext.SigningKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.KeyId == keyId
                             && (candidate.Status == SigningKeyStatus.Active
                                 || candidate.Status == SigningKeyStatus.Retiring),
                cancellationToken);

        if (key is null)
        {
            return null;
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.PublicKey), out _);

        return ecdsa;
    }

    private async Task<SigningKey> GetOrCreateActiveKeyAsync(CancellationToken cancellationToken)
    {
        var active = await dbContext.SigningKeys
            .SingleOrDefaultAsync(key => key.Status == SigningKeyStatus.Active, cancellationToken);

        if (active is not null)
        {
            return active;
        }

        // First run, or after a manual wipe. Two instances starting together both reach
        // here; the partial unique index on Status = 'Active' lets exactly one win, and the
        // loser re-reads the winner's key. Racing on insert is safer than a lock, because
        // the database is the only thing both instances share.
        var created = CreateKey(timeProvider.GetUtcNow());
        dbContext.SigningKeys.Add(created);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("No active signing key existed. Generated {KeyId}.", created.KeyId);
            return created;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(created).State = EntityState.Detached;

            return await dbContext.SigningKeys
                .SingleAsync(key => key.Status == SigningKeyStatus.Active, cancellationToken);
        }
    }

    private SigningKey CreateKey(DateTimeOffset now)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        var privateKey = ecdsa.ExportPkcs8PrivateKey();

        return new SigningKey
        {
            // Derived from the public key rather than random: the same key always has the
            // same kid, so a kid in an old log line still identifies something.
            KeyId = WebEncoders.Base64UrlEncode(SHA256.HashData(publicKey)),
            PublicKey = Convert.ToBase64String(publicKey),
            PrivateKeyProtected = _protector.Protect(Convert.ToBase64String(privateKey)),
            Status = SigningKeyStatus.Active,
            ActivatedAt = now,
        };
    }

    private ECDsa ImportPrivateKey(SigningKey key)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_protector.Unprotect(key.PrivateKeyProtected)), out _);
        return ecdsa;
    }

    private static PublicSigningKey ToPublicKey(SigningKey key)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.PublicKey), out _);

        // ExportParameters(false) — the false is load-bearing. Passing true exports the
        // private key, and this value is served anonymously at /.well-known/jwks.json.
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        return new PublicSigningKey(
            key.KeyId,
            "P-256",
            WebEncoders.Base64UrlEncode(parameters.Q.X!),
            WebEncoders.Base64UrlEncode(parameters.Q.Y!));
    }
}
