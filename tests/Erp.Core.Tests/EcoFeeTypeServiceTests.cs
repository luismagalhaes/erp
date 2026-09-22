using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// An eco-fee ("Ecovalor") is invoiced as its own document line, built straight from its own code,
/// description and rate — it needs no catalog article of its own, only a code an article cannot reuse.
/// </summary>
public class EcoFeeTypeServiceTests
{
    private readonly IEcoFeeTypeStorage _ecoFeeTypes = Substitute.For<IEcoFeeTypeStorage>();
    private readonly IProductStorage _products = Substitute.For<IProductStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private EcoFeeTypeService CreateService() => new(_ecoFeeTypes, _products);

    private CreateEcoFeeTypeRequest Request(
        string code = "ECOVALOR-BAT",
        string calculationBasis = "PerUnit",
        decimal rate = 0.03m) =>
        new(_companyId, code, "Ecovalor - Pilhas e baterias", calculationBasis, rate, "Ecopilhas / Amb3E");

    [Fact]
    public async Task CreateAsync_creates_the_fee_from_its_own_fields()
    {
        var created = await CreateService().CreateAsync(Request());

        created.Code.Should().Be("ECOVALOR-BAT");
        created.CalculationBasis.Should().Be("PerUnit");
        created.Rate.Should().Be(0.03m);
        created.ManagingEntityName.Should().Be("Ecopilhas / Amb3E");
    }

    /// <summary>No pseudo-product exists any more, so creating a fee never touches the article catalogue.</summary>
    [Fact]
    public async Task CreateAsync_does_not_create_a_catalog_article()
    {
        await CreateService().CreateAsync(Request());

        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_refuses_a_code_already_used_by_another_eco_fee()
    {
        _ecoFeeTypes.CodeExistsAsync(_companyId, "ECOVALOR-BAT", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateService().CreateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    /// <summary>The fee's own line uses the same code, so an article cannot already hold it.</summary>
    [Fact]
    public async Task CreateAsync_refuses_a_code_already_used_by_an_article()
    {
        _products.CodeExistsAsync(_companyId, "ECOVALOR-BAT", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateService().CreateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already used by an article*");
    }

    [Fact]
    public async Task CreateAsync_refuses_a_zero_or_negative_rate()
    {
        var act = () => CreateService().CreateAsync(Request(rate: 0m));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public async Task CreateAsync_refuses_an_unknown_calculation_basis()
    {
        var act = () => CreateService().CreateAsync(Request(calculationBasis: "PerLitre"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*calculation basis*");
    }

    [Fact]
    public async Task UpdateAsync_changes_the_rate_and_description()
    {
        var ecoFeeType = new EcoFeeType
        {
            CompanyId = _companyId,
            Code = "ECOVALOR-BAT",
            Description = "Old",
            CalculationBasis = EcoFeeCalculationBasis.PerUnit,
            Rate = 0.03m,
            ManagingEntityName = "Ecopilhas / Amb3E"
        };

        _ecoFeeTypes.GetByIdAsync(ecoFeeType.Id, Arg.Any<CancellationToken>()).Returns(ecoFeeType);

        var updated = await CreateService().UpdateAsync(
            ecoFeeType.Id,
            new UpdateEcoFeeTypeRequest("Ecovalor - Pilhas (2026)", 0.05m, "Ecopilhas / Amb3E", true));

        updated!.Rate.Should().Be(0.05m);
        updated.Description.Should().Be("Ecovalor - Pilhas (2026)");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_eco_fee_that_does_not_exist()
    {
        _ecoFeeTypes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EcoFeeType?)null);

        var updated = await CreateService().UpdateAsync(
            Guid.NewGuid(), new UpdateEcoFeeTypeRequest("Descrição", 0.05m, "Entidade", true));

        updated.Should().BeNull();
    }
}
