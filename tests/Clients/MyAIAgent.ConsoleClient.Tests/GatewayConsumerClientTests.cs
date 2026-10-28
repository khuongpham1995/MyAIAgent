/**
 * MyAIAgent | Gateway SDK · Tests
 *
 * Exercises generated public, upload, and streaming methods as a consumer independent of console policies.
 */

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Gateway.Client;

namespace MyAIAgent.ConsoleClient.Tests;

public sealed class GatewayConsumerClientTests
{
    [Fact]
    public async Task ChatAsync_WhenValidationFails_ShouldExposeTypedErrorEnvelopeToConsumers()
    {
        // Arrange
        await using var provider = ConsumerProvider(_ => GatewayFixture.Json(
            """{"isSuccess":false,"data":{"statusCode":400},"message":"Validation failed","errors":{"UserMessage":"A message is required."}}""",
            HttpStatusCode.BadRequest));
        var api = provider.GetRequiredService<GatewayApiClient>();

        // Act
        var act = () => api.ChatAsync(new ChatGatewayRequestDto { UserMessage = "Hello" }, CancellationToken.None);

        // Assert
        var failure = await act.Should().ThrowAsync<ApiException<ResponseModelOfErrorResponseDto>>();
        failure.Which.StatusCode.Should().Be(400);
        failure.Which.Result.Message.Should().Be("Validation failed");
        failure.Which.Result.Data!.StatusCode.Should().Be(400);
        failure.Which.Result.Errors.Should().ContainKey("UserMessage").WhoseValue.Should().Be("A message is required.");
    }

    [Fact]
    public async Task PublicListConversationsAsync_WhenUsedByAnotherConsumer_ShouldUseItsHostAndAuthorization()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        await using var provider = ConsumerProvider(request =>
        {
            captured = request;
            return GatewayFixture.Json("""{"isSuccess":true,"data":[]}""");
        });
        var api = provider.GetRequiredService<GatewayApiClient>();

        // Act
        var result = await api.PublicListConversationsAsync(1, 20, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
        captured!.RequestUri!.Host.Should().Be("gateway.example.test");
        captured.RequestUri.PathAndQuery.Should().Be("/api/public/conversations?page=1&pageSize=20");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenConsumerSendsFile_ShouldGenerateMultipartRequestAndTypedResponse()
    {
        // Arrange
        string? mediaType = null;
        string? path = null;
        await using var provider = ConsumerProvider(request =>
        {
            mediaType = request.Content!.Headers.ContentType!.MediaType;
            path = request.RequestUri!.PathAndQuery;
            return GatewayFixture.Json("""{"isSuccess":true,"data":{"documentId":"doc-1","status":"accepted"}}""");
        });
        using var file = new MemoryStream("test document"u8.ToArray());

        // Act
        var result = await provider.GetRequiredService<GatewayApiClient>()
            .UploadDocumentAsync("doc-1", new FileParameter(file, "test.txt", "text/plain"), CancellationToken.None);

        // Assert
        mediaType.Should().Be("multipart/form-data");
        path.Should().Be("/api/admin/documents?documentId=doc-1");
        result.Data.Should().NotBeNull();
        result.Data!.DocumentId.Should().Be("doc-1");
    }

    [Fact]
    public async Task PublicStreamChatAsync_WhenConsumerRequestsStream_ShouldReturnUnbufferedDisposableResponse()
    {
        // Arrange
        var body = new FragmentedReadStream(System.Text.Encoding.UTF8.GetBytes(GatewayClientTests.Session + GatewayClientTests.Delta + GatewayClientTests.Done));
        await using var provider = ConsumerProvider(_ => GatewayFixture.Stream(body));

        // Act
        var response = await provider.GetRequiredService<GatewayApiClient>()
            .PublicStreamChatAsync(new PublicChatRequestDto { UserMessage = "Hello" }, CancellationToken.None);
        var positionBeforeReading = body.Position;
        using var reader = new StreamReader(response.Stream);
        var text = await reader.ReadToEndAsync();
        response.Dispose();

        // Assert
        positionBeforeReading.Should().Be(0);
        text.Should().Contain("event: session").And.Contain("event: done");
        body.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAgentAsync_WhenConsumerHasAdminAccess_ShouldExposeOperationBeyondConsoleMenus()
    {
        // Arrange
        string? path = null;
        await using var provider = ConsumerProvider(request =>
        {
            path = request.RequestUri!.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        // Act
        await provider.GetRequiredService<GatewayApiClient>().DeleteAgentAsync("a-1", CancellationToken.None);

        // Assert
        path.Should().Be("/api/admin/agents/a-1");
    }

    private static ServiceProvider ConsumerProvider(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var services = new ServiceCollection();
        services.AddHttpClient<GatewayApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://gateway.example.test/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-only-token");
        }).ConfigurePrimaryHttpMessageHandler(() => new FixtureHandler((request, _) => Task.FromResult(respond(request)), []));
        return services.BuildServiceProvider();
    }
}
