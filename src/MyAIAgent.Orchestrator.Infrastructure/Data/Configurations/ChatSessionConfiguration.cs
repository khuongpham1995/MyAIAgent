using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Configurations;

public sealed class ChatSessionConfiguration(bool isCosmosProvider) : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        if (isCosmosProvider)
            builder.ToContainer("ChatSessions").HasPartitionKey(session => session.SessionId);

        builder.OwnsMany(session => session.Messages);
    }
}
