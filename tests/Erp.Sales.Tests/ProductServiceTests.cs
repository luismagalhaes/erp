using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

public class ProductServiceTests
{
    private readonly IProductStorage _storage = Substitute.For<IProductStorage>();
    private readonly ISalesUnitOfWork _unitOfWork = Substitute.For<ISalesUnitOfWork>();
    private readonly Guid _companyId = Guid.NewGuid();

    private ProductService CreateService() => new(_storage, _unitOfWork);

    private CreateProductRequest Request(
        string code = "ART001",
        string description = "Artigo de teste",
        decimal unitPrice = 100m,
        string productType = "P",
        string taxCode = "NOR",
        string region = "PT") =>
        new(_companyId, code, description, unitPrice, productType, "UN", region, taxCode, 23m);

    [Fact]
    public async Task CreateAsync_stores_a_trimmed_active_product()
    {
        var service = CreateService();

        var created = await service.CreateAsync(Request(code: "  ART001  ", description: "  Cadeira  "));

        created.ProductCode.Should().Be("ART001");
        created.Description.Should().Be("Cadeira");
        created.IsActive.Should().BeTrue();
        await _storage.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_code_in_the_same_company()
    {
        _storage.CodeExistsAsync(_companyId, "ART001", Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();

        var act = () => service.CreateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Theory]
    [InlineData("X", "NOR", "PT")]
    [InlineData("P", "XXX", "PT")]
    [InlineData("P", "NOR", "ES")]
    public async Task CreateAsync_rejects_values_outside_the_saft_tables(string productType, string taxCode, string region)
    {
        var service = CreateService();

        var act = () => service.CreateAsync(Request(productType: productType, taxCode: taxCode, region: region));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_negative_price()
    {
        var service = CreateService();

        var act = () => service.CreateAsync(Request(unitPrice: -1m));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public async Task UpdateAsync_changes_the_description_price_and_state()
    {
        var product = new Product
        {
            CompanyId = _companyId,
            ProductCode = "ART001",
            Description = "Antigo",
            UnitPrice = 10m
        };

        _storage.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var service = CreateService();

        var updated = await service.UpdateAsync(
            product.Id,
            new UpdateProductRequest("Novo", 25m, "S", "HR", "PT", "RED", 6m, false));

        updated!.Description.Should().Be("Novo");
        updated.UnitPrice.Should().Be(25m);
        updated.IsActive.Should().BeFalse();
        product.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_never_changes_the_product_code()
    {
        var product = new Product { CompanyId = _companyId, ProductCode = "ART001", Description = "Antigo" };
        _storage.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var service = CreateService();

        var updated = await service.UpdateAsync(
            product.Id,
            new UpdateProductRequest("Novo", 25m, "P", "UN", "PT", "NOR", 23m, true));

        updated!.ProductCode.Should().Be("ART001");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_product()
    {
        var service = CreateService();

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateProductRequest("Novo", 1m, "P", "UN", "PT", "NOR", 23m, true));

        result.Should().BeNull();
    }
}
