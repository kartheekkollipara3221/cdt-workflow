using Microsoft.EntityFrameworkCore;

namespace WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
}
