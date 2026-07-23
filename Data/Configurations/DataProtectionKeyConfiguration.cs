using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

/// <summary>
/// The Data Protection key ring (ADR-0021). The one table in this model whose entity type
/// is not ours — it ships with
/// <c>Microsoft.AspNetCore.DataProtection.EntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// Mapped here anyway rather than left to convention, so the table is visible where every
/// other table is and picks up <see cref="AppDbContext.Schema"/> from
/// <c>HasDefaultSchema</c> like the rest.
/// <para>
/// <b>This table is written at runtime, not only by migrations.</b> Data Protection creates
/// a successor key when the active one nears the end of its lifetime, during a normal
/// request. A runtime database role with read-only access here fails roughly 90 days after
/// deployment, not at deploy time.
/// </para>
/// </remarks>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("DataProtectionKeys");

        builder.HasKey(key => key.Id);

        // Identity, not Guid v7: the type is the package's and its key is an int. This is
        // the one place the model's Guid-v7 convention does not apply, because the shape
        // is not ours to choose.
        builder.Property(key => key.Id).ValueGeneratedOnAdd();

        builder.Property(key => key.FriendlyName).HasMaxLength(256);

        // The serialised <key> element, including the encrypted-or-not master key material.
        // Unbounded: the payload grows with whichever ProtectKeysWith* provider is chosen
        // in §27, and a length cap here would be a limit on that future choice.
        builder.Property(key => key.Xml).IsRequired();
    }
}
