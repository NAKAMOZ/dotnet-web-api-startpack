using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.IpAddress).HasMaxLength(45);
        builder.Property(entry => entry.UserAgent).HasMaxLength(512);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(64);

        // jsonb, not text: the admin query endpoint filters inside this column, and jsonb
        // is the form Postgres can index and query. Contents are subject to the redaction
        // rules — never a token, never a secret (ADR-0010).
        builder.Property(entry => entry.Metadata).HasColumnType("jsonb");

        // SetNull, and no navigation property on either side. Deleting an account must not
        // erase the record of what it did — this is the one relationship in the model where
        // a user delete deliberately leaves data behind, and it is why AuditLogEntry.UserId
        // is nullable in the first place.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The three admin filters, each leading with its selective column and ending in
        // OccurredAt so the range predicate is served by the same index rather than a sort.
        builder.HasIndex(entry => new { entry.UserId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.EventType, entry.OccurredAt });
        builder.HasIndex(entry => entry.OccurredAt);

        // Stitches an audit row to the operational log lines from the same request (§14).
        builder.HasIndex(entry => entry.CorrelationId);
    }
}
