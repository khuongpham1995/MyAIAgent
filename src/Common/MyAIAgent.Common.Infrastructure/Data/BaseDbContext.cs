/**
 * MyAIAgent | Common · Infrastructure
 *
 * Provides the common EF Core context base used by service-specific persistence contexts.
 */

using Microsoft.EntityFrameworkCore;

namespace MyAIAgent.Common.Infrastructure.Data;

public abstract class BaseDbContext(DbContextOptions options) : DbContext(options);
