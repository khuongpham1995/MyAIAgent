/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Maps document tracking records to their Cosmos container and index-name partition key.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.KernelMemory.Domain.Entities;

namespace MyAIAgent.KernelMemory.Infrastructure.Data.Configurations;

public sealed class DocumentRecordConfiguration : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        builder.ToContainer("DocumentRecords").HasPartitionKey(record => record.IndexName);
    }
}
