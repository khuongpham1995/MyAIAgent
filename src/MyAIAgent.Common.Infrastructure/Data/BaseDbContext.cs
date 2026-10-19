using Microsoft.EntityFrameworkCore;

namespace MyAIAgent.Common.Infrastructure.Data;

public abstract class BaseDbContext(DbContextOptions options) : DbContext(options);
