using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The defence against deducting the same VAT twice.
/// </summary>
/// <remarks>
/// The same invoice arrives on paper and again by email, gets recorded twice, and the input VAT is
/// claimed twice with nothing looking wrong. The service checks for it, but the guarantee is the
/// unique index on <c>(CompanyId, SupplierTaxId, SupplierDocumentNumber)</c> — and that index
/// exists <b>only in the migration</b>: it spans a column of the invoice and one of the owned
/// supplier snapshot, which the model builder has no way to express.
/// <para>
/// So it is invisible to every other test in this repository. Nothing but a real database can say
/// whether it is there at all, and it was very nearly lost once when the migration was regenerated.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class PurchaseInvoiceDuplicationTests(SqlServerFixture fixture)
{
    private static RecordPurchaseInvoiceRequest Invoice(
        CompanyScenario scenario,
        string number,
        PurchaseOrderSupplierDto? supplier = null) =>
        new(scenario.CompanyId,
            scenario.SupplierId,
            supplier ?? scenario.Supplier,
            "FT",
            number,
            new DateOnly(2026, 3, 12),
            new DateOnly(2026, 3, 14),
            [new PurchaseInvoiceLineRequest("ART001", "Artigo de teste", 3m, 5m)],
            WarehouseId: scenario.WarehouseId);

    [Fact]
    public async Task The_same_document_from_the_same_supplier_is_refused_the_second_time()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>()
                .RecordAsync(Invoice(scenario, "FT 2026/17"), "user-1"));

        var again = () => scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>()
                .RecordAsync(Invoice(scenario, "FT 2026/17"), "user-1"));

        await again.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already recorded*");
    }

    /// <summary>
    /// The service check reads before it writes, so two requests can both read "not there" and both
    /// go on to write. Only the index stops the second one, which is the whole reason it exists.
    /// </summary>
    [Fact]
    public async Task Recording_the_same_document_twice_at_once_still_leaves_only_one()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        async Task<Exception?> RecordAsync()
        {
            try
            {
                await using var scope = fixture.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceService>()
                    .RecordAsync(Invoice(scenario, "FT 2026/99"), "user-1");

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(RecordAsync)));

        outcomes.Count(outcome => outcome is null).Should().Be(1, "exactly one recording may win");

        // The rest fail — some on the service check, some on the index, depending on the timing.
        // Which one is not the point; that the database has one row is.
        var recorded = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().GetAllAsync(scenario.CompanyId));

        recorded.Should().ContainSingle();
    }

    /// <summary>
    /// The number alone is deliberately not unique: two suppliers both issue their own FT 2026/1,
    /// and refusing the second would be refusing a perfectly ordinary invoice.
    /// </summary>
    [Fact]
    public async Task The_same_number_from_a_different_supplier_is_a_different_document()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var other = new PurchaseOrderSupplierDto("F002", "Outro Fornecedor, Lda", "502222222");

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>()
                .RecordAsync(Invoice(scenario, "FT 2026/1"), "user-1"));

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>()
                .RecordAsync(Invoice(scenario, "FT 2026/1", other), "user-1"));

        var recorded = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().GetAllAsync(scenario.CompanyId));

        recorded.Should().HaveCount(2);
    }

    /// <summary>
    /// And the index really is an index, not merely a service rule: writing straight past the
    /// service has to fail too. This is what would have caught the index being dropped when the
    /// migration was regenerated.
    /// </summary>
    [Fact]
    public async Task The_database_itself_refuses_the_duplicate()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>()
                .RecordAsync(Invoice(scenario, "FT 2026/50"), "user-1"));

        var writing = () => scenario.InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<Erp.Storage.ErpDbContext>();

            // Straight to the table, past every rule the service applies.
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO PurchaseInvoice
                     (Id, CompanyId, SupplierId, DocumentType, SupplierDocumentNumber,
                      SupplierDocumentDate, ReceivedDate, ReverseCharge, Status, WarehouseId,
                      SupplierCode, SupplierName, SupplierTaxId, SupplierCountry,
                      NetTotal, TaxTotal, GrossTotal, CreatedAtUtc)
                 VALUES
                     ({Guid.NewGuid()}, {scenario.CompanyId}, {scenario.SupplierId}, 'FT', 'FT 2026/50',
                      '2026-03-12', '2026-03-14', 0, 0, {scenario.WarehouseId},
                      'F001', 'Fornecedor Teste, Lda', '501234567', 'PT',
                      15, 3.45, 18.45, SYSUTCDATETIME())
                 """);
        });

        // Raw SQL surfaces the server's own error rather than the one EF wraps around SaveChanges,
        // and the index is named so the failure says which guarantee just held.
        await writing.Should().ThrowAsync<SqlException>()
            .WithMessage("*IX_PurchaseInvoice_CompanyId_SupplierTaxId_Number*");
    }
}
