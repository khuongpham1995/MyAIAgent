using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Infrastructure.Data;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data;

public sealed class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<AgentDefinition> AgentDefinitions => Set<AgentDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (IsCosmosProvider)
        {
            modelBuilder.Entity<ChatSession>()
                        .ToContainer("ChatSessions")
                        .HasPartitionKey(x => x.SessionId)
                        .OwnsMany(x => x.Messages);

            modelBuilder.Entity<AgentDefinition>()
                        .ToContainer("AgentDefinitions")
                        .HasPartitionKey(x => x.Id)
                        .OwnsMany(x => x.AgentTools);
        }
        else
        {
            modelBuilder.Entity<ChatSession>().OwnsMany(x => x.Messages);
            modelBuilder.Entity<AgentDefinition>().OwnsMany(x => x.AgentTools);
        }
    }
}
