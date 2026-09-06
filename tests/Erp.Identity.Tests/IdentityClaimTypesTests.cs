using Erp.Identity.Common.Constants;
using Erp.Identity.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Identity.Tests;

/// <summary>
/// The role a user has only reaches an API if ASP.NET Identity names the claim the same way
/// Duende and the APIs expect. This builds the host registration and checks that chain, which
/// is otherwise only visible by decoding a live token.
/// </summary>
public class IdentityClaimTypesTests
{
    private static IdentityOptions BuildIdentityOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = "Server=(localdb)\\MSSQLLocalDB;Database=ErpTests;Integrated Security=true;"
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AddIdentityStorage(configuration)
            .BuildServiceProvider();

        return provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
    }

    [Fact]
    public void Roles_are_named_role_so_the_apis_can_authorize_by_role()
    {
        BuildIdentityOptions().ClaimsIdentity.RoleClaimType.Should().Be(Constants.Claims.Role);
    }

    [Fact]
    public void The_user_identifier_is_named_sub()
    {
        BuildIdentityOptions().ClaimsIdentity.UserIdClaimType.Should().Be(Constants.Claims.Subject);
    }

    [Fact]
    public void The_user_name_is_named_name()
    {
        BuildIdentityOptions().ClaimsIdentity.UserNameClaimType.Should().Be(Constants.Claims.Name);
    }
}
