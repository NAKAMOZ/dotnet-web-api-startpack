using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data;

/// <summary>
/// Maps the model's three collection properties onto PostgreSQL <c>text[]</c> columns.
/// </summary>
/// <remarks>
/// A real array column rather than a joined-string blob or a child table: the values are
/// small, always read with their parent, and never queried independently, so a table would
/// buy a join and nothing else — while a delimited string would need escaping rules that
/// break the first time a value contains the delimiter.
/// <para>
/// Each conversion carries an explicit <see cref="ValueComparer{T}"/>. EF compares mutable
/// reference types by reference unless told otherwise, so without one, mutating a
/// collection in place — <c>key.Scopes.Add(...)</c> — produces no UPDATE and the change is
/// lost with no error anywhere.
/// </para>
/// </remarks>
internal static class PostgresArrayConversions
{
    /// <summary>Stores a string collection as <c>text[]</c>.</summary>
    public static PropertyBuilder<ICollection<string>> AsTextArray(
        this PropertyBuilder<ICollection<string>> property) =>
        property.HasConversion(
            collection => collection.ToArray(),
            values => new List<string>(values),
            new ValueComparer<ICollection<string>>(
                (left, right) => left!.SequenceEqual(right!),
                collection => collection.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
                collection => new List<string>(collection)));

    /// <summary>
    /// Stores an enum collection as <c>text[]</c> of member names — same reasoning as the
    /// scalar enum convention: readable in the database, and safe against reordering.
    /// </summary>
    public static PropertyBuilder<ICollection<TEnum>> AsEnumTextArray<TEnum>(
        this PropertyBuilder<ICollection<TEnum>> property)
        where TEnum : struct, Enum =>
        property.HasConversion(
            collection => collection.Select(value => value.ToString()).ToArray(),
            values => values.Select(value => Enum.Parse<TEnum>(value)).ToList(),
            new ValueComparer<ICollection<TEnum>>(
                (left, right) => left!.SequenceEqual(right!),
                collection => collection.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
                collection => collection.ToList()));
}
