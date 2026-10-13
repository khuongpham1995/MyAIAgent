using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.KernelMemory.Domain.Entities;

namespace MyAIAgent.KernelMemory.Infrastructure.Data.Configurations;

public sealed class DocumentRecordConfiguration(bool isCosmosProvider) : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        if (isCosmosProvider)
            builder.ToContainer("DocumentRecords").HasPartitionKey(record => record.IndexName);
    }
}
