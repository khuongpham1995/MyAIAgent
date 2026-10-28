/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Verifies the document tracking entity's identifier and metadata behavior.
 */

using FluentAssertions;
using MyAIAgent.KernelMemory.Domain.Entities;

namespace MyAIAgent.KernelMemory.Tests.Unit.Domain.Entities;

public sealed class DocumentRecordTests
{
    [Fact]
    public void Constructor_WhenCreated_ShouldHaveNonEmptyId()
    {
        // Arrange & Act
        var record = new DocumentRecord();

        // Assert
        record.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenCreated_ShouldHaveUniqueIds()
    {
        // Arrange & Act
        var first = new DocumentRecord();
        var second = new DocumentRecord();

        // Assert
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Constructor_WhenInitialized_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var uploadedAt = DateTimeOffset.UtcNow;

        // Act
        var record = new DocumentRecord
        {
            DocumentId = "doc-123",
            IndexName = "my-index",
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 204800,
            UploadedAt = uploadedAt
        };

        // Assert
        record.DocumentId.Should().Be("doc-123");
        record.IndexName.Should().Be("my-index");
        record.FileName.Should().Be("report.pdf");
        record.ContentType.Should().Be("application/pdf");
        record.FileSizeBytes.Should().Be(204800);
        record.UploadedAt.Should().Be(uploadedAt);
    }
}
