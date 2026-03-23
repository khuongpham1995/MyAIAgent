namespace MyAIAgent.KernelMemory.Domain.Entities;

public sealed class DocumentRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string DocumentId { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
