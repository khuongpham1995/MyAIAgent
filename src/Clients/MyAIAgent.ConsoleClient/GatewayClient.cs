/**
 * MyAIAgent | Console Client
 *
 * Calls Gateway's local HTTP APIs without service keys, retries, or backend project references.
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MyAIAgent.Gateway.Client;

namespace MyAIAgent.ConsoleClient;

internal sealed class GatewayClient(HttpClient httpClient, ClientOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public Uri Address => options.Gateway;

    private readonly GatewayApiClient api = new(httpClient);

    public Task CheckConnectionAsync(CancellationToken cancellationToken) =>
        ExecuteApiAsync(api.CheckConnectionAsync, options.RequestTimeout, cancellationToken);

    public Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.ListAgentsAsync(token);
            return Data(result.IsSuccess, result.Data);
        }, options.RequestTimeout, cancellationToken);

    public Task<IReadOnlyList<ToolInfo>> ListToolsAsync(CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.ListToolsAsync(token);
            return Data(result.IsSuccess, result.Data);
        }, options.RequestTimeout, cancellationToken);

    public Task<string> CreateAgentAsync(CreateAgentRequestDto agent, CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.CreateAgentAsync(agent, token);
            return Data(result.IsSuccess, result.Data);
        }, options.RequestTimeout, cancellationToken);

    public Task<ChatGatewayResponse> ChatAsync(ChatGatewayRequestDto request, CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.ChatAsync(request, token);
            return Data(result.IsSuccess, result.Data);
        }, options.ChatTimeout, cancellationToken);

    public Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(int page, CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.ListConversationsAsync(page, 20, token);
            return Data(result.IsSuccess, result.Data);
        }, options.RequestTimeout, cancellationToken);

    public Task<ConversationDetail> GetConversationAsync(string id, CancellationToken cancellationToken) =>
        CallApiAsync(async token =>
        {
            var result = await api.GetConversationAsync(id, token);
            return Data(result.IsSuccess, result.Data);
        }, options.RequestTimeout, cancellationToken);

    public Task RenameConversationAsync(string id, string title, CancellationToken cancellationToken) =>
        ExecuteApiAsync(token => api.RenameConversationAsync(id, new RenameConversationRequestDto { Title = title }, token), options.RequestTimeout, cancellationToken);

    public Task DeleteConversationAsync(string id, CancellationToken cancellationToken) =>
        ExecuteApiAsync(token => api.DeleteConversationAsync(id, token), options.RequestTimeout, cancellationToken);

    public async IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(ChatGatewayRequestDto chat, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var deadline = Deadline(options.ChatTimeout, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat/stream");
        request.Content = JsonContent.Create(chat);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await SendStreamHeadersAsync(request, deadline.Token);
        EnsureStatus(response);
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
            throw new GatewayClientException("Gateway did not return an event stream.");

        await using var body = await response.Content.ReadAsStreamAsync(deadline.Token);
        await using var limited = new LimitedReadStream(body, 8 * 1024 * 1024);
        var parser = SseParser.Create(limited, ParseEvent);
        string? sessionId = null;

        await foreach (var item in parser.EnumerateAsync(deadline.Token))
        {
            var value = item.Data;
            if (value.Type == "session")
            {
                if (sessionId is not null || !IsSessionId(value.SessionId))
                    throw new GatewayClientException("Gateway returned an invalid session event.");
                sessionId = value.SessionId;
            }
            else if (value.Type != "error" && sessionId is null)
                throw new GatewayClientException("Gateway stream is missing its session event.");

            if (value.Type == "done" && value.SessionId != sessionId)
                throw new GatewayClientException("Gateway returned a mismatched completion event.");

            yield return value;
            if (value.Type is "done" or "error")
                yield break;
        }

        throw new GatewayClientException("Stream interrupted before completion. Inspect saved history before retrying.");
    }

    public static bool IsSessionId(string? value) => value is { Length: > 0 and <= 64 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static T Data<T>(bool success, T? data)
    {
        if (!success || data is null)
            throw new GatewayClientException("Gateway could not complete this operation. Check the service logs.");
        ValidateData(data);
        return data;
    }

    private async Task<HttpResponseMessage> SendStreamHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var headers = Deadline(options.RequestTimeout, cancellationToken);
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token);
    }

    private async Task<T> CallApiAsync<T>(Func<CancellationToken, Task<T>> call, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = Deadline(timeout, cancellationToken);
        try
        {
            return await call(deadline.Token);
        }
        catch (ApiException)
        {
            // Generated exceptions can contain response details; expose only our safe boundary message.
            throw new GatewayClientException("Gateway returned an invalid or unexpected response.");
        }
    }

    private Task ExecuteApiAsync(Func<CancellationToken, Task> call, TimeSpan timeout, CancellationToken cancellationToken) =>
        CallApiAsync(async token => { await call(token); return true; }, timeout, cancellationToken);

    private static ChatStreamEvent ParseEvent(string eventType, ReadOnlySpan<byte> data)
    {
        ChatStreamEvent? value;
        try
        {
            value = JsonSerializer.Deserialize<ChatStreamEvent>(data, JsonOptions);
        }
        catch (JsonException)
        {
            throw new GatewayClientException("Gateway returned a malformed stream event.");
        }
        if (value is null || value.Type != eventType || (value.Type switch
        {
            "session" or "done" => !IsSessionId(value.SessionId),
            "delta" => value.Text is null,
            "title" => value.Title is null,
            "error" => value.Message is null,
            _ => true,
        }))
            throw new GatewayClientException("Gateway returned an unsupported or invalid stream event.");
        return value;
    }

    private static void ValidateData<T>(T data)
    {
        // Generated non-nullable SDK declarations do not prevent null values in incoming JSON.
        var invalid = data switch
        {
            string id => !IsSessionId(id),
            ChatGatewayResponse reply => !IsSessionId(reply.SessionId) || string.IsNullOrWhiteSpace(reply.Reply),
            IReadOnlyList<AgentSummary> agents => agents.Any(agent => agent is null
                || !IsSessionId(agent.Id) || string.IsNullOrWhiteSpace(agent.Name) || agent.EnabledTools is null),
            IReadOnlyList<ToolInfo> tools => tools.Any(tool => tool is null || string.IsNullOrWhiteSpace(tool.Name)),
            IReadOnlyList<ConversationSummary> conversations => conversations.Any(item => item is null || !IsSessionId(item.SessionId)),
            ConversationDetail conversation => !IsSessionId(conversation.SessionId)
                || conversation.Messages is null || conversation.Messages.Any(message => message is null),
            _ => false,
        };
        if (invalid) throw new GatewayClientException("Gateway returned incomplete or invalid data.");
    }

    private static CancellationTokenSource Deadline(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        return deadline;
    }

    internal static void EnsureStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Request validation failed. Check your input.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Gateway denied access. This client requires local Development routes.",
            HttpStatusCode.NotFound => "The requested resource was not found. Refresh the list.",
            HttpStatusCode.TooManyRequests => "Request limit reached. Wait before trying again.",
            _ => "Gateway request failed. Check the service logs.",
        };
        throw new GatewayClientException(message);
    }
}
