using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class VerificationTokenConfiguration : IEntityTypeConfiguration<VerificationToken>
{
    public void Configure(EntityTypeBuilder<VerificationToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Optional FK — passkey authentication challenges and social OAuth state are
        // issued before any user is known. Cascade rather than the default ClientSetNull
        // for an optional relationship: deleting an account must take its pending reset
        // and verification tokens with it, not orphan them into ownerless credentials.
        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cleanup worker (§12): unconsumed tokens past expiry.
        builder.HasIndex(token => token.ExpiresAt)
            .HasFilter("\"ConsumedAt\" IS NULL");
    }
}
