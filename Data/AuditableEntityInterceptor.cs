using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Data;

/// <summary>
/// Stamps <see cref="IAuditableEntity.CreatedAt"/> and <see cref="IAuditableEntity.UpdatedAt"/>
/// on every save, from the injected <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// An interceptor rather than a convention at the call site: a service that forgets to set
/// the timestamps produces rows that look ordinary and are quietly wrong. Here it is
/// impossible to forget.
/// <para>
/// <see cref="TimeProvider"/> rather than <c>DateTimeOffset.UtcNow</c> — the same reason
/// everything else in this codebase takes it (ADR-0011). Tests advance a
/// <c>FakeTimeProvider</c> and assert on the stamps without waiting.
/// </para>
/// </remarks>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // One timestamp for the whole save, not one per entity: rows written by the same
        // transaction should share a CreatedAt, or ordering by it becomes ambiguous for
        // work that logically happened at one moment.
        var now = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;

                    // Guard against a rewritten CreatedAt. Without this, any update that
                    // happens to carry a modified CreatedAt — a bad mapping, a detached
                    // entity attached with a default value — silently rewrites history.
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
