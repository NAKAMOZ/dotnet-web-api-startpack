using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Id).ValueGeneratedNever();

        builder.Property(code => code.CodeHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasOne(code => code.User)
            .WithMany(user => user.RecoveryCodes)
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // MFA fallback verification loads a user's unused codes. No unique index on the
        // hash: two users may legitimately hold the same code value, and a global unique
        // constraint would turn that coincidence into a failed regeneration.
        builder.HasIndex(code => new { code.UserId, code.UsedAt });
    }
}
