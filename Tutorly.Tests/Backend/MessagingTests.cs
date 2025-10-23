using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Tutorly.Server.Controllers;   // MessagingController
using Tutorly.Server.Hubs;          // MessagingHub
using Tutorly.Server.Services;      // IMessagingService
using Tutorly.Shared;               // DTOs
using Xunit;

namespace Tutorly.Tests.Backend;

public sealed class MessagingTests
{
    // ---------- helpers ----------
    private static (MessagingController sut, Mock<IMessagingService> svc, Mock<IClientProxy> proxy) BuildController(string? userId = "test-user-id")
    {
        var svc = new Mock<IMessagingService>(MockBehavior.Strict);

        // Mock SignalR hub so controller can broadcast without real hub
        var hub = new Mock<IHubContext<MessagingHub>>(MockBehavior.Strict);
        var hubClients = new Mock<IHubClients>(MockBehavior.Strict);
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Loose);

        hub.SetupGet(h => h.Clients).Returns(hubClients.Object);
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);
        // SendAsync is an extension that calls SendCoreAsync under the hood
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<MessagingController>>();
        var sut = new MessagingController(svc.Object, logger.Object, hub.Object);

        var http = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            http.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Name, "Test User")
                    },
                    "TestAuth"));
        }
        sut.ControllerContext = new ControllerContext { HttpContext = http };

        return (sut, svc, clientProxy);
    }

    // ---------- Conversations ----------

    [Fact]
    public async Task GetConversations_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetConversationsAsync(It.IsAny<ConversationSearchDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<List<ConversationDto>> { Success = true, Data = new() });

        var result = await sut.GetConversations(new ConversationSearchDto());
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateDirectConversation_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.CreateDirectConversationAsync("other", "test-user-id", null))
           .ReturnsAsync(new ApiResponse<ConversationDto> { Success = true, Data = new ConversationDto() });

        var dto = new CreateConversationDto { OtherUserId = "other" };
        var result = await sut.CreateDirectConversation(dto);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateDirectConversation_MissingOtherUser_BadRequest()
    {
        var (sut, _, _) = BuildController();

        var dto = new CreateConversationDto { OtherUserId = null };
        var result = await sut.CreateDirectConversation(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateGroupConversation_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.CreateGroupConversationAsync(It.IsAny<CreateConversationDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<ConversationDto> { Success = true, Data = new ConversationDto() });

        var result = await sut.CreateGroupConversation(new CreateConversationDto());
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConversation_NotFound()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetConversationByIdAsync(99, "test-user-id"))
           .ReturnsAsync(new ApiResponse<ConversationDto> { Success = false, Message = "not found" });

        var result = await sut.GetConversation(99);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ---------- Messages ----------

    [Fact]
    public async Task GetMessages_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetMessagesAsync(1, It.IsAny<MessageFilterDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<List<MessageDto>> { Success = true, Data = new() });

        var result = await sut.GetMessages(1, new MessageFilterDto());
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SendMessage_Success_Ok_and_Broadcast()
    {
        var (sut, svc, proxy) = BuildController();

        var returned = new MessageDto { MessageId = 123, Content = "hi" };
        svc.Setup(s => s.SendMessageAsync(1, It.IsAny<SendMessageDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<MessageDto> { Success = true, Data = returned });

        var result = await sut.SendMessage(1, new SendMessageDto { Content = "hi" });

        result.Result.Should().BeOfType<OkObjectResult>();

        // verify the SignalR extension ultimately called SendCoreAsync
        proxy.Verify(p => p.SendCoreAsync(
            It.Is<string>(m => m == "ReceiveMessage"),
            It.IsAny<object?[]>(),
            default), Times.Once);
    }

    [Fact]
    public async Task GetMessage_NotFound()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetMessageByIdAsync(42, "test-user-id"))
           .ReturnsAsync(new ApiResponse<MessageDto> { Success = false, Message = "missing" });

        var result = await sut.GetMessage(42);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task EditMessage_Fail_BadRequest()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.EditMessageAsync(5, It.IsAny<EditMessageDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<MessageDto> { Success = false, Message = "bad" });

        var result = await sut.EditMessage(5, new EditMessageDto());
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }


    [Fact]
    public async Task MarkMessagesAsRead_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.MarkMessagesAsReadAsync(1, It.IsAny<MarkMessagesAsReadDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<bool> { Success = true, Data = true });

        var result = await sut.MarkMessagesAsRead(1, new MarkMessagesAsReadDto());
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SearchMessages_EmptyQuery_BadRequest()
    {
        var (sut, _, _) = BuildController();
        var result = await sut.SearchMessages(1, "");
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- User search & presence / counts ----------

    [Fact]
    public async Task SearchUsers_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.SearchUsersAsync(It.IsAny<UserSearchDto>(), "test-user-id"))
           .ReturnsAsync(new ApiResponse<List<UserSearchResultDto>> { Success = true, Data = new() });

        var result = await sut.SearchUsers(new UserSearchDto { SearchQuery = "al" });
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetTotalUnreadCount_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetTotalUnreadCountAsync("test-user-id"))
           .ReturnsAsync(new ApiResponse<int> { Success = true, Data = 3 });

        var result = await sut.GetTotalUnreadCount();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConversationUnreadCount_Success_Ok()
    {
        var (sut, svc, _) = BuildController();

        svc.Setup(s => s.GetConversationUnreadCountAsync(5, "test-user-id"))
           .ReturnsAsync(new ApiResponse<int> { Success = true, Data = 1 });

        var result = await sut.GetConversationUnreadCount(5);
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
