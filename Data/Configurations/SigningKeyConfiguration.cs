using Api.Models;
using Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();

        builder.Property(key => key.KeyId)
            .IsRequired()
            .HasMaxLength(64);

        // Every token header carries this kid, and resolution is exact-match with no
        // fallback (Authentication.md §12). Two keys sharing a kid would force the resolver
        // to choose — and a resolver that chooses is a resolver that can validate a token
        // against a key that did not sign it.
        builder.HasIndex(key => key.KeyId).IsUnique();

        // Data Protection envelope around a P-256 private key. Never logged, never
        // serialised into a response, never in a Problem Details payload (ADR-0020).
        builder.Property(key => key.PrivateKeyProtected)
            .IsRequired()
            .HasMaxLength(4096);

        // base64 SubjectPublicKeyInfo — about 124 characters for P-256.
        builder.Property(key => key.PublicKey)
            .IsRequired()
            .HasMaxLength(256);

        // Exactly one Active key at a time, enforced by a partial unique index rather than
        // by rotation being careful. Rotation demotes and promotes in one transaction; if a
        // future change ever splits those steps, this constraint fails the write instead of
        // leaving two keys signing — a state where retiring either one breaks live tokens.
        builder.HasIndex(key => key.Status)
            .IsUnique()
            .HasFilter($"\"Status\" = '{nameof(SigningKeyStatus.Active)}'");

        // JWKS projection: Active plus Retiring, ordered by activation.
        builder.HasIndex(key => new { key.Status, key.ActivatedAt });
    }
}
