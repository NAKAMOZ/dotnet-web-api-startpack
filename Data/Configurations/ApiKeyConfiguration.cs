using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();

        builder.Property(key => key.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(key => key.KeyPrefix)
            .IsRequired()
            .HasMaxLength(32);

        // The prefix is the lookup key: one indexed seek, then one hash verification.
        // Without it, authenticating a key means verifying an Argon2id hash against every
        // row — which turns each request into a table scan of deliberate slow work.
        builder.HasIndex(key => key.KeyPrefix).IsUnique();

        builder.Property(key => key.KeyHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(key => key.Scopes).AsTextArray();

        builder.HasOne(key => key.User)
            .WithMany(user => user.ApiKeys)
            .HasForeignKey(key => key.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(key => new { key.UserId, key.RevokedAt });
    }
}
