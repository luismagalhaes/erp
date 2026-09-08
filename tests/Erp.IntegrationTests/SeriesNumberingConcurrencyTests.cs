using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Numbering a series from several requests at once.
/// </summary>
/// <remarks>
/// This is the test the whole project was missing. The rule — no gaps, no repeats, and each
/// document signed onto the one before it — rests entirely on
/// <c>SELECT ... WITH (UPDLOCK, ROWLOCK)</c> holding the series row for the length of the issuing
/// transaction. Substituted storage cannot express that: it hands both callers the same row and
/// both succeed, so the tests pass whether or not the lock is there.
/// <para>
/// A gap or a repeat here is not a bug to be fixed later. It is a broken chain, and the certified
/// document numbering is the one thing in this system that must never be wrong.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SeriesNumberingConcurrencyTests(SqlServerFixture fixture)
{
    private const int Concurrent = 12;

    private static CreateInvoiceRequest Invoice(Guid companyId, Guid seriesId, Guid warehouseId) =>
        new(companyId,
            seriesId,
            new DateOnly(2026, 3, 15),
            new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
            [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, 100m, "NOR", 23m)],
            WarehouseId: warehouseId);

    [Fact]
    public async Task Issuing_at_the_same_time_never_repeats_or_skips_a_number()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var seriesId = await scenario.GivenCommunicatedSeriesAsync(stockEffect: "None");

        // Each issue gets its own scope, which is what each gets in the API: its own context, its
        // own connection. Anything less and they would queue up in the client instead of the server.
        var issuing = Enumerable.Range(0, Concurrent).Select(_ => Task.Run(async () =>
        {
            await using var scope = fixture.CreateScope();
            var documents = scope.ServiceProvider.GetRequiredService<ISalesDocumentService>();

            return await documents.IssueAsync(
                Invoice(scenario.CompanyId, seriesId, scenario.WarehouseId), "user-1");
        }));

        var issued = await Task.WhenAll(issuing);

        var numbers = issued.Select(document => document.DocumentNumber).ToList();

        numbers.Should().OnlyHaveUniqueItems("a repeated number is two documents claiming to be one");
        numbers.Should().HaveCount(Concurrent);

        // The sequence part of "FT A2026/7". Contiguous from 1, with nothing missing.
        var sequences = numbers
            .Select(number => int.Parse(number[(number.LastIndexOf('/') + 1)..]))
            .OrderBy(sequence => sequence)
            .ToList();

        sequences.Should().Equal(Enumerable.Range(1, Concurrent));
    }

    /// <summary>
    /// The signature chain, which is the same lock seen from the other side: each document signs
    /// the hash of the one before it, so two issuing at once must not read the same previous hash.
    /// </summary>
    [Fact]
    public async Task Issuing_at_the_same_time_leaves_one_unbroken_chain()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var seriesId = await scenario.GivenCommunicatedSeriesAsync(stockEffect: "None");

        var issuing = Enumerable.Range(0, Concurrent).Select(_ => Task.Run(async () =>
        {
            await using var scope = fixture.CreateScope();
            var documents = scope.ServiceProvider.GetRequiredService<ISalesDocumentService>();

            return await documents.IssueAsync(
                Invoice(scenario.CompanyId, seriesId, scenario.WarehouseId), "user-1");
        }));

        await Task.WhenAll(issuing);

        var chain = await scenario.InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ErpDbContext>();

            // Read from the table, not through the service: what matters is what was written, and
            // the detail DTO deliberately exposes only the four printed characters of the hash.
            var stored = await context.Set<SalesDocument>()
                .AsNoTracking()
                .Where(document => document.SeriesId == seriesId)
                .OrderBy(document => document.SequenceNumber)
                .Select(document => new
                {
                    document.SequenceNumber,
                    document.Hash,
                    document.PreviousHash
                })
                .ToListAsync();

            return stored;
        });

        chain.Should().HaveCount(Concurrent);
        chain[0].PreviousHash.Should().BeEmpty("the first document of a series chains onto nothing");

        foreach (var (previous, current) in chain.Zip(chain.Skip(1)))
        {
            current.PreviousHash.Should().Be(
                previous.Hash,
                "document {0} must sign onto {1}",
                current.SequenceNumber,
                previous.SequenceNumber);
        }

        chain.Select(link => link.Hash).Should().OnlyHaveUniqueItems();
    }
}
