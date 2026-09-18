using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// The exemption reason an article carries, so a zero-rated line does not have to be explained on
/// every document it goes on.
/// </summary>
public class ProductExemptionTests
{
    private readonly IProductStorage _products = Substitute.For<IProductStorage>();
    private readonly IProductFamilyStorage _families = Substitute.For<IProductFamilyStorage>();
    private readonly IProductSubfamilyStorage _subfamilies = Substitute.For<IProductSubfamilyStorage>();
    private readonly IBrandStorage _brands = Substitute.For<IBrandStorage>();
    private readonly IEcoFeeTypeStorage _ecoFeeTypes = Substitute.For<IEcoFeeTypeStorage>();

    private readonly Guid _companyId = Guid.NewGuid();

    public ProductExemptionTests()
    {
        _products.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Product?)null);
    }

    private ProductService CreateService() => new(_products, _families, _subfamilies, _brands, _ecoFeeTypes);

    private CreateProductRequest Request(string taxCode, string? exemptionCode) =>
        new(_companyId,
            "ART001",
            "Artigo de teste",
            100m,
            DefaultTaxCode: taxCode,
            DefaultTaxPercentage: taxCode == "ISE" ? 0m : 23m,
            DefaultTaxExemptionCode: exemptionCode);

    private Product? Added()
    {
        var calls = _products.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IProductStorage.AddAsync))
            .ToList();

        return calls.Count == 0 ? null : (Product)calls[^1].GetArguments()[0]!;
    }

    [Fact]
    public async Task An_exempt_article_keeps_the_reason_it_was_given()
    {
        await CreateService().CreateAsync(Request("ISE", "m07"));

        Added()!.DefaultTaxExemptionCode.Should().Be("M07", "the code is normalised to the table's own");
    }

    [Fact]
    public async Task A_taxed_article_carries_no_reason()
    {
        await CreateService().CreateAsync(Request("NOR", "M07"));

        Added()!.DefaultTaxExemptionCode.Should().BeNull();
    }

    [Fact]
    public async Task A_reason_the_tax_authority_does_not_know_is_refused()
    {
        var act = () => CreateService().CreateAsync(Request("ISE", "M03"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*M03*");
    }
}
