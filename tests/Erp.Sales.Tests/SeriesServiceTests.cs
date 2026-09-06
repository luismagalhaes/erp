using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

public class SeriesServiceTests
{
    private readonly ISeriesStorage _storage = Substitute.For<ISeriesStorage>();
    private readonly ISalesUnitOfWork _unitOfWork = Substitute.For<ISalesUnitOfWork>();
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

    [Fact]
    public async Task CreateAsync_rejects_an_unsupported_document_type()
    {
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateSeriesRequest(_companyId, "XX", "A2026"), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*document type*");
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
