using System.Globalization;
using Erp.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Erp.Storage;

/// <summary>
/// Deletes everything a company has done and set up — documents, stock, master data, series —
/// and leaves the company itself, who belongs to it and its subscription, so it can be filled again
/// from scratch.
/// </summary>
/// <remarks>
/// Written against the EF model rather than a list of tables, for the same reason the tenant filter
/// is: this project knows no module's entities, and a list kept here would forget the next table
/// someone adds. A table is the company's when it has a <c>CompanyId</c>, or is a required child of
/// one that does (a document line, a tax summary, a status change); one marked
/// <see cref="SurvivesCompanyResetAttribute"/> is left alone. The rows go child before parent, in
/// one transaction, so a table the model does not explain fails the whole thing instead of leaving a
/// company half emptied.
/// <para>
/// This defeats the immutability of issued fiscal documents, which is the point and the danger. It
/// is refused by the API outside development, and a database hardened with
/// <c>harden-sales-permissions.sql</c> refuses it too.
/// </para>
/// </remarks>
public sealed class CompanyDataPurger(AppDbContext context)
{
    private const string CompanyIdProperty = "CompanyId";
    private const string CompanyIdParameter = "@companyId";

    /// <summary>Deletes the company's data and returns how many rows each table lost, tables with none omitted.</summary>
    public async Task<IReadOnlyDictionary<string, int>> PurgeAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var steps = BuildPlan();
        var deleted = new Dictionary<string, int>();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (name, table, condition) in steps)
        {
            // Only names taken from the model and the company id as a parameter are in the statement.
#pragma warning disable EF1002
            var rows = await context.Database.ExecuteSqlRawAsync(
                $"DELETE t FROM {table} AS t WHERE {condition}",
                [new SqlParameter(CompanyIdParameter, companyId)],
                cancellationToken);
#pragma warning restore EF1002

            if (rows > 0)
                deleted[name] = rows;
        }

        await transaction.CommitAsync(cancellationToken);

        return deleted;
    }

    /// <summary>The tables to empty, each with its name and the condition that picks the company's rows, in deletion order.</summary>
    private List<(string Name, string Table, string Condition)> BuildPlan()
    {
        var scoped = new Dictionary<IEntityType, string>();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (Condition(entityType, "t", 0) is { } condition)
                scoped[entityType] = condition;
        }

        // A table has to go before the tables it points at.
        var remaining = scoped.Keys.ToHashSet();
        var ordered = new List<IEntityType>();

        while (remaining.Count > 0)
        {
            var next = remaining
                .Where(entityType => !remaining.Any(other =>
                    other != entityType && other.GetForeignKeys().Any(key => key.PrincipalEntityType == entityType)))
                .OrderBy(entityType => entityType.GetTableName(), StringComparer.Ordinal)
                .ToList();

            if (next.Count == 0)
            {
                throw new InvalidOperationException(
                    "The tables of a company reference each other in a circle, so there is no order to delete them in: "
                    + string.Join(", ", remaining.Select(entityType => entityType.GetTableName())));
            }

            ordered.AddRange(next);
            remaining.ExceptWith(next);
        }

        return [.. ordered.Select(entityType => (entityType.GetTableName()!, Table(entityType), scoped[entityType]))];
    }

    /// <summary>
    /// The condition, on <paramref name="alias"/>, that selects the company's rows of this entity;
    /// null when the entity is not the company's, or is one that survives a reset.
    /// </summary>
    private static string? Condition(IEntityType entityType, string alias, int depth)
    {
        if (entityType.IsOwned()
            || entityType.GetTableName() is null
            || entityType.ClrType.GetCustomAttributes(typeof(SurvivesCompanyResetAttribute), inherit: false).Length > 0)
        {
            return null;
        }

        var companyId = entityType.FindProperty(CompanyIdProperty);

        if (companyId?.ClrType == typeof(Guid))
            return $"{alias}.[{companyId.GetColumnName()}] = {CompanyIdParameter}";

        // Guards against a chain of required references that loops back on itself.
        if (depth > 6)
            return null;

        foreach (var key in entityType.GetForeignKeys().Where(key => key.IsRequired && key.PrincipalEntityType != entityType))
        {
            var parentAlias = $"{alias}p";

            if (Condition(key.PrincipalEntityType, parentAlias, depth + 1) is not { } parentCondition)
                continue;

            var join = string.Join(
                " AND ",
                key.Properties.Zip(
                    key.PrincipalKey.Properties,
                    (dependent, principal) => $"{parentAlias}.[{principal.GetColumnName()}] = {alias}.[{dependent.GetColumnName()}]"));

            return $"EXISTS (SELECT 1 FROM {Table(key.PrincipalEntityType)} AS {parentAlias} WHERE {join} AND {parentCondition})";
        }

        return null;
    }

    private static string Table(IEntityType entityType)
    {
        var table = entityType.GetTableName()!;
        var schema = entityType.GetSchema();

        return string.IsNullOrEmpty(schema) ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
    }

    private static string Quote(string identifier) =>
        string.Create(CultureInfo.InvariantCulture, $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]");
}
