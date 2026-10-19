using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class AzureOpenAICompatibilityTests
{
    [Fact]
    public async Task GenerateTextAsync_WhenDependenciesArePatched_ShouldStreamThroughKernelMemoryConnector()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth"] = "ApiKey",
                ["Endpoint"] = "https://example.openai.azure.com",
                ["Deployment"] = "test-model",
                ["APIKey"] = "test-key",
                ["APIType"] = "ChatCompletion"
            })
            .Build().Get<AzureOpenAIConfig>()!;
        using var handler = new CompletionHandler();
        using var client = new HttpClient(handler);
        var builder = new KernelMemoryBuilder().WithAzureOpenAITextGeneration(config, httpClient: client);
        using var services = builder.Services.BuildServiceProvider();
        var generator = services.GetRequiredService<ITextGenerator>();
        var text = new StringBuilder();

        await foreach (var chunk in generator.GenerateTextAsync("Test prompt", new TextGenerationOptions(), CancellationToken.None))
            text.Append(chunk.Text);

        text.ToString().Should().Be("KernelMemory reply");
        handler.RequestPath.Should().Contain("/deployments/test-model/chat/completions");
    }

    private sealed class CompletionHandler : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"KernelMemory reply\"},\"finish_reason\":null}]}\n\n" +
                    "data: {\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                    "data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
            });
        }
    }
}
