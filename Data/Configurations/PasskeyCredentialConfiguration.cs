using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class PasskeyCredentialConfiguration : IEntityTypeConfiguration<PasskeyCredential>
{
    public void Configure(EntityTypeBuilder<PasskeyCredential> builder)
    {
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).ValueGeneratedNever();

        // bytea, not text. The credential ID is opaque binary the authenticator chose;
        // base64-ing it for storage would only add an encode/decode step to every assertion.
        builder.Property(credential => credential.CredentialId).IsRequired();

        builder.Property(credential => credential.PublicKey).IsRequired();

        // Unique across all users: an assertion arrives with a credential ID and no user
        // id, so this index is what resolves the caller. A duplicate would make that
        // resolution ambiguous — which is to say, it would pick a user.
        builder.HasIndex(credential => credential.CredentialId).IsUnique();

        builder.Property(credential => credential.Transports).AsTextArray();

        builder.Property(credential => credential.Label).HasMaxLength(100);

        builder.HasOne(credential => credential.User)
            .WithMany(user => user.PasskeyCredentials)
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lists a user's passkeys, and scopes the delete-by-id query to its owner — the
        // WHERE CredentialId = @id AND UserId = @sub shape that keeps this route from being
        // an IDOR (Authorization.md §5).
        builder.HasIndex(credential => credential.UserId);
    }
}
