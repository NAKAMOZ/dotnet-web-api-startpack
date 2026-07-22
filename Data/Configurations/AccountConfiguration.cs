using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();

        builder.Property(account => account.Provider)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(account => account.ProviderAccountId)
            .IsRequired()
            .HasMaxLength(256);

        // The social-login lookup, and the only key an account may be matched on. Unique so
        // one external identity cannot be linked to two local users — otherwise a second
        // link silently decides which account a Google login lands in.
        builder.HasIndex(account => new { account.Provider, account.ProviderAccountId })
            .IsUnique();

        builder.HasOne(account => account.User)
            .WithMany(user => user.Accounts)
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
