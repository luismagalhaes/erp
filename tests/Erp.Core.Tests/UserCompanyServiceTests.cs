using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class UserCompanyServiceTests
{
    private readonly IUserCompanyStorage _storage = Substitute.For<IUserCompanyStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private UserCompanyService CreateService() => new(_storage);

    private UserCompany Membership(string role) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        CompanyId = _companyId,
        Role = role,
        Company = new Company { Id = _companyId, Name = "Alfa", TaxId = "500000001" }
    };

    [Fact]
    public async Task GetUserCompaniesAsync_maps_the_active_memberships()
    {
        _storage.GetActiveByUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([Membership("Admin")]);

        var result = await CreateService().GetUserCompaniesAsync("user-1");

        result.Should().ContainSingle();
        result[0].CompanyName.Should().Be("Alfa");
        result[0].Role.Should().Be("Admin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserCompaniesAsync_returns_empty_without_a_user(string userId)
    {
        var result = await CreateService().GetUserCompaniesAsync(userId);

        result.Should().BeEmpty();
        await _storage.DidNotReceive().GetActiveByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserRoleAsync_returns_null_for_an_empty_company()
    {
        var result = await CreateService().GetUserRoleAsync("user-1", Guid.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public async Task HasRoleAsync_ignores_case()
    {
        _storage.GetActiveAsync("user-1", _companyId, Arg.Any<CancellationToken>())
            .Returns(Membership("Admin"));

        var result = await CreateService().HasRoleAsync("user-1", _companyId, "admin");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRoleAsync_is_false_for_a_different_role()
    {
        _storage.GetActiveAsync("user-1", _companyId, Arg.Any<CancellationToken>())
            .Returns(Membership("User"));

        var result = await CreateService().HasRoleAsync("user-1", _companyId, "Admin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasRoleAsync_is_false_when_no_role_is_asked_for()
    {
        var result = await CreateService().HasRoleAsync("user-1", _companyId, "  ");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessCompanyAsync_is_false_when_there_is_no_active_membership()
    {
        _storage.GetActiveAsync("user-1", _companyId, Arg.Any<CancellationToken>())
            .Returns((UserCompany?)null);

        var result = await CreateService().CanAccessCompanyAsync("user-1", _companyId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessCompanyAsync_is_true_for_an_active_membership()
    {
        _storage.GetActiveAsync("user-1", _companyId, Arg.Any<CancellationToken>())
            .Returns(Membership("User"));

        var result = await CreateService().CanAccessCompanyAsync("user-1", _companyId);

        result.Should().BeTrue();
    }
}
