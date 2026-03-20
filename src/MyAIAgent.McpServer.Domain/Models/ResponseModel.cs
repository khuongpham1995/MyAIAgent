namespace MyAIAgent.McpServer.Domain.Models;

public record ResponseModel<T>
{
    public string? Message { get; init; }
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public IReadOnlyDictionary<string, string> Errors { get; init; }
        = new Dictionary<string, string>();

    public static ResponseModel<T> Succeed(
        T? data = default,
        string? message = null)
        => new()
        {
            Data = data,
            Message = message,
            IsSuccess = true
        };

    public static ResponseModel<T> Fail(
        IReadOnlyDictionary<string, string>? errors = null,
        string? message = null)
        => new()
        {
            Errors = errors ?? new Dictionary<string, string>(),
            Message = message,
            IsSuccess = false
        };
}