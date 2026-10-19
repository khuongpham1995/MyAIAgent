using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Infrastructure.Data;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data.Configurations;

namespace MyAIAgent.Orchestrator.Infrastructure.Data;

public sealed class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<AgentDefinition> AgentDefinitions => Set<AgentDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Shared database throughput keeps every container inside the free tier budget; a per-container setting would not.
        modelBuilder.HasManualThroughput(400);

        modelBuilder.ApplyConfiguration(new ChatSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AgentDefinitionConfiguration());
    }
}
