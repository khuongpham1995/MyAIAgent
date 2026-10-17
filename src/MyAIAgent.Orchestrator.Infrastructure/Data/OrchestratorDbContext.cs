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
        modelBuilder.ApplyConfiguration(new ChatSessionConfiguration(IsCosmosProvider));
        modelBuilder.ApplyConfiguration(new AgentDefinitionConfiguration(IsCosmosProvider));
    }
}
