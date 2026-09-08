using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The internal document numbers — <c>REC2026/7</c>, <c>ENC2026/7</c>, <c>DEV2026/7</c> — from
/// several requests at once.
/// </summary>
/// <remarks>
/// These carry no fiscal meaning: no series, no signature, no ATCUD. What they do have to be is
/// <b>sequential</b>, because a warehouse worker reading <c>REC2026/7</c> expects a seventh receipt
/// and cannot explain a hole.
/// <para>
/// They used to be derived from the highest number already stored, which cannot give that: several
/// requests read the same maximum, propose the same number, and all but one are refused by the
/// unique index. Six concurrent receipts left one receipt and five failed requests. They now come
/// from a counter row that is locked while the number is taken.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class InternalNumberingConcurrencyTests(SqlServerFixture fixture)
{
    private const int Concurrent = 10;

    /// <summary>The sequence part of "REC2026/7".</summary>
    private static int SequenceOf(string number) =>
        int.Parse(number[(number.LastIndexOf('/') + 1)..]);

    private static async Task<IReadOnlyList<string>> RaceAsync(Func<Task<string>> issue)
    {
        var attempts = Enumerable.Range(0, Concurrent).Select(_ => Task.Run(issue));
        return await Task.WhenAll(attempts);
    }

    [Fact]
    public async Task Receipts_issued_at_the_same_time_are_numbered_in_sequence()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var numbers = await RaceAsync(async () =>
        {
            await using var scope = fixture.CreateScope();

            var receipt = await scope.ServiceProvider.GetRequiredService<IGoodsReceiptService>()
                .CreateAsync(
                    new CreateGoodsReceiptRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        new DateOnly(2026, 3, 15),
                        scenario.WarehouseId,
                        [new GoodsReceiptLineRequest("ART001", "Artigo de teste", 1m, 5m)]),
                    "user-1");

            return receipt.Number;
        });

        numbers.Should().HaveCount(Concurrent, "no request may be refused for want of a number");
        numbers.Should().OnlyHaveUniqueItems();
        numbers.Select(SequenceOf).OrderBy(sequence => sequence)
            .Should().Equal(Enumerable.Range(1, Concurrent), "the sequence has no holes");
    }

    [Fact]
    public async Task Orders_placed_at_the_same_time_are_numbered_in_sequence()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var numbers = await RaceAsync(async () =>
        {
            await using var scope = fixture.CreateScope();

            var order = await scope.ServiceProvider.GetRequiredService<IPurchaseOrderService>()
                .CreateAsync(
                    new CreatePurchaseOrderRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        new DateOnly(2026, 3, 1),
                        scenario.WarehouseId,
                        [new PurchaseOrderLineRequest("ART001", "Artigo de teste", 1m, 5m)]),
                    "user-1");

            return order.Number;
        });

        numbers.Should().HaveCount(Concurrent);
        numbers.Select(SequenceOf).OrderBy(sequence => sequence)
            .Should().Equal(Enumerable.Range(1, Concurrent));
    }

    /// <summary>
    /// Counters restart each year, which is what puts the year in the number and keeps two years
    /// from sharing a sequence.
    /// </summary>
    [Fact]
    public async Task Each_year_counts_from_one_again()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        async Task<string> ReceiveAsync(int year)
        {
            await using var scope = fixture.CreateScope();

            var receipt = await scope.ServiceProvider.GetRequiredService<IGoodsReceiptService>()
                .CreateAsync(
                    new CreateGoodsReceiptRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        new DateOnly(year, 3, 15),
                        scenario.WarehouseId,
                        [new GoodsReceiptLineRequest("ART001", "Artigo de teste", 1m, 5m)]),
                    "user-1");

            return receipt.Number;
        }

        (await ReceiveAsync(2026)).Should().Be("REC2026/1");
        (await ReceiveAsync(2026)).Should().Be("REC2026/2");
        (await ReceiveAsync(2027)).Should().Be("REC2027/1");
        (await ReceiveAsync(2026)).Should().Be("REC2026/3");
    }

    /// <summary>Two companies count on their own, and neither sees the other's numbers.</summary>
    [Fact]
    public async Task Each_company_counts_on_its_own()
    {
        var first = await new CompanyScenario(fixture).CreateAsync();
        var second = await new CompanyScenario(fixture).CreateAsync();

        async Task<string> ReceiveAsync(CompanyScenario scenario)
        {
            await using var scope = fixture.CreateScope();

            var receipt = await scope.ServiceProvider.GetRequiredService<IGoodsReceiptService>()
                .CreateAsync(
                    new CreateGoodsReceiptRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        new DateOnly(2026, 3, 15),
                        scenario.WarehouseId,
                        [new GoodsReceiptLineRequest("ART001", "Artigo de teste", 1m, 5m)]),
                    "user-1");

            return receipt.Number;
        }

        (await ReceiveAsync(first)).Should().Be("REC2026/1");
        (await ReceiveAsync(second)).Should().Be("REC2026/1");
        (await ReceiveAsync(first)).Should().Be("REC2026/2");
    }
}
