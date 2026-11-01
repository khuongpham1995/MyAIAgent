/**
 * MyAIAgent | Console Client
 *
 * Drives local chat, agent selection, and conversation management through Gateway's HTTP boundary.
 */

using System.Text.Json;
using MyAIAgent.Gateway.Client;

namespace MyAIAgent.ConsoleClient;

internal sealed class ConsoleApplication(GatewayClient gateway, TextReader input, TextWriter output, ClientOptions options) : IDisposable
{
    private readonly CancellationTokenSource exit = new();
    private readonly object cancellationLock = new();
    private readonly TerminalTextFilter textFilter = new();
    private CancellationTokenSource? activeOperation;
    private Task<string?>? pendingInput;
    private string? sessionId;
    private string? agentId;
    private string agentName = "Server default (no explicit selection)";
    private bool streaming = true;

    public void Cancel()
    {
        lock (cancellationLock)
        {
            if (activeOperation is not null) activeOperation.Cancel();
            else exit.Cancel();
        }
    }

    public async Task RunAsync()
    {
        Line("Development routes share one local identity. Chat/research can incur provider usage.");
        try
        {
            while (!exit.IsCancellationRequested)
            {
                ShowMenu();
                var choice = await ReadAsync("  Select [0-7] > ", exit.Token);
                switch (choice)
                {
                    case null or "0": return;
                    case "1": NewConversation(); await ChatAsync(); break;
                    case "2":
                        var opened = false;
                        await ExecuteAsync(async token => opened = await ConversationsAsync(token));
                        if (opened) await ChatAsync();
                        break;
                    case "3": await ExecuteAsync(ChooseAgentAsync); break;
                    case "4": await ExecuteAsync(ShowToolsAsync); break;
                    case "5":
                        await ExecuteAsync(async token =>
                        {
                            await gateway.CheckConnectionAsync(token);
                            Line("Gateway is live. This does not verify provider credentials or quota.");
                        });
                        break;
                    case "6": await ChatAsync(); break;
                    case "7": streaming = !streaming; break;
                    default: Line("Choose a number from 0 to 7."); break;
                }
            }
        }
        catch (OperationCanceledException) when (exit.IsCancellationRequested)
        {
            Line("Goodbye.");
        }
    }

    private void ShowMenu()
    {
        ShowHeading("MyAIAgent / Local console");
        ShowStatus("Gateway", gateway.Address.ToString());
        ShowStatus("Agent", agentName);
        ShowStatus("Session", sessionId ?? "New conversation");
        ShowStatus("Mode", streaming ? "streaming" : "JSON");

        Line("");
        Line("  CHAT & CONVERSATIONS");
        ShowOption("1", "New chat", "Start a fresh conversation");
        ShowOption("6", "Continue chat", sessionId is null ? "Start with the selected agent" : "Resume the current session");
        ShowOption("2", "Conversations", "Browse and manage saved history");
        ShowOption("3", "Select/create agent", "Choose who answers your messages");

        Line("");
        Line("  TOOLS & SETTINGS");
        ShowOption("4", "Tools", "Discover available MCP tools");
        ShowOption("5", "Connection check", "Check Gateway availability");
        ShowOption("7", "Reply mode", $"Switch to {(streaming ? "JSON" : "streaming")}");
        Line("");
        ShowOption("0", "Exit", "Close the console");
        Line("");
    }

    private void ShowHeading(string title)
    {
        Line("");
        Line($"  {title}");
        Line("  ────────────────────────────────────────────────────────");
    }

    private void ShowStatus(string label, string value) =>
        Line($"  {label}: {value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')}");

    private void ShowOption(string key, string label, string description) =>
        Line($"    [{key}] {label,-21} {description}");

    private async Task ChatAsync()
    {
        ShowHeading("Chat");
        ShowStatus("Agent", agentName);
        ShowStatus("Session", sessionId ?? "new");
        ShowStatus("Mode", streaming ? "streaming" : "JSON");
        Line("");
        Line("  /help  /new  /back  /exit");
        Line("  Ctrl+C cancels a request; when idle it exits.");
        Line("");
        while (!exit.IsCancellationRequested)
        {
            var message = await ReadAsync("You > ", exit.Token);
            switch (message)
            {
                case null or "/exit": exit.Cancel(); return;
                case "/back": return;
                case "/new": NewConversation(); Line($"New conversation. Agent: {agentName}"); continue;
                case "/help": Line("Type a message (up to 2000 characters). /new resets the session; /back opens the menu; /exit quits."); continue;
            }
            if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
            {
                Line("Enter a nonempty message of at most 2000 characters.");
                continue;
            }
            var completed = await ExecuteAsync(async token =>
            {
                var request = new ChatGatewayRequestDto { SessionId = sessionId, UserMessage = message, AgentId = agentId };
                output.Write("Assistant > ");
                textFilter.Reset();
                if (!streaming)
                {
                    var reply = await gateway.ChatAsync(request, token);
                    sessionId = reply.SessionId;
                    WriteContent(reply.Reply);
                }
                else
                {
                    await foreach (var item in gateway.StreamChatAsync(request, token))
                    {
                        switch (item.Type)
                        {
                            case "session": sessionId = item.SessionId; break;
                            case "delta": WriteContent(item.Text!); break;
                            case "title": Line($"\nTitle: {item.Title}"); break;
                            case "error": throw new GatewayClientException("Chat failed. Check service logs and saved history before retrying.");
                        }
                    }
                }
                Line($"\nCompleted | Session: {sessionId}");
            });
            if (!completed)
                Line($"Reply incomplete. Inspect conversations before resending; a turn may already be saved. Session: {sessionId ?? "unknown"}");
        }
    }

    private async Task ChooseAgentAsync(CancellationToken cancellationToken)
    {
        ShowHeading("Agents");
        var agents = await gateway.ListAgentsAsync(cancellationToken);
        for (var index = 0; index < agents.Count; index++)
        {
            Line($"  [{index + 1}] {agents[index].Name}");
            Line($"      Tools: {string.Join(", ", agents[index].EnabledTools)}");
        }
        if (agents.Count == 0) Line("No agents exist yet. Create one to enable web research.");
        Line("");
        var choice = await ReadAsync("Number to select, c to create, d for server default, Enter to go back > ", cancellationToken);

        switch (choice)
        {
            case "c":
                await CreateAgentAsync(cancellationToken);
                return;
            case "d":
                SelectAgent(null, "Server default (no explicit selection)");
                return;
        }

        if (int.TryParse(choice, out var number) && number >= 1 && number <= agents.Count)
            SelectAgent(agents[number - 1].Id, agents[number - 1].Name);
        else if (!string.IsNullOrEmpty(choice)) Line("No agent selected: choose a listed number.");
    }

    private async Task CreateAgentAsync(CancellationToken cancellationToken)
    {
        ShowHeading("Create agent");
        var name = await ReadAsync("Agent name (1-200 characters; Enter cancels) > ", cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) return;
        var instructions = await ReadAsync("Instructions > ", cancellationToken);
        if (name.Length > 200 || string.IsNullOrWhiteSpace(instructions))
        {
            Line("A name up to 200 characters and nonempty instructions are required.");
            return;
        }
        var tools = await gateway.ListToolsAsync(cancellationToken);
        Line($"Available tools: {string.Join(", ", tools.Select(tool => tool.Name))}");
        var selection = await ReadAsync("Enabled tool names, comma-separated (Enter = none) > ", cancellationToken);
        if (selection is null) return;
        var enabled = selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (enabled.Any(name => !tools.Any(tool => tool.Name == name)))
        {
            Line("Unknown tool name. Agent was not created.");
            return;
        }
        string? endpoint = null;
        if (enabled.Length != 0)
        {
            var endpointPrompt = options.McpServerEndpoint is null
                ? "MCP endpoint > "
                : $"MCP endpoint [{options.McpServerEndpoint}] > ";
            endpoint = await ReadAsync(endpointPrompt, cancellationToken);
            if (endpoint is null) return;
            if (endpoint.Length == 0) endpoint = options.McpServerEndpoint?.ToString();
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            {
                Line("Invalid MCP endpoint. Agent was not created.");
                return;
            }
        }
        if (await ReadAsync("Create this agent? Type yes > ", cancellationToken) != "yes") return;
        var id = await gateway.CreateAgentAsync(new CreateAgentRequestDto { Name = name, Instructions = instructions, McpServerEndpoint = endpoint, IsDefault = false, EnabledTools = enabled }, cancellationToken);
        SelectAgent(id, name);
        Line($"Agent created: {id}");
    }

    private void SelectAgent(string? id, string name)
    {
        agentId = id;
        agentName = name;
        sessionId = null;
        Line($"Selected: {name}. The next message starts a new conversation.");
    }

    private void NewConversation()
    {
        sessionId = null;
        if (agentId is null) agentName = "Server default (no explicit selection)";
    }

    private async Task ShowToolsAsync(CancellationToken cancellationToken)
    {
        ShowHeading("MCP tools");
        var tools = await gateway.ListToolsAsync(cancellationToken);
        if (tools.Count == 0) Line("No tools discovered.");
        foreach (var tool in tools)
        {
            Line($"  {tool.Name}");
            Line($"    {tool.Description}");
            Line("");
        }
    }

    private async Task<bool> ConversationsAsync(CancellationToken cancellationToken)
    {
        var page = 1;
        while (true)
        {
            var conversations = await gateway.ListConversationsAsync(page, cancellationToken);
            ShowHeading($"Conversations / Page {page}");
            for (var index = 0; index < conversations.Count; index++)
            {
                Line($"  [{index + 1}] {conversations[index].Title}");
                Line($"      Updated: {conversations[index].UpdatedAt:O}");
            }
            if (conversations.Count == 0) Line("No conversations on this page.");
            Line("");
            var choice = await ReadAsync("Number for details, n next, p previous, Enter back > ", cancellationToken);
            if (string.IsNullOrEmpty(choice)) return false;

            switch (choice)
            {
                case "n":
                    {
                        if (conversations.Count == 20) page++; else Line("No next page."); continue;
                    }
                case "p":
                    page = Math.Max(1, page - 1); continue;
            }

            if (!int.TryParse(choice, out var number) || number < 1 || number > conversations.Count)
            {
                Line("Choose a listed conversation.");
                continue;
            }
            var detail = await gateway.GetConversationAsync(conversations[number - 1].SessionId, cancellationToken);
            Line($"Title: {detail.Title} | Session: {detail.SessionId}");
            foreach (var message in detail.Messages) Line($"{message.Role}: {message.Content}");
            var action = await ReadAsync("o open, r rename, d delete, Enter list > ", cancellationToken);

            switch (action)
            {
                case "o":
                    sessionId = detail.SessionId;
                    agentId = null;
                    agentName = "Saved conversation agent (resolved by server)";
                    return true;
                case "r":
                    {
                        var title = await ReadAsync("New title (1-100 characters) > ", cancellationToken);
                        if (!string.IsNullOrWhiteSpace(title) && title.Length <= 100)
                        {
                            await gateway.RenameConversationAsync(detail.SessionId, title, cancellationToken);
                            Line("Conversation renamed.");
                        }
                        else Line("Rename cancelled: a title up to 100 characters is required.");

                        break;
                    }
                case "d" when await ReadAsync("Permanently delete this conversation? Type yes > ", cancellationToken) == "yes":
                    {
                        await gateway.DeleteConversationAsync(detail.SessionId, cancellationToken);
                        if (sessionId == detail.SessionId) NewConversation();
                        Line("Conversation deleted.");
                        break;
                    }
            }
        }
    }

    private async Task<bool> ExecuteAsync(Func<CancellationToken, Task> operation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(exit.Token);
        lock (cancellationLock) activeOperation = cancellation;
        try
        {
            await operation(cancellation.Token);
            return true;
        }
        catch (GatewayClientException exception) { Line($"\n{exception.Message}"); }
        catch (OperationCanceledException) { Line(cancellation.IsCancellationRequested ? "\nOperation cancelled." : "\nRequest timed out."); }
        catch (HttpRequestException) { Line("\nCannot reach Gateway securely. Check Aspire, the URL, and your trusted HTTPS development certificate."); }
        catch (IOException) { Line("\nConnection interrupted. Check Aspire and try again."); }
        catch (JsonException) { Line("\nGateway returned invalid data."); }
        finally
        {
            lock (cancellationLock) activeOperation = null;
        }
        return false;
    }

    private async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
    {
        await output.WriteAsync(prompt);
        // Console.In can block even through ReadLineAsync. Keep a single pending read so
        // cancellation returns control without creating competing readers on the next prompt.
        pendingInput ??= Task.Run(input.ReadLine, cancellationToken);
        var value = await pendingInput.WaitAsync(cancellationToken);
        pendingInput = null;
        return value;
    }

    private void WriteContent(string text)
    {
        output.Write(textFilter.Filter(text));
        output.Flush();
    }

    private void Line(string text)
    {
        textFilter.Reset();
        output.WriteLine(textFilter.Filter(text));
        textFilter.Reset();
    }

    public void Dispose() => exit.Dispose();
}
