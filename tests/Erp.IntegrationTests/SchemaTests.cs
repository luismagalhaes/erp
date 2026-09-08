using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Whether the model and the schema agree.
/// </summary>
/// <remarks>
/// This is the cheapest test in the project and it would have saved an afternoon. A column the
/// model names one way and the migration another compiles, migrates, and then fails at the first
/// query with <i>Invalid column name</i> — which reads like a broken migration and is not one.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SchemaTests(SqlServerFixture fixture)
{
    /// <summary>
    /// The database was built by running the migrations, so reaching here at all means they apply
    /// cleanly from nothing. What is left is the question they cannot answer: does the model still
    /// describe what they built?
    /// </summary>
    [Fact]
    public async Task The_migrations_leave_nothing_the_model_still_wants_to_change()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty();
    }

    /// <summary>
    /// Every table the model declares is read once. A mismatch between a mapped column and the real
    /// one surfaces here, on the table it belongs to, instead of in whichever feature happened to
    /// query it first.
    /// </summary>
    [Fact]
    public async Task Every_mapped_entity_can_be_read_from_the_database()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var failures = new List<string>();

        foreach (var entityType in context.Model.GetEntityTypes().Where(x => x.GetTableName() is not null))
        {
            // Owned types are read through their owner; querying them on their own is not a thing.
            if (entityType.IsOwned())
                continue;

            try
            {
                // One row is enough: SQL Server binds every selected column before it returns any.
                var query = (IQueryable<object>)context
                    .GetType()
                    .GetMethod(nameof(DbContext.Set), 1, [])!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(context, null)!;

                await query.Take(1).ToListAsync();
            }
            catch (Exception ex)
            {
                failures.Add($"{entityType.ClrType.Name} ({entityType.GetTableName()}): {ex.GetBaseException().Message}");
            }
        }

        failures.Should().BeEmpty();
    }
}
