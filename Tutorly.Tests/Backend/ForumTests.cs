using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Tutorly.Server.Controllers;   // ForumController
using Tutorly.Server.Services;      // IForumService
using Tutorly.Shared;               // DTOs
using Xunit;

namespace Tutorly.Tests.Backend;

public sealed class ForumTests
{
    // Build controller with a fake authenticated user (so [Authorize] passes)
    private static ForumController BuildController(Mock<IForumService> forumMock, string? userId = "test-user-id")
    {
        var logger = new Mock<ILogger<ForumController>>();
        var controller = new ForumController(forumMock.Object, logger.Object);

        var http = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "student@belgiumcampus.ac.za")
            }, authenticationType: "Test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    // ---------- Communities ----------

    [Fact]
    public async Task CreateCommunity_WithValidData_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.CreateCommunityAsync(It.IsAny<CreateCommunityDto>(), "test-user-id"))
             .ReturnsAsync(new ApiResponse<CommunityDto> { Success = true, Data = new CommunityDto { CommunityId = 1, CommunityName = "CS101" } });

        var sut = BuildController(forum);

        var result = await sut.CreateCommunity(new CreateCommunityDto
        {
            CommunityName = "CS101",
            CommunityDescription = "Intro",
            CommunityType = "course"
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        forum.Verify(s => s.CreateCommunityAsync(It.IsAny<CreateCommunityDto>(), "test-user-id"), Times.Once);
    }

    [Fact]
    public async Task CreateCommunity_Unauthenticated_ReturnsUnauthorized()
    {
        var forum = new Mock<IForumService>();
        var sut = BuildController(forum, userId: null);

        var result = await sut.CreateCommunity(new CreateCommunityDto { CommunityName = "X", CommunityType = "course" });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetCommunities_ReturnsOk_WhenServiceSucceeds()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.GetCommunitiesAsync(It.IsAny<CommunityFilterDto>()))
             .ReturnsAsync(new ApiResponse<List<CommunityDto>>
             {
                 Success = true,
                 Data = new List<CommunityDto> { new() { CommunityId = 1, CommunityName = "CS101" } }
             });

        var sut = BuildController(forum);

        var result = await sut.GetCommunities(new CommunityFilterDto());

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    // ---------- Threads ----------

    [Fact]
    public async Task CreateThread_WithValidData_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.CreateThreadAsync(1, It.IsAny<CreateThreadDto>(), "test-user-id"))
             .ReturnsAsync(new ApiResponse<ThreadDto> { Success = true, Data = new ThreadDto() });

        var sut = BuildController(forum);

        var result = await sut.CreateThread(1, new CreateThreadDto()); // no assumptions on DTO properties
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetThreadsByCommunity_ServiceFails_ReturnsBadRequest()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.GetThreadsByCommunityAsync(99))
             .ReturnsAsync(new ApiResponse<List<ThreadDto>> { Success = false, Message = "boom" });

        var sut = BuildController(forum);

        var result = await sut.GetThreadsByCommunity(99);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- Posts ----------

    [Fact]
    public async Task CreatePost_WithValidData_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.CreatePostAsync(5, It.IsAny<CreatePostDto>(), "test-user-id"))
             .ReturnsAsync(new ApiResponse<PostDto> { Success = true, Data = new PostDto() });

        var sut = BuildController(forum);

        var result = await sut.CreatePost(5, new CreatePostDto { IsAnonymous = false, Tag = "study" });
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPostsByThread_ServiceFails_ReturnsBadRequest()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.GetPostsByThreadAsync(7, It.IsAny<PostFilterDto>()))
             .ReturnsAsync(new ApiResponse<List<PostDto>> { Success = false, Message = "fail" });

        var sut = BuildController(forum);

        var result = await sut.GetPostsByThread(7, new PostFilterDto());
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- Responses & Votes ----------

    [Fact]
    public async Task CreateResponse_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.CreateResponseAsync(99, It.IsAny<CreateResponseDto>(), "test-user-id"))
             .ReturnsAsync(new ApiResponse<ResponseDto> { Success = true, Data = new ResponseDto() });

        var sut = BuildController(forum);

        var result = await sut.CreateResponse(99, new CreateResponseDto());
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task VoteOnResponse_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.VoteOnResponseAsync(7, It.IsAny<CreateVoteDto>(), "test-user-id"))
             .ReturnsAsync(new ApiResponse<VoteDto> { Success = true, Data = new VoteDto() });

        var sut = BuildController(forum);

        var result = await sut.VoteOnResponse(7, new CreateVoteDto { VoteType = 1 });
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveVote_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.RemoveVoteAsync(7, "test-user-id"))
             .ReturnsAsync(new ApiResponse<bool> { Success = true, Data = true });

        var sut = BuildController(forum);

        var result = await sut.RemoveVote(7);
        result.Result.Should().BeOfType<OkObjectResult>();
    }



    // ---------- Metrics (AllowAnonymous) ----------

    [Fact]
    public async Task GetForumMetrics_ReturnsOk()
    {
        var forum = new Mock<IForumService>();
        forum.Setup(s => s.GetForumMetricsAsync())
             .ReturnsAsync(new ApiResponse<ForumMetricsDto> { Success = true, Data = new ForumMetricsDto() });

        // anonymous call: userId null proves it still returns 200
        var sut = BuildController(forum, userId: null);

        var result = await sut.GetForumMetrics();
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
