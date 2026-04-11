using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Infrastructure.Data;
using MyAIAgent.KernelMemory.Domain.Entities;

namespace MyAIAgent.KernelMemory.Infrastructure.Data;

public sealed class KernelMemoryDbContext(DbContextOptions<KernelMemoryDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<DocumentRecord> DocumentRecords => Set<DocumentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (IsCosmosProvider)
        {
            modelBuilder.Entity<DocumentRecord>()
                        .ToContainer("DocumentRecords")
                        .HasPartitionKey(x => x.IndexName);
        }
    }
}
