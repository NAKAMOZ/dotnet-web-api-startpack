using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations;

/// <summary>Adds the persisted TOTP time-step replay lock.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260802120000_AddTotpReplayProtection")]
public sealed class AddTotpReplayProtection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "LastUsedTimeStep",
            schema: AppDbContext.Schema,
            table: "TotpCredentials",
            type: "bigint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastUsedTimeStep",
            schema: AppDbContext.Schema,
            table: "TotpCredentials");
    }
}
