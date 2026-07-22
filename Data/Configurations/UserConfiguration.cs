using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        // Guid v7 is assigned in the entity initializer (§6). Telling EF the value is never
        // database-generated stops it treating a set Id as a temporary value to overwrite.
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(254)
            .HasColumnType("citext");

        // Uniqueness is a DATABASE constraint, not an application check. "SELECT then
        // INSERT" loses the race between two concurrent registrations and produces two
        // accounts for one human — a duplicate the whole auth model assumes cannot exist.
        // citext makes the index case-insensitive, so Alice@x.com cannot register twice.
        builder.HasIndex(user => user.Email).IsUnique();

        // Argon2id encoded hash — algorithm, parameters and salt travel inside the string,
        // which is what makes re-hash-on-login possible (ADR-0006). Nullable by design:
        // social- and passkey-only accounts have no password.
        builder.Property(user => user.PasswordHash).HasMaxLength(256);

        builder.Property(user => user.DisplayName).HasMaxLength(100);

        builder.Property(user => user.SecurityStamp)
            .IsRequired()
            .HasMaxLength(64);
    }
}
