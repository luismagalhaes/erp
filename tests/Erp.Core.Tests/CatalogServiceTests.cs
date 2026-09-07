using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class ProductServiceTests
{
    private readonly IProductStorage _products = Substitute.For<IProductStorage>();
    private readonly IProductFamilyStorage _families = Substitute.For<IProductFamilyStorage>();
    private readonly IProductSubfamilyStorage _subfamilies = Substitute.For<IProductSubfamilyStorage>();
    private readonly IBrandStorage _brands = Substitute.For<IBrandStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private ProductService CreateService() => new(_products, _families, _subfamilies, _brands);

    private CreateProductRequest Request(
        string code = "ART001",
        string productType = "P",
        string taxCode = "NOR",
        string region = "PT",
        decimal unitPrice = 100m,
        Guid? familyId = null,
        Guid? subfamilyId = null,
        Guid? brandId = null,
        string inventoryCategory = "M") =>
        new(_companyId, code, "Cadeira", unitPrice, productType, "UN", region, taxCode, 23m,
            null, familyId, subfamilyId, brandId, 0m, inventoryCategory);

    private (ProductFamily Family, ProductSubfamily Subfamily) GivenClassification()
    {
        var family = new ProductFamily { CompanyId = _companyId, Code = "MOB", Name = "Mobiliário" };
        var subfamily = new ProductSubfamily
        {
            CompanyId = _companyId,
            FamilyId = family.Id,
            Family = family,
            Code = "CAD",
            Name = "Cadeiras"
        };

        _families.GetByIdAsync(family.Id, Arg.Any<CancellationToken>()).Returns(family);
        _subfamilies.GetByIdAsync(subfamily.Id, Arg.Any<CancellationToken>()).Returns(subfamily);

        return (family, subfamily);
    }

    [Fact]
    public async Task CreateAsync_stores_a_trimmed_active_product()
    {
        var created = await CreateService().CreateAsync(Request(code: "  ART001  "));

        created.ProductCode.Should().Be("ART001");
        created.IsActive.Should().BeTrue();
        await _products.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_code_in_the_same_company()
    {
        _products.CodeExistsAsync(_companyId, "ART001", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateService().CreateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Theory]
    [InlineData("X", "NOR", "PT")]
    [InlineData("P", "XXX", "PT")]
    [InlineData("P", "NOR", "ES")]
    public async Task CreateAsync_rejects_values_outside_the_saft_tables(string productType, string taxCode, string region)
    {
        var act = () => CreateService().CreateAsync(Request(productType: productType, taxCode: taxCode, region: region));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// The inventory category is a different vocabulary from the SAF-T product type: the letters
    /// overlap but mean different things, so a value from one is not valid in the other.
    /// </summary>
    [Theory]
    [InlineData("M")]
    [InlineData("P")]
    [InlineData("A")]
    [InlineData("S")]
    [InlineData("T")]
    [InlineData("B")]
    public async Task CreateAsync_accepts_every_inventory_category_of_the_valued_schema(string category)
    {
        var created = await CreateService().CreateAsync(Request(inventoryCategory: category));

        created.InventoryCategory.Should().Be(category);
    }

    [Theory]
    [InlineData("O")]
    [InlineData("I")]
    [InlineData("")]
    [InlineData("X")]
    public async Task CreateAsync_rejects_a_category_the_inventory_schema_does_not_know(string category)
    {
        var act = () => CreateService().CreateAsync(Request(inventoryCategory: category));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*inventory category*");
    }

    /// <summary>Merchandise is what a product is until someone says otherwise.</summary>
    [Fact]
    public async Task CreateAsync_defaults_the_category_to_merchandise()
    {
        var request = new CreateProductRequest(_companyId, "ART001", "Cadeira", 100m);

        var created = await CreateService().CreateAsync(request);

        created.InventoryCategory.Should().Be("M");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_negative_price()
    {
        var act = () => CreateService().CreateAsync(Request(unitPrice: -1m));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public async Task CreateAsync_accepts_a_family_with_its_own_subfamily()
    {
        var (family, subfamily) = GivenClassification();

        var created = await CreateService().CreateAsync(
            Request(familyId: family.Id, subfamilyId: subfamily.Id));

        created.FamilyId.Should().Be(family.Id);
        created.SubfamilyId.Should().Be(subfamily.Id);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_subfamily_from_another_family()
    {
        var (_, subfamily) = GivenClassification();
        var otherFamily = new ProductFamily { CompanyId = _companyId, Code = "ILU", Name = "Iluminação" };
        _families.GetByIdAsync(otherFamily.Id, Arg.Any<CancellationToken>()).Returns(otherFamily);

        var act = () => CreateService().CreateAsync(
            Request(familyId: otherFamily.Id, subfamilyId: subfamily.Id));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_subfamily_without_its_family()
    {
        var (_, subfamily) = GivenClassification();

        var act = () => CreateService().CreateAsync(Request(subfamilyId: subfamily.Id));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*requires the family*");
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_brand()
    {
        var act = () => CreateService().CreateAsync(Request(brandId: Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*brand*");
    }

    [Fact]
    public async Task UpdateAsync_never_changes_the_product_code()
    {
        var product = new Product { CompanyId = _companyId, ProductCode = "ART001", Description = "Antigo" };
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var updated = await CreateService().UpdateAsync(
            product.Id,
            new UpdateProductRequest("Novo", 25m, "S", "HR", "PT", "RED", 6m, null, null, null, null, false));

        updated!.ProductCode.Should().Be("ART001");
        updated.Description.Should().Be("Novo");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_changes_the_inventory_category()
    {
        var product = new Product { CompanyId = _companyId, ProductCode = "ART001", Description = "Cadeira" };
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var updated = await CreateService().UpdateAsync(
            product.Id,
            new UpdateProductRequest("Cadeira", 25m, "P", "UN", "PT", "NOR", 23m, null, null, null, null, true, 0m, "A"));

        updated!.InventoryCategory.Should().Be("A");
        product.InventoryCategory.Should().Be("A");
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_category_the_inventory_schema_does_not_know()
    {
        var product = new Product { CompanyId = _companyId, ProductCode = "ART001", Description = "Cadeira" };
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var act = () => CreateService().UpdateAsync(
            product.Id,
            new UpdateProductRequest("Cadeira", 25m, "P", "UN", "PT", "NOR", 23m, null, null, null, null, true, 0m, "X"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*inventory category*");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_product()
    {
        var result = await CreateService().UpdateAsync(
            Guid.NewGuid(),
            new UpdateProductRequest("Novo", 1m, "P", "UN", "PT", "NOR", 23m, null, null, null, null, true));

        result.Should().BeNull();
    }
}

public class ClassificationServiceTests
{
    private readonly IProductFamilyStorage _families = Substitute.For<IProductFamilyStorage>();
    private readonly IProductSubfamilyStorage _subfamilies = Substitute.For<IProductSubfamilyStorage>();
    private readonly IBrandStorage _brands = Substitute.For<IBrandStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task Family_creation_rejects_a_duplicate_code()
    {
        _families.CodeExistsAsync(_companyId, "MOB", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new ProductFamilyService(_families)
            .CreateAsync(new CreateProductFamilyRequest(_companyId, "MOB", "Mobiliário"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Family_listing_reports_how_many_subfamilies_it_holds()
    {
        var family = new ProductFamily { CompanyId = _companyId, Code = "MOB", Name = "Mobiliário" };
        family.Subfamilies.Add(new ProductSubfamily { Code = "CAD", Name = "Cadeiras" });
        family.Subfamilies.Add(new ProductSubfamily { Code = "MES", Name = "Mesas" });

        _families.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns([family]);

        var result = await new ProductFamilyService(_families).GetAllAsync(_companyId);

        result.Single().SubfamilyCount.Should().Be(2);
    }

    [Fact]
    public async Task Subfamily_creation_requires_an_existing_family()
    {
        var act = () => new ProductSubfamilyService(_subfamilies, _families)
            .CreateAsync(new CreateProductSubfamilyRequest(_companyId, Guid.NewGuid(), "CAD", "Cadeiras"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*family was not found*");
    }

    [Fact]
    public async Task Subfamily_creation_carries_the_family_name_back()
    {
        var family = new ProductFamily { CompanyId = _companyId, Code = "MOB", Name = "Mobiliário" };
        _families.GetByIdAsync(family.Id, Arg.Any<CancellationToken>()).Returns(family);

        var created = await new ProductSubfamilyService(_subfamilies, _families)
            .CreateAsync(new CreateProductSubfamilyRequest(_companyId, family.Id, " CAD ", " Cadeiras "));

        created.Code.Should().Be("CAD");
        created.Name.Should().Be("Cadeiras");
        created.FamilyName.Should().Be("Mobiliário");
    }

    [Fact]
    public async Task Brand_update_returns_null_for_an_unknown_brand()
    {
        var result = await new BrandService(_brands).UpdateAsync(Guid.NewGuid(), new UpdateBrandRequest("Nova", true));

        result.Should().BeNull();
    }
}

public class PartnerServiceTests
{
    private readonly ICustomerStorage _customers = Substitute.For<ICustomerStorage>();
    private readonly ISupplierStorage _suppliers = Substitute.For<ISupplierStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private CreatePartnerRequest Request(string code = "C001", string name = "  Alfa  ", string taxId = " 500000001 ") =>
        new(_companyId, code, name, taxId);

    [Fact]
    public async Task Customer_creation_trims_and_activates()
    {
        var created = await new CustomerService(_customers).CreateAsync(Request());

        created.Name.Should().Be("Alfa");
        created.TaxId.Should().Be("500000001");
        created.IsActive.Should().BeTrue();
        await _customers.Received(1).AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Customer_creation_rejects_a_duplicate_code()
    {
        _customers.CodeExistsAsync(_companyId, "C001", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new CustomerService(_customers).CreateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Theory]
    [InlineData("", "Alfa", "500000001")]
    [InlineData("C001", "  ", "500000001")]
    [InlineData("C001", "Alfa", "")]
    public async Task Customer_creation_requires_code_name_and_tax_id(string code, string name, string taxId)
    {
        var act = () => new CustomerService(_customers).CreateAsync(Request(code, name, taxId));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Supplier_creation_follows_the_same_rules()
    {
        _suppliers.CodeExistsAsync(_companyId, "F001", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => new SupplierService(_suppliers).CreateAsync(Request(code: "F001"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Supplier_update_changes_the_contact_and_the_state()
    {
        var supplier = new Supplier { CompanyId = _companyId, Code = "F001", Name = "Beta", TaxId = "500000002" };
        _suppliers.GetByIdAsync(supplier.Id, Arg.Any<CancellationToken>()).Returns(supplier);

        var updated = await new SupplierService(_suppliers).UpdateAsync(
            supplier.Id,
            new UpdatePartnerRequest("Beta II", "500000009", "Rua Um", "1000-001", "Lisboa", "PT", "b@b.pt", null, false));

        updated!.Name.Should().Be("Beta II");
        updated.City.Should().Be("Lisboa");
        updated.Phone.Should().BeNull();
        updated.IsActive.Should().BeFalse();
        supplier.UpdatedAtUtc.Should().NotBeNull();
    }
}
