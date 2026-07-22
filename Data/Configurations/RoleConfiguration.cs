using Api.Data.Seeding;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(64);

        // The name is the authorization key — it lands in the `roles` claim and keys the
        // static permission map. Two rows named "Admin" would mean two role ids granting
        // the same authority, and revoking one would leave the other in place.
        builder.HasIndex(role => role.Name).IsUnique();

        builder.Property(role => role.Description).HasMaxLength(256);

        // Static reference data, versioned inside the migration (§8). A database built from
        // migrations alone is complete — no separate "now run the role seeder" step that a
        // deployment can skip and leave every authorization check failing.
        builder.HasData(RoleSeed.All);
    }
}
