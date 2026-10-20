/**
 * MyAIAgent | Gateway · Infrastructure
 *
 * Parses Orchestrator SSE responses and reports malformed, failed, or unterminated streams safely.
 */

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Infrastructure.Services;

public sealed class OrchestratorStreamClient(
    HttpClient httpClient,
    ILogger<OrchestratorStreamClient> logger) : IOrchestratorStreamClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(
        string? sessionId,
        string userMessage,
        string userId,
        string? agentId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await TrySendAsync(sessionId, userMessage, userId, agentId, cancellationToken);
        if (response is null)
        {
            yield return ChatStreamEvent.Error(GatewayConstant.Errors.ChatFailed);
            yield break;
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parser = SseParser.Create(
            body,
            (_, data) => JsonSerializer.Deserialize<ChatStreamEvent>(data, JsonOptions));
        await using var enumerator = parser.EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        // yield is not allowed inside a catch clause, so a broken stream is recorded and reported afterwards.
        var streamBroke = false;
        var sawTerminalEvent = false;
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
            {
                logger.LogError(ex, "Orchestrator chat stream was interrupted");
                streamBroke = true;
                break;
            }

            if (!hasNext)
                break;

            if (enumerator.Current.Data is { } chatEvent)
            {
                sawTerminalEvent |= chatEvent.IsTerminal;
                yield return chatEvent;
            }
        }

        // A stream that ends cleanly but never said done or error was cut short; the client must not mistake it for success.
        if (streamBroke || !sawTerminalEvent)
        {
            if (!streamBroke)
                logger.LogError("Orchestrator chat stream ended without a terminal event");

            yield return ChatStreamEvent.Error(GatewayConstant.Errors.ChatFailed);
        }
    }

    private async Task<HttpResponseMessage?> TrySendAsync(
        string? sessionId,
        string userMessage,
        string userId,
        string? agentId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
        {
            Content = JsonContent.Create(new { sessionId, userMessage, userId, agentId }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        try
        {
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
                return response;

            logger.LogError("Orchestrator chat stream returned status {StatusCode}", (int)response.StatusCode);
            response.Dispose();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator chat stream request failed");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation the caller did not request.
            logger.LogError(ex, "Orchestrator chat stream request timed out");
        }

        return null;
    }
}
