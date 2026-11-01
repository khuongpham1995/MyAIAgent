/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Maps owned messages and concurrency metadata to Cosmos sessions partitioned by user identity.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Configurations;

public sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToContainer("ChatSessions").HasPartitionKey(session => session.UserId);
        // A delayed title or summary must not replace a turn committed by another request.
        builder.UseETagConcurrency();

        builder.OwnsMany(session => session.Messages);
    }
}
