using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    public void Configure(EntityTypeBuilder<TotpCredential> builder)
    {
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).ValueGeneratedNever();

        // Data Protection payload, base64 — comfortably longer than the 20-byte secret it
        // wraps, because the envelope carries key-ring metadata as well.
        builder.Property(credential => credential.SecretEncrypted)
            .IsRequired()
            .HasMaxLength(1024);

        // One authenticator per user, enforced by the database. A second row would make
        // "which secret validates this code?" a question the code has to answer by picking,
        // and disabling MFA would leave a live enrolment behind.
        builder.HasOne(credential => credential.User)
            .WithOne(user => user.TotpCredential)
            .HasForeignKey<TotpCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
