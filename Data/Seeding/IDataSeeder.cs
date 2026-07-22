namespace Api.Data.Seeding;

/// <summary>
/// Runtime seeding, as opposed to the <c>HasData</c> reference data baked into migrations.
/// </summary>
/// <remarks>
/// The split is a security boundary, not an organisational one. Anything seeded through
/// <c>HasData</c> becomes part of the schema history and therefore reaches every
/// environment; anything seeded through this interface runs only where the implementation
/// allows it. Development credentials belong on this side and must never cross to the
/// other — a fake password compiled into a migration is a real password in production.
/// </remarks>
public interface IDataSeeder
{
    /// <summary>
    /// Seeds data if the environment permits it. Implementations are <b>idempotent</b>:
    /// a second run against an already-seeded database changes nothing, because the
    /// development loop restarts the app far more often than it recreates the database.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken);
}
