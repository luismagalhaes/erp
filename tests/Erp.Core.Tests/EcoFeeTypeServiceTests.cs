using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// An eco-fee ("Ecovalor") is always invoiced as its own pseudo-product — the law requires it on
/// its own line — so creating or updating one keeps that product in step.
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
    public async Task CreateAsync_creates_the_fee_and_its_pseudo_product_together()
    {
        Product? addedProduct = null;
        _products.When(x => x.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>()))
            .Do(call => addedProduct = call.Arg<Product>());

        var created = await CreateService().CreateAsync(Request());

        addedProduct.Should().NotBeNull();
        addedProduct!.ProductCode.Should().Be("ECOVALOR-BAT");
        addedProduct.ProductType.Should().Be("I");
        addedProduct.UnitPrice.Should().Be(0.03m);
        addedProduct.UnitOfMeasure.Should().Be("UN");

        created.FeeProductCode.Should().Be("ECOVALOR-BAT");
        created.CalculationBasis.Should().Be("PerUnit");
    }

    /// <summary>A fee billed by weight invoices its pseudo-product in kilograms, not units.</summary>
    [Fact]
    public async Task CreateAsync_uses_kilograms_for_a_per_kg_fee()
    {
        Product? addedProduct = null;
        _products.When(x => x.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>()))
            .Do(call => addedProduct = call.Arg<Product>());

        await CreateService().CreateAsync(Request("ECOVALOR-OLEO", "PerKg", 0.10m));

        addedProduct!.UnitOfMeasure.Should().Be("KG");
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

    /// <summary>Raising the rate updates the pseudo-product too, since it is not an article a user
    /// edits separately.</summary>
    [Fact]
    public async Task UpdateAsync_keeps_the_pseudo_product_in_step_with_the_fee()
    {
        var feeProduct = new Product { CompanyId = _companyId, ProductCode = "ECOVALOR-BAT", Description = "Old", UnitPrice = 0.03m };
        var ecoFeeType = new EcoFeeType
        {
            CompanyId = _companyId,
            Code = "ECOVALOR-BAT",
            Description = "Old",
            CalculationBasis = EcoFeeCalculationBasis.PerUnit,
            Rate = 0.03m,
            ManagingEntityName = "Ecopilhas / Amb3E",
            FeeProductId = feeProduct.Id,
            FeeProduct = feeProduct
        };

        _ecoFeeTypes.GetByIdAsync(ecoFeeType.Id, Arg.Any<CancellationToken>()).Returns(ecoFeeType);

        var updated = await CreateService().UpdateAsync(
            ecoFeeType.Id,
            new UpdateEcoFeeTypeRequest("Ecovalor - Pilhas (2026)", 0.05m, "Ecopilhas / Amb3E", true));

        updated!.Rate.Should().Be(0.05m);
        feeProduct.UnitPrice.Should().Be(0.05m);
        feeProduct.Description.Should().Be("Ecovalor - Pilhas (2026)");
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
