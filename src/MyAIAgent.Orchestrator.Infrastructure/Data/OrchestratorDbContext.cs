using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Infrastructure.Data;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data;

public sealed class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSession>()
            .ToContainer("ChatSessions")
            .HasPartitionKey(x => x.SessionId)
            .OwnsMany(x => x.Messages);
    }
}
