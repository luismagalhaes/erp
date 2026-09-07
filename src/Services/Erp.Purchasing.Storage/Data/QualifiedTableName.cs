using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Data;

/// <summary>
/// Where an entity is mapped, quoted for SQL Server. Needed by the statements that drop to SQL to
/// take a lock hint, and read from the model so a literal cannot drift away from the mapping.
/// </summary>
public static class QualifiedTableName
{
    public static string For<TEntity>(DbContext dbContext)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");

        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped to a table.");

        var schema = entityType.GetSchema();

        return string.IsNullOrEmpty(schema) ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
    }

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
