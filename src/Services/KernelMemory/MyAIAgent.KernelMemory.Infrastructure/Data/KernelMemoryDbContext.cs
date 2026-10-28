/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Defines the Cosmos document-record model and its shared database throughput configuration.
 */

using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Infrastructure.Data;
using MyAIAgent.KernelMemory.Domain.Entities;
using MyAIAgent.KernelMemory.Infrastructure.Data.Configurations;

namespace MyAIAgent.KernelMemory.Infrastructure.Data;

public sealed class KernelMemoryDbContext(DbContextOptions<KernelMemoryDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<DocumentRecord> DocumentRecords => Set<DocumentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Shared database throughput keeps every container inside the free tier budget; a per-container setting would not.
        modelBuilder.HasManualThroughput(400);

        modelBuilder.ApplyConfiguration(new DocumentRecordConfiguration());
    }
}
