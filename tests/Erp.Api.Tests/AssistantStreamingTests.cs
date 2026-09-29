using System.Text;
using Erp.Api.Contracts;
using Erp.Api.Controllers.Assistant;
using Erp.Api.Services.Assistant;
using Erp.Common;
using Erp.Sales.Infrastructure.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Api.Tests;

/// <summary>
/// The answer as it reaches the chat: pieces in order, a closing event, and a history that never
/// grows past what the model is sent.
/// </summary>
public class AssistantStreamingTests
{
    private readonly IChatClient _chat = Substitute.For<IChatClient>();
    private readonly Guid _companyId = Guid.NewGuid();

    private AssistantService CreateService(bool configured = true) =>
        new(
            Options.Create(new AssistantOptions
            {
                Endpoint = configured ? "https://example.test/v1" : null,
                Model = configured ? "model" : null
            }),
            new AssistantTools(Substitute.For<ISalesAnalyticsService>()),
            NullLogger<AssistantService>.Instance,
            _chat);

    private AssistantController CreateController(AssistantService service, out MemoryStream body)
    {
        body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = body;

        return new AssistantController(service, NullLogger<AssistantController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Updates(params string[] pieces)
    {
        foreach (var piece in pieces)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, piece);
            await Task.Yield();
        }
    }

    private static AssistantChatRequest Ask(string text) =>
        new([new AssistantMessageDto(Constants.Assistant.MessageRoles.User, text)]);

    [Fact]
    public async Task The_answer_is_handed_over_piece_by_piece_without_the_blank_lines_it_opens_with()
    {
        _chat.GetStreamingResponseAsync(default!, default, default)
            .ReturnsForAnyArgs(Updates("\n\n", "  Ven", "das ", "subiram."));

        var pieces = await CreateService()
            .ChatStreamAsync(_companyId, [new AssistantMessage(Constants.Assistant.MessageRoles.User, "?")], CancellationToken.None)
            .ToListAsync();

        pieces.Should().Equal("Ven", "das ", "subiram.");
    }

    [Fact]
    public async Task The_stream_carries_each_piece_as_json_and_ends_with_done()
    {
        _chat.GetStreamingResponseAsync(default!, default, default)
            .ReturnsForAnyArgs(Updates("Olá ", "mundo"));
        var controller = CreateController(CreateService(), out var body);

        await controller.ChatStream(_companyId, Ask("oi"), CancellationToken.None);

        Encoding.UTF8.GetString(body.ToArray()).Should().Be(
            "data: \"Olá \"\n\ndata: \"mundo\"\n\nevent: done\ndata: {}\n\n");
        controller.Response.ContentType.Should().Be("text/event-stream");
    }

    [Fact]
    public async Task A_failure_after_the_answer_started_closes_the_stream_with_an_error_event()
    {
        _chat.GetStreamingResponseAsync(default!, default, default)
            .ReturnsForAnyArgs(Failing());
        var controller = CreateController(CreateService(), out var body);

        await controller.ChatStream(_companyId, Ask("oi"), CancellationToken.None);

        var text = Encoding.UTF8.GetString(body.ToArray());
        text.Should().StartWith("data: \"Olá\"\n\n");
        text.Should().EndWith("event: error\ndata: {}\n\n");

        static async IAsyncEnumerable<ChatResponseUpdate> Failing()
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Olá");
            await Task.Yield();
            throw new InvalidOperationException("provider down");
        }
    }

    [Fact]
    public async Task An_unconfigured_assistant_answers_503_before_opening_a_stream()
    {
        var controller = CreateController(CreateService(configured: false), out var body);

        var result = await controller.ChatStream(_companyId, Ask("oi"), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        body.Length.Should().Be(0);
    }

    [Fact]
    public async Task A_long_history_loses_its_oldest_turns_but_keeps_the_question_and_starts_with_the_user()
    {
        IList<ChatMessage>? sent = null;
        _chat.GetResponseAsync(default!, default, default).ReturnsForAnyArgs(callInfo =>
        {
            sent = callInfo.Arg<IEnumerable<ChatMessage>>().ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        });
        var controller = CreateController(CreateService(), out _);
        var chunk = new string('x', Constants.Assistant.MaxMessageLength);
        var roles = new[] { Constants.Assistant.MessageRoles.User, Constants.Assistant.MessageRoles.Assistant };
        var history = Enumerable.Range(0, 16)
            .Select(index => new AssistantMessageDto(roles[index % 2], chunk))
            .Append(new AssistantMessageDto(Constants.Assistant.MessageRoles.User, "a última"))
            .ToList();

        await controller.Chat(_companyId, new AssistantChatRequest(history), CancellationToken.None);

        var conversation = sent!.Where(message => message.Role != ChatRole.System).ToList();
        conversation.Sum(message => message.Text.Length).Should().BeLessThanOrEqualTo(Constants.Assistant.MaxHistoryCharacters);
        conversation[0].Role.Should().Be(ChatRole.User);
        conversation[^1].Text.Should().Be("a última");
    }
}
