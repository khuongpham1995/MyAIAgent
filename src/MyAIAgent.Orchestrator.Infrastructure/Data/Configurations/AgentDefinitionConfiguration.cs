using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Configurations;

public sealed class AgentDefinitionConfiguration(bool isCosmosProvider) : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        if (isCosmosProvider)
            builder.ToContainer("AgentDefinitions").HasPartitionKey(agent => agent.Id);

        builder.OwnsMany(agent => agent.AgentTools);
    }
}
