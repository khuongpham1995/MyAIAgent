/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Maps agent definitions and owned tools to the Cosmos AgentDefinitions container.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Configurations;

public sealed class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        builder.ToContainer("AgentDefinitions").HasPartitionKey(agent => agent.Id);

        builder.OwnsMany(agent => agent.AgentTools);
    }
}
