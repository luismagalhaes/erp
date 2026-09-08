using Erp.Sales.Domain;
using Erp.Sales.Storage.Data;
using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Erp.Sales.Tests;

/// <summary>
/// The column names this module's tables actually use.
/// </summary>
/// <remarks>
/// The Storage layer has no tests, and this is the gap that let a real failure through: the loading
/// and delivery points of a movement are a complex property, and EF names those
/// <c>ShipFrom_Address</c> unless told otherwise. The migration and the database say
/// <c>ShipFromAddress</c>, so a model built without this configuration compiles, migrates, and then
/// fails at the first query with "Invalid column name".
/// <para>
/// Building the model here is the cheapest thing that would have caught it — no database needed,
/// because the question is what SQL EF intends to write, not whether it runs.
/// </para>
/// </remarks>
public class SalesModelConfigurationTests
{
    /// <summary>The model as the host builds it: one context, one module contributing its tables.</summary>
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            // Never opened: building the model is all this needs, and the provider only has to be
            // the same one production uses, so the SQL Server naming conventions apply.
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ErpModelOnly;")
            .Options;

        using var context = new ErpDbContext(options, [new SalesModelConfiguration()]);

        return context.Model;
    }

    private static string? ColumnOf<TEntity>(string propertyPath)
    {
        var entity = BuildModel().FindEntityType(typeof(TEntity))!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());

        var parts = propertyPath.Split('.');

        if (parts.Length == 1)
            return entity.FindProperty(parts[0])?.GetColumnName(table);

        var complex = entity.FindComplexProperty(parts[0])?.ComplexType;
        return complex?.FindProperty(parts[1])?.GetColumnName(table);
    }

    [Theory]
    [InlineData("ShipFrom.Address", "ShipFromAddress")]
    [InlineData("ShipFrom.City", "ShipFromCity")]
    [InlineData("ShipFrom.PostalCode", "ShipFromPostalCode")]
    [InlineData("ShipFrom.Country", "ShipFromCountry")]
    [InlineData("ShipFrom.WarehouseId", "ShipFromWarehouseId")]
    [InlineData("ShipFrom.LocationId", "ShipFromLocationId")]
    [InlineData("ShipTo.Address", "ShipToAddress")]
    [InlineData("ShipTo.City", "ShipToCity")]
    [InlineData("ShipTo.PostalCode", "ShipToPostalCode")]
    [InlineData("ShipTo.Country", "ShipToCountry")]
    [InlineData("ShipTo.WarehouseId", "ShipToWarehouseId")]
    [InlineData("ShipTo.LocationId", "ShipToLocationId")]
    public void The_shipping_points_are_flattened_without_an_underscore(string property, string column) =>
        ColumnOf<StockMovement>(property).Should().Be(column);

    /// <summary>
    /// The query behind "faturar a partir de guias", as SQL. The model being right is not quite the
    /// whole answer: this one uses <c>AsSplitQuery</c> with two includes, and the question is whether
    /// the column names survive that. They must, because the database has no <c>ShipFrom_Address</c>.
    /// </summary>
    [Fact]
    public void The_invoiceable_movements_query_asks_for_the_columns_the_database_has()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ErpModelOnly;")
            .Options;

        using var context = new ErpDbContext(options, [new SalesModelConfiguration()]);

        var sql = context.Set<StockMovement>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .Where(x => !x.PartyIsSupplier)
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.SequenceNumber)
            .ToQueryString();

        sql.Should().NotContain("ShipFrom_", "the database has no such column");
        sql.Should().NotContain("ShipTo_");
        sql.Should().Contain("ShipFromAddress");
        sql.Should().Contain("ShipToAddress");
    }

    [Fact]
    public void The_movement_tables_are_named_after_the_documents_they_hold()
    {
        var model = BuildModel();

        model.FindEntityType(typeof(StockMovement))!.GetTableName().Should().Be("StockMovement");
        model.FindEntityType(typeof(StockMovementLine))!.GetTableName().Should().Be("StockMovementLine");
    }
}
