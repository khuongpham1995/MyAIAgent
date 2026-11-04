/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Verifies the Document Intelligence request contract and page text mapping.
 */

using System.Net;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core.Pipeline;
using FluentAssertions;
using MyAIAgent.KernelMemory.Infrastructure.Services;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class DocumentIntelligenceOcrClientTests
{
    [Fact]
    public async Task AnalyzeAsync_WhenOfficePagesHaveOnlySpans_ShouldExtractUtf16TextPerPage()
    {
        // Arrange
        using var handler = new CompletedAnalysisHandler(
            "A😀B Second",
            "[{\"pageNumber\":1,\"spans\":[{\"offset\":0,\"length\":1},{\"offset\":1,\"length\":3}]}," +
            "{\"pageNumber\":2,\"spans\":[{\"offset\":5,\"length\":6}]}]");
        var client = CreateClient(handler);

        // Act
        var pages = await client.AnalyzeAsync(BinaryData.FromBytes([1, 2, 3]), CancellationToken.None);

        // Assert
        pages.Select(x => (x.PageNumber, x.Text)).Should().Equal((1, "A😀B"), (2, "Second"));
        handler.AnalyzeUri!.Query.Should().Contain("stringIndexType=utf16CodeUnit");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenOfficeSpanIsOutOfRange_ShouldRejectTheResult()
    {
        // Arrange
        using var handler = new CompletedAnalysisHandler(
            "Short", "[{\"pageNumber\":1,\"spans\":[{\"offset\":20,\"length\":4}]}]");
        var client = CreateClient(handler);

        // Act
        var action = () => client.AnalyzeAsync(BinaryData.FromBytes([1]), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenSent_ShouldUseV4PrebuiltRead()
    {
        // Arrange
        using var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var options = new DocumentIntelligenceClientOptions(
            DocumentIntelligenceClientOptions.ServiceVersion.V2024_11_30)
        {
            Transport = new HttpClientTransport(httpClient)
        };
        var sdkClient = new DocumentIntelligenceClient(
            new Uri("https://example.cognitiveservices.azure.com"),
            new AzureKeyCredential("test-key"), options);
        var client = new DocumentIntelligenceOcrClient(sdkClient);

        // Act
        var action = () => client.AnalyzeAsync(BinaryData.FromBytes([1, 2, 3]), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<RequestFailedException>();
        handler.RequestUri.Should().NotBeNull();
        handler.RequestUri!.AbsolutePath.Should().Contain("/documentModels/prebuilt-read:analyze");
        handler.RequestUri.Query.Should().Contain("api-version=2024-11-30");
        handler.Method.Should().Be(HttpMethod.Post);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"code\":\"InvalidRequest\",\"message\":\"test rejection\"}}")
            });
        }
    }

    private static DocumentIntelligenceOcrClient CreateClient(HttpMessageHandler handler)
    {
        var options = new DocumentIntelligenceClientOptions(
            DocumentIntelligenceClientOptions.ServiceVersion.V2024_11_30)
        {
            Transport = new HttpClientTransport(new HttpClient(handler))
        };
        return new DocumentIntelligenceOcrClient(new DocumentIntelligenceClient(
            new Uri("https://example.cognitiveservices.azure.com"),
            new AzureKeyCredential("test-key"), options));
    }

    private sealed class CompletedAnalysisHandler(string content, string pagesJson) : HttpMessageHandler
    {
        public Uri? AnalyzeUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                AnalyzeUri = request.RequestUri;
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Add("Operation-Location",
                    "https://example.cognitiveservices.azure.com/documentintelligence/documentModels/prebuilt-read/analyzeResults/test-operation?api-version=2024-11-30");
                accepted.Headers.Add("Retry-After", "0");
                return Task.FromResult(accepted);
            }

            var escapedContent = System.Text.Json.JsonSerializer.Serialize(content);
            var response = "{\"status\":\"succeeded\",\"createdDateTime\":\"2024-11-30T00:00:00Z\"," +
                "\"lastUpdatedDateTime\":\"2024-11-30T00:00:01Z\",\"analyzeResult\":{" +
                "\"apiVersion\":\"2024-11-30\",\"modelId\":\"prebuilt-read\",\"content\":" +
                escapedContent + ",\"pages\":" + pagesJson + "}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            });
        }
    }
}
