using FluentAssertions;
using Moq;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Admin.Commands.UploadDocument;
using MyAIAgent.MainSubSys.Application.Admin.Queries.ListMcpTools;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Tests.Unit.Application.Admin.Queries.ListMcpTools;

public sealed class ListMcpToolsHandlerTests
{
    [Fact]
    public async Task Handle_WhenToolsAreAvailable_ShouldReturnSuccessWithTools()
    {
        // Arrange
        IReadOnlyList<ToolInfo> tools = [new ToolInfo("web_search", "Search the web"), new ToolInfo("get_date", "Get current date")];
        var mockClient = new Mock<IMcpServerClient>();
        mockClient
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<ToolInfo>>.Succeed(tools));

        var handler = new ListMcpToolsHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new ListMcpToolsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.Should().Contain(t => t.Name == "web_search");
        result.Data.Should().Contain(t => t.Name == "get_date");
    }

    [Fact]
    public async Task Handle_WhenNoToolsAvailable_ShouldReturnSuccessWithEmptyList()
    {
        // Arrange
        var mockClient = new Mock<IMcpServerClient>();
        mockClient
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<ToolInfo>>.Succeed([]));

        var handler = new ListMcpToolsHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new ListMcpToolsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMcpServerReturnsFail_ShouldPropagateFailure()
    {
        // Arrange
        var mockClient = new Mock<IMcpServerClient>();
        mockClient
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<ToolInfo>>.Fail(message: "Tool listing failed."));

        var handler = new ListMcpToolsHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new ListMcpToolsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Tool listing failed.");
    }
}
