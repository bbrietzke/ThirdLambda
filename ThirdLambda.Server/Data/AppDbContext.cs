// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace ThirdLambda.Server.Data;

public abstract class AppDbContext : DbContext
{
    protected AppDbContext(DbContextOptions options) : base(options) { }

    // public DbSet<Job> Jobs => Set<Job>();

    // Model configuration goes here once and is shared by both providers
    // protected override void OnModelCreating(ModelBuilder modelBuilder) { ... }
}

public class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);

public class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : AppDbContext(options);