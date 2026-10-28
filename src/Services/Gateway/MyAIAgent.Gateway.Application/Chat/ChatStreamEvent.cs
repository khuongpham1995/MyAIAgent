/**
 * MyAIAgent | Gateway · Application
 *
 * Defines the session, text, title, completion, and error payloads exchanged by the chat stream.
 */

using System.Text.Json.Serialization;

namespace MyAIAgent.Gateway.Application.Chat;

public sealed record ChatStreamEvent(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SessionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null)
{
    public const string DoneType = "done";
    public const string ErrorType = "error";

    [JsonIgnore]
    public bool IsTerminal => Type is DoneType or ErrorType;

    public static ChatStreamEvent Error(string message) => new(ErrorType, Message: message);
}
