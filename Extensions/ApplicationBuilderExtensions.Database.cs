using Api.Data;
using Api.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Api.Extensions;

public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies pending migrations and runs the development seeder. <b>Development only</b>
    /// — in every other environment this is a no-op that logs why.
    /// </summary>
    /// <remarks>
    /// Production migrates through an <b>EF migration bundle</b> executed as a deploy step
    /// (§26 builds it, §27 runs it), never from inside the API process. Three reasons, and
    /// the first is the one that bites:
    /// <list type="number">
    /// <item>Several instances starting at once would race to apply the same migration.</item>
    /// <item>The runtime database role should not hold DDL rights at all; auto-migration
    /// requires exactly the permissions an application server should never have.</item>
    /// <item>A failed migration during startup leaves the schema half-changed with no
    /// operator watching, instead of failing a deploy step that can be rolled back.</item>
    /// </list>
    /// </remarks>
    public static async Task<WebApplication> UseDatabaseSetupAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        await using var scope = app.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        return app;
    }
}
