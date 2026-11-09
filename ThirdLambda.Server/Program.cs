using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using ThirdLambda.Server.Data;

namespace ThirdLambda.Server;

public enum DatabaseProvider { SqlServer, Sqlite }

public class Program
{
    private static readonly Option<DatabaseProvider?> DatabaseOption = new("--database", "-d")
    {
        Description = "Database provider (sqlite or sqlserver). Overrides Database:Provider in config.",
        Recursive = true,   // usable with both serve and migrate
    };

    public static async Task<int> Main(string[] args)
    {
        RootCommand root = new RootCommand("ThirdLambda Server");
        root.Options.Add(DatabaseOption);

        root.Subcommands.Add(BuildMigrateCommand());
        root.Subcommands.Add(BuildServeCommand());

        var invocation = new InvocationConfiguration { ProcessTerminationTimeout = null };
        return await root.Parse(args).InvokeAsync(invocation);
    }

    private static HostApplicationBuilder CreateBuilder(ParseResult parseResult)
    {
        var builder = Host.CreateApplicationBuilder();

        // Precedence: --database flag > Database:Provider config/env var > SqlServer
        var provider = parseResult.GetValue(DatabaseOption)
            ?? builder.Configuration.GetValue<DatabaseProvider?>("Database:Provider")
            ?? DatabaseProvider.SqlServer;

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                builder.Services.AddDbContext<AppDbContext, SqliteAppDbContext>(o =>
                    o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));
                break;

            case DatabaseProvider.SqlServer:
                builder.Services.AddDbContext<AppDbContext, SqlServerAppDbContext>(o =>
                    o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
                break;
        }

        return builder;
    }

    private static Command BuildMigrateCommand()
    {
        var command = new Command("migrate", "Run database migrations");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = CreateBuilder(parseResult).Build();
            await using var scope = host.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(cancellationToken);   // applies that provider's migrations

            return 0;
        });

        return command;
    }

    private static Command BuildServeCommand()
    {
        var command = new Command("serve", "Start the server");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var builder = CreateBuilder(parseResult);
            builder.Services.AddHostedService<Worker>();

            using var host = builder.Build();
            await host.RunAsync();
            return 0;
        });

        return command;
    }
}