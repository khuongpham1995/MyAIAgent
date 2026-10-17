using MyAIAgent.Common.Domain.Entities;

namespace MyAIAgent.KernelMemory.Domain.Entities;

public sealed class DocumentRecord : BaseEntity<string>
{
    public DocumentRecord() : base(Guid.NewGuid().ToString()) { }

    public string DocumentId { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
