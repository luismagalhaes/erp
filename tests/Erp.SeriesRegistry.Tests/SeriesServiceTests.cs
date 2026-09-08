using Erp.FiscalPT.Documents;
using Erp.SeriesRegistry.Application.Services;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;
using Erp.Common;

namespace Erp.SeriesRegistry.Tests;

public class SeriesServiceTests
{
    private readonly ISeriesStorage _storage = Substitute.For<ISeriesStorage>();
    private readonly IErpUnitOfWork _unitOfWork = Substitute.For<IErpUnitOfWork>();
    private readonly Guid _companyId = Guid.NewGuid();

    private SeriesService CreateService() => new(_storage, _unitOfWork);

    [Fact]
    public async Task CreateAsync_starts_the_series_unable_to_issue()
    {
        var service = CreateService();

        var created = await service.CreateAsync(
            new CreateSeriesRequest(_companyId, "FT", "A2026"), "user-1");

        created.CanIssue.Should().BeFalse("a series cannot issue before it is communicated");
        created.Status.Should().Be(nameof(SeriesStatus.Created));
        created.CurrentSequence.Should().Be(0);
    }

    /// <summary>Every family numbered from a series: faturação, movimentação e recibos.</summary>
    [Theory]
    [InlineData("FT")]
    [InlineData("FS")]
    [InlineData("FR")]
    [InlineData("NC")]
    [InlineData("ND")]
    [InlineData("GR")]
    [InlineData("GT")]
    [InlineData("GA")]
    [InlineData("GC")]
    [InlineData("GD")]
    [InlineData("RC")]
    [InlineData("RG")]
    public async Task CreateAsync_accepts_every_supported_document_type(string documentType)
    {
        var service = CreateService();

        var created = await service.CreateAsync(
            new CreateSeriesRequest(_companyId, documentType, "A2026"), "user-1");

        created.DocumentType.Should().Be(documentType);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unsupported_document_type()
    {
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateSeriesRequest(_companyId, "XX", "A2026"), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*document type*");
    }

    /// <summary>
    /// A self-billing series numbers invoices issued on a supplier's behalf. Nothing else may use
    /// it, and its documents go in a SAF-T file of their own.
    /// </summary>
    [Fact]
    public async Task CreateAsync_marks_a_series_as_self_billing()
    {
        var service = CreateService();

        var created = await service.CreateAsync(
            new CreateSeriesRequest(_companyId, "FT", "AF2026", SelfBilling: true), "user-1");

        created.SelfBilling.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_leaves_an_ordinary_series_out_of_self_billing()
    {
        var service = CreateService();

        var created = await service.CreateAsync(new CreateSeriesRequest(_companyId, "FT", "A2026"), "user-1");

        created.SelfBilling.Should().BeFalse();
    }

    /// <summary>Self-billing is a way of invoicing; the regime provides for nothing else.</summary>
    [Theory]
    [InlineData("GR")]
    [InlineData("GT")]
    [InlineData("RG")]
    public async Task CreateAsync_rejects_self_billing_on_a_type_that_is_not_invoicing(string documentType)
    {
        var service = CreateService();

        var act = () => service.CreateAsync(
            new CreateSeriesRequest(_companyId, documentType, "AF2026", SelfBilling: true), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot be self-billed*");
    }

    [Fact]
    public async Task CreateAsync_rejects_an_empty_series_code()
    {
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateSeriesRequest(_companyId, "FT", "   "), "user-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_series_for_the_same_document_type()
    {
        _storage.ExistsAsync(_companyId, "FT", "A2026", Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateSeriesRequest(_companyId, "FT", "A2026"), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CommunicateAsync_stores_the_validation_code_and_unlocks_issuing()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var result = await service.CommunicateAsync(series.Id, " JFTX7RK9 ");

        result!.ValidationCode.Should().Be("JFTX7RK9");
        result.CanIssue.Should().BeTrue();
        series.CommunicatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CommunicateAsync_returns_null_for_an_unknown_series()
    {
        var service = CreateService();

        var result = await service.CommunicateAsync(Guid.NewGuid(), "JFTX7RK9");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CommunicateAsync_refuses_a_finalized_series()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        series.Finalize(DateTime.UtcNow);
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var act = () => service.CommunicateAsync(series.Id, "OTHER123");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*finalized*");
    }
}
