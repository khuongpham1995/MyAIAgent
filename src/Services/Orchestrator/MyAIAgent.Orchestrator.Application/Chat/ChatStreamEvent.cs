/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines the session, text, title, completion, and error payloads exchanged by the chat stream.
 */

using System.Text.Json.Serialization;

namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatStreamEvent(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SessionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null)
{
    public const string SessionType = "session";
    public const string DeltaType = "delta";
    public const string TitleType = "title";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static ChatStreamEvent Session(string sessionId) => new(SessionType, SessionId: sessionId);
    public static ChatStreamEvent Delta(string text) => new(DeltaType, Text: text);
    public static ChatStreamEvent TitleGenerated(string title) => new(TitleType, Title: title);
    public static ChatStreamEvent Done(string sessionId) => new(DoneType, SessionId: sessionId);
    public static ChatStreamEvent Error(string message) => new(ErrorType, Message: message);
}
