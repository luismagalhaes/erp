using Erp.FiscalPT;
using Erp.FiscalPT.AtWebservice;
using Erp.FiscalPT.AtWebservice.Series;
using Erp.FiscalPT.Documents;
using Erp.SeriesRegistry.Application.Services;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Erp.Common;

namespace Erp.SeriesRegistry.Tests;

public class SeriesServiceTests
{
    private readonly ISeriesStorage _storage = Substitute.For<ISeriesStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAtSeriesClient _atClient = Substitute.For<IAtSeriesClient>();
    private readonly IAtCompanyProfileProvider _atProfiles = Substitute.For<IAtCompanyProfileProvider>();
    private readonly Guid _companyId = Guid.NewGuid();

    public SeriesServiceTests()
    {
        _atProfiles.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AtCompanyProfile("123456789", "Acme", "Rua A", "Lisboa", "1000-000", "1", "secret"));

        _atClient.RegisterAsync(Arg.Any<AtSeriesRegistrationRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new AtSeriesOperationResult(2001, null, "JFTX7RK9", "A"));

        _atClient.CancelAsync(Arg.Any<AtSeriesCancellationRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new AtSeriesOperationResult(2003, null, null, "N"));

        _atClient.FinalizeAsync(Arg.Any<AtSeriesFinalizationRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new AtSeriesOperationResult(2004, null, null, "F"));
    }

    private SeriesService CreateService() =>
        new(_storage, _unitOfWork, _atClient, _atProfiles, NullLogger<SeriesService>.Instance,
            Options.Create(new FiscalOptions()));

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

        var result = await service.CommunicateAsync(series.Id);

        result!.ValidationCode.Should().Be("JFTX7RK9");
        result.CanIssue.Should().BeTrue();
        series.CommunicatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CommunicateAsync_returns_null_for_an_unknown_series()
    {
        var service = CreateService();

        var result = await service.CommunicateAsync(Guid.NewGuid());

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

        var act = () => service.CommunicateAsync(series.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*finalized*");
    }

    [Fact]
    public async Task CommunicateAsync_requires_the_company_to_have_at_credentials_configured()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);
        _atProfiles.GetAsync(_companyId, Arg.Any<CancellationToken>()).Returns((AtCompanyProfile?)null);

        var service = CreateService();

        var act = () => service.CommunicateAsync(series.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AT WDT credentials*");
    }

    [Fact]
    public async Task CancelAsync_cancels_a_communicated_series_with_nothing_issued()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var result = await service.CancelAsync(series.Id);

        result!.Status.Should().Be(nameof(SeriesStatus.Cancelled));
        series.CancelledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAsync_refuses_a_series_that_already_issued_a_document()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        series.TakeNextSequence();
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var act = () => service.CancelAsync(series.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no document issued*");
        await _atClient.DidNotReceive().CancelAsync(
            Arg.Any<AtSeriesCancellationRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_refuses_a_series_never_communicated()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var act = () => service.CancelAsync(series.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FinalizeAsync_finalizes_a_communicated_series()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        series.TakeNextSequence();
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = CreateService();

        var result = await service.FinalizeAsync(series.Id, "fim de ano");

        result!.Status.Should().Be(nameof(SeriesStatus.Finalized));
        series.FinalizedAtUtc.Should().NotBeNull();

        await _atClient.Received(1).FinalizeAsync(
            Arg.Is<AtSeriesFinalizationRequest>(r => r.SeqUltimoDocEmitido == 1 && r.Justificacao == "fim de ano"),
            Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeAsync_surfaces_an_at_rejection()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _storage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        _atClient.FinalizeAsync(Arg.Any<AtSeriesFinalizationRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AtSeriesOperationResult>(new AtSeriesException(4005, "motivo inválido")));

        var service = CreateService();

        var act = () => service.FinalizeAsync(series.Id, null);

        await act.Should().ThrowAsync<AtSeriesException>();
        series.FinalizedAtUtc.Should().BeNull();
    }
}
