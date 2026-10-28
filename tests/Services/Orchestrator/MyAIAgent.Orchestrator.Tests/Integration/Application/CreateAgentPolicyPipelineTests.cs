/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Exercises configured MCP endpoint validation through MediatR before agent repository writes.
 */

using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Services;

namespace MyAIAgent.Orchestrator.Tests.Integration.Application;

public sealed class CreateAgentPolicyPipelineTests
{
    [Theory]
    [InlineData("https://unapproved.example/mcp", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    [InlineData("http://127.0.0.1:9000/mcp", false)]
    [InlineData("https://trusted.internal/mcp?session=1", false)]
    [InlineData("https://trusted.internal/mcp", true)]
    [InlineData("https://approved.example:8443/sse", true)]
    [InlineData("", true)]
    public async Task Send_WhenAgentEndpointIsChecked_ShouldEnforcePolicyBeforePersistence(string endpoint, bool allowed)
    {
        // Arrange
        var settings = McpServiceClientSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpServer:BaseUrl"] = "https://trusted.internal",
            ["McpServer:AccessKey"] = "test-key",
            ["McpServer:AllowedExternalOrigins:0"] = "https://approved.example:8443"
        }).Build(), false);
        var repository = new Mock<IAgentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var services = new ServiceCollection();
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<CreateAgentCommand>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<CreateAgentCommandValidator>();
        services.AddSingleton<IMcpEndpointPolicy>(new McpEndpointPolicy(settings));
        services.AddSingleton(repository.Object);
        services.AddSingleton(unitOfWork.Object);
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var action = () => sender.Send(new CreateAgentCommand("Agent", "Instructions", endpoint, false));

        // Assert
        if (allowed) (await action()).IsSuccess.Should().BeTrue();
        else await action.Should().ThrowAsync<ValidationException>();
        repository.Verify(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()), allowed ? Times.Once() : Times.Never());
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), allowed ? Times.Once() : Times.Never());
    }
}
