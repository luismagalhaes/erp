using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class UserCompanyAdminServiceTests
{
    private readonly IUserCompanyStorage _storage = Substitute.For<IUserCompanyStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private UserCompanyAdminService CreateService() => new(_storage);

    private UserCompany Membership(Guid id) => new()
    {
        Id = id,
        UserId = "user-1",
        CompanyId = _companyId,
        Role = "Admin",
        Company = new Company { Id = _companyId, Name = "Alfa", TaxId = "500000001" }
    };

    [Fact]
    public async Task CreateAsync_persists_the_membership()
    {
        _storage.CompanyExistsAsync(_companyId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.ExistsAsync("user-1", _companyId, Arg.Any<CancellationToken>()).Returns(false);
        _storage.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Membership(call.Arg<Guid>()));

        var created = await CreateService().CreateAsync(
            new CreateUserCompanyRequest("user-1", _companyId, "Admin"));

        created.UserId.Should().Be("user-1");
        created.CompanyName.Should().Be("Alfa");
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_company()
    {
        _storage.CompanyExistsAsync(_companyId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => CreateService().CreateAsync(
            new CreateUserCompanyRequest("user-1", _companyId, "Admin"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Company not found.");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_membership()
    {
        _storage.CompanyExistsAsync(_companyId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.ExistsAsync("user-1", _companyId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateService().CreateAsync(
            new CreateUserCompanyRequest("user-1", _companyId, "Admin"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_requires_a_company()
    {
        var act = () => CreateService().CreateAsync(
            new CreateUserCompanyRequest("user-1", Guid.Empty, "Admin"));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_membership()
    {
        var result = await CreateService().UpdateAsync(
            Guid.NewGuid(), new UpdateUserCompanyRequest("Admin", true));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_removes_an_existing_membership()
    {
        var id = Guid.NewGuid();
        _storage.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Membership(id));

        var deleted = await CreateService().DeleteAsync(id);

        deleted.Should().BeTrue();
        _storage.Received(1).Remove(Arg.Any<UserCompany>());
    }

    [Fact]
    public async Task DeleteAsync_is_false_for_an_unknown_membership()
    {
        var deleted = await CreateService().DeleteAsync(Guid.NewGuid());

        deleted.Should().BeFalse();
        _storage.DidNotReceive().Remove(Arg.Any<UserCompany>());
    }
}
