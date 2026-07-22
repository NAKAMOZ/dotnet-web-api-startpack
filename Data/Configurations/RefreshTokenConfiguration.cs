using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        // 43 characters for an unpadded base64url SHA-256; 64 leaves room for the encoding
        // to change without a migration.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        // The lookup index for every refresh, and a security control in its own right: two
        // rows sharing a hash would make rotation ambiguous, and the database must make
        // that impossible rather than the service assuming it.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasOne(token => token.Session)
            .WithMany(session => session.RefreshTokens)
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ReplacedByTokenId is deliberately NOT a foreign key. The chain is an audit
        // artefact, and an FK would impose a delete order on cleanup — the successor would
        // have to outlive its predecessor — for a link that is only ever read by a human
        // reconstructing an incident.

        // Cleanup worker (§12): unused tokens past expiry. Spent tokens are excluded by the
        // filter because they are retained on purpose — reuse detection needs to tell
        // "already used" apart from "never existed" (Authentication.md §11).
        builder.HasIndex(token => token.ExpiresAt)
            .HasFilter("\"UsedAt\" IS NULL");
    }
}
