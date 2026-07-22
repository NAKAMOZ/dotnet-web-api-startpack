using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();

        // 45 characters holds the longest IPv6 representation, including an embedded IPv4
        // suffix. Stored as text rather than inet: it is display and audit data, never
        // subnet-matched.
        builder.Property(session => session.IpAddress).HasMaxLength(45);

        // Attacker-controlled input. Truncated here so an oversized header cannot bloat a
        // row, and always logged as a structured property, never concatenated (ADR-0010).
        builder.Property(session => session.UserAgent).HasMaxLength(512);

        builder.Property(session => session.DeviceLabel).HasMaxLength(100);

        builder.Property(session => session.SecurityStamp)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(session => session.AuthenticationMethods).AsEnumTextArray();

        builder.HasOne(session => session.User)
            .WithMany(user => user.Sessions)
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The session-list query: a user's live sessions. Composite so "live sessions for
        // this user" is one index seek rather than a scan filtered afterwards.
        builder.HasIndex(session => new { session.UserId, session.RevokedAt });

        // Cleanup worker (§12): sessions past their absolute cap that are not yet revoked.
        // The partial filter keeps the index proportional to live sessions rather than to
        // every session ever created — on this table the difference grows without bound.
        builder.HasIndex(session => session.AbsoluteExpiresAt)
            .HasFilter("\"RevokedAt\" IS NULL");
    }
}
