using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class CompanyAdminServiceTests
{
    private readonly ICompanyStorage _storage = Substitute.For<ICompanyStorage>();

    private CompanyAdminService CreateService() => new(_storage);

    [Fact]
    public async Task GetAllAsync_maps_every_company()
    {
        _storage.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Company>
        {
            new() { Id = Guid.NewGuid(), Name = "Alfa", TaxId = "500000001", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Beta", TaxId = "500000002", IsActive = false }
        });

        var result = await CreateService().GetAllAsync();

        result.Should().HaveCount(2);
        result.Select(x => x.Name).Should().Equal("Alfa", "Beta");
        result.Single(x => x.Name == "Beta").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_trims_the_input_and_activates_the_company()
    {
        var service = CreateService();

        var created = await service.CreateAsync(
            new CreateCompanyRequest("  Alfa  ", " 500000001 ", "  Alfa, Lda  ", " a@b.pt ", " 210000000 "));

        created.Name.Should().Be("Alfa");
        created.TaxId.Should().Be("500000001");
        created.LegalName.Should().Be("Alfa, Lda");
        created.Email.Should().Be("a@b.pt");
        created.IsActive.Should().BeTrue();
        await _storage.Received(1).AddAsync(Arg.Any<Company>(), Arg.Any<CancellationToken>());
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_tax_id()
    {
        _storage.TaxIdExistsAsync("500000001", null, Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateCompanyRequest("Alfa", "500000001"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        await _storage.DidNotReceive().AddAsync(Arg.Any<Company>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "500000001")]
    [InlineData("   ", "500000001")]
    [InlineData("Alfa", "")]
    [InlineData("Alfa", "  ")]
    public async Task CreateAsync_requires_a_name_and_a_tax_id(string name, string taxId)
    {
        var service = CreateService();

        var act = () => service.CreateAsync(new CreateCompanyRequest(name, taxId));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_changes_the_fields_and_stamps_the_update()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Alfa", TaxId = "500000001" };
        _storage.GetByIdAsync(company.Id, Arg.Any<CancellationToken>()).Returns(company);
        var service = CreateService();

        var updated = await service.UpdateAsync(
            company.Id,
            new UpdateCompanyRequest("Alfa II", "500000009", "Alfa II, Lda", null, null, false));

        updated!.Name.Should().Be("Alfa II");
        updated.TaxId.Should().Be("500000009");
        updated.IsActive.Should().BeFalse();
        updated.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_allows_a_company_to_keep_its_own_tax_id()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Alfa", TaxId = "500000001" };
        _storage.GetByIdAsync(company.Id, Arg.Any<CancellationToken>()).Returns(company);
        _storage.TaxIdExistsAsync("500000001", company.Id, Arg.Any<CancellationToken>()).Returns(false);
        var service = CreateService();

        var updated = await service.UpdateAsync(
            company.Id,
            new UpdateCompanyRequest("Alfa", "500000001", null, null, null, true));

        updated.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_tax_id_used_by_another_company()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Alfa", TaxId = "500000001" };
        _storage.GetByIdAsync(company.Id, Arg.Any<CancellationToken>()).Returns(company);
        _storage.TaxIdExistsAsync("500000002", company.Id, Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService();

        var act = () => service.UpdateAsync(
            company.Id,
            new UpdateCompanyRequest("Alfa", "500000002", null, null, null, true));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already uses*");
        company.TaxId.Should().Be("500000001");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_company()
    {
        var service = CreateService();

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateCompanyRequest("Alfa", "500000001", null, null, null, true));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_company()
    {
        var result = await CreateService().GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
