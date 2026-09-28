using Erp.Sales.Application.Services;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

/// <summary>
/// Net sales as the assistant reports them: what was invoiced minus what was credited, per month
/// and per customer, with the year on year comparison worked out here rather than by the model.
/// </summary>
public class SalesAnalyticsServiceTests
{
    private readonly ISalesDocumentStorage _documents = Substitute.For<ISalesDocumentStorage>();
    private readonly List<SalesFigure> _figures = [];
    private readonly Guid _companyId = Guid.NewGuid();

    public SalesAnalyticsServiceTests()
    {
        // The storage filters by period; the substitute does the same so the tests see what the
        // service would.
        _documents.GetSalesFiguresAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var start = call.ArgAt<DateOnly>(1);
                var end = call.ArgAt<DateOnly>(2);

                return (IReadOnlyList<SalesFigure>)[.. _figures.Where(x => x.DocumentDate >= start && x.DocumentDate <= end)];
            });
    }

    private SalesAnalyticsService CreateService() => new(_documents);

    private void GivenSale(string type, DateOnly date, decimal net, string customerTaxId = "500000001", string customerName = "Cliente A") =>
        _figures.Add(new SalesFigure(date, type, customerName, customerTaxId, net, net * 1.23m));

    [Fact]
    public async Task Monthly_sales_sums_each_month_and_lists_months_with_no_sales()
    {
        GivenSale("FT", new DateOnly(2026, 1, 10), 100m);
        GivenSale("FR", new DateOnly(2026, 1, 20), 50m);
        GivenSale("FT", new DateOnly(2026, 3, 5), 200m);

        var months = await CreateService().GetMonthlySalesAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        months.Select(x => x.Month).Should().Equal(1, 2, 3);
        months[0].NetTotal.Should().Be(150m);
        months[0].DocumentCount.Should().Be(2);
        months[1].NetTotal.Should().Be(0m);
        months[1].DocumentCount.Should().Be(0);
        months[2].NetTotal.Should().Be(200m);
    }

    [Fact]
    public async Task Credit_notes_take_away_from_the_month_they_were_issued_in()
    {
        GivenSale("FT", new DateOnly(2026, 2, 10), 300m);
        GivenSale("NC", new DateOnly(2026, 2, 25), 100m);

        var months = await CreateService().GetMonthlySalesAsync(
            _companyId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        months.Should().ContainSingle();
        months[0].NetTotal.Should().Be(200m);
        months[0].GrossTotal.Should().Be(246m);
    }

    [Fact]
    public async Task Monthly_sales_rejects_an_end_date_before_the_start_date()
    {
        var act = () => CreateService().GetMonthlySalesAsync(
            _companyId, new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Comparing_years_lines_up_the_same_month_of_both_years()
    {
        GivenSale("FT", new DateOnly(2025, 1, 10), 100m);
        GivenSale("FT", new DateOnly(2026, 1, 10), 150m);
        GivenSale("FT", new DateOnly(2025, 2, 10), 200m);
        GivenSale("FT", new DateOnly(2026, 2, 10), 100m);

        var comparison = await CreateService().CompareYearsAsync(_companyId, 2026, 2025);

        comparison.Months.Should().HaveCount(12);

        comparison.Months[0].CurrentNetTotal.Should().Be(150m);
        comparison.Months[0].PreviousNetTotal.Should().Be(100m);
        comparison.Months[0].Difference.Should().Be(50m);
        comparison.Months[0].ChangePercent.Should().Be(50m);

        comparison.Months[1].Difference.Should().Be(-100m);
        comparison.Months[1].ChangePercent.Should().Be(-50m);

        comparison.CurrentNetTotal.Should().Be(250m);
        comparison.PreviousNetTotal.Should().Be(300m);
        comparison.ChangePercent.Should().Be(-16.7m);
    }

    [Fact]
    public async Task A_month_that_sold_nothing_the_year_before_has_no_percentage()
    {
        GivenSale("FT", new DateOnly(2026, 4, 10), 80m);

        var comparison = await CreateService().CompareYearsAsync(_companyId, 2026, 2025);

        comparison.Months[3].PreviousNetTotal.Should().Be(0m);
        comparison.Months[3].Difference.Should().Be(80m);
        comparison.Months[3].ChangePercent.Should().BeNull();
    }

    [Fact]
    public async Task Top_customers_are_ranked_by_net_sales_after_credit_notes()
    {
        GivenSale("FT", new DateOnly(2026, 1, 10), 500m, "500000001", "Grande Cliente");
        GivenSale("NC", new DateOnly(2026, 1, 15), 450m, "500000001", "Grande Cliente");
        GivenSale("FT", new DateOnly(2026, 1, 12), 200m, "500000002", "Cliente Fiel");
        GivenSale("FT", new DateOnly(2026, 2, 12), 100m, "500000002", "Cliente Fiel");
        GivenSale("FT", new DateOnly(2026, 2, 1), 10m, "500000003", "Cliente Pequeno");

        var top = await CreateService().GetTopCustomersAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), count: 2);

        top.Select(x => x.CustomerName).Should().Equal("Cliente Fiel", "Grande Cliente");
        top[0].NetTotal.Should().Be(300m);
        top[0].DocumentCount.Should().Be(2);
        top[1].NetTotal.Should().Be(50m);
    }
}
