using Erp.Identity.Application.Handlers;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Identity.Tests;

/// <summary>
/// The Identity application services are thin: they forward to storage without reshaping the
/// data. These tests hold that contract, so a future change that swallows a result or drops an
/// argument does not pass unnoticed.
/// </summary>
public class ApplicationServiceTests
{
    [Fact]
    public async Task UserService_returns_the_users_from_storage()
    {
        var storage = Substitute.For<IUserStorage>();
        IReadOnlyList<UserListItem> users =
            [new("user-1", "a@b.pt", "Ana Alves", true, DateTime.UtcNow, ["Admin"])];
        storage.GetUsersAsync(Arg.Any<CancellationToken>()).Returns(users);

        var result = await new UserService(storage).GetUsersAsync();

        result.Should().BeSameAs(users);
    }

    [Fact]
    public async Task UserService_forwards_the_roles_to_update()
    {
        var storage = Substitute.For<IUserStorage>();
        string[] roles = ["Admin", "Auditor"];

        await new UserService(storage).UpdateUserRolesAsync("user-1", roles);

        await storage.Received(1).UpdateUserRolesAsync("user-1", roles, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApiScopeService_forwards_the_upsert_request_unchanged()
    {
        var storage = Substitute.For<IApiScopeStorage>();
        var request = new ApiScopeUpsertRequest(
            "erp.sales.read", "Sales - Read", "Read access to sales", true, false, false, true, []);

        await new ApiScopeService(storage).CreateApiScopeAsync(request);

        await storage.Received(1).CreateApiScopeAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApiScopeService_returns_null_when_the_scope_does_not_exist()
    {
        var storage = Substitute.For<IApiScopeStorage>();
        storage.GetApiScopeAsync("missing", Arg.Any<CancellationToken>()).Returns((ApiScopeEditItem?)null);

        var result = await new ApiScopeService(storage).GetApiScopeAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmailService_queues_the_message_on_the_notification_client()
    {
        var client = Substitute.For<INotificationEmailClient>();

        await new EmailService(client).SendAsync("a@b.pt", "Assunto", "<p>Corpo</p>");

        await client.Received(1).QueueAsync("a@b.pt", "Assunto", "<p>Corpo</p>", Arg.Any<CancellationToken>());
    }
}
