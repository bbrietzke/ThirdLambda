using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using ThirdLambda.Server.Data;
using ThirdLambda.Server.Services;

namespace ThirdLambda.Server;

public enum DatabaseProvider { SqlServer, Sqlite }

public class Program
{
    private static readonly Option<DatabaseProvider?> DatabaseOption = new("--database", "-d")
    {
        Description = "Database provider (sqlite or sqlserver). Overrides Database:Provider in config.",
        Recursive = true,
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

    // Works for both HostApplicationBuilder and WebApplicationBuilder
    private static void ConfigureDatabase(IHostApplicationBuilder builder, ParseResult parseResult)
    {
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
    }

    private static Command BuildMigrateCommand()
    {
        var command = new Command("migrate", "Run database migrations");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var builder = Host.CreateApplicationBuilder();
            ConfigureDatabase(builder, parseResult);

            using var host = builder.Build();
            await using var scope = host.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            return 0;
        });

        return command;
    }

    private static Command BuildServeCommand()
    {
        var command = new Command("serve", "Start the server");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var builder = WebApplication.CreateBuilder();
            ConfigureDatabase(builder, parseResult);

            builder.Services.AddGrpc();
            builder.Services.AddThirdLambdaHealthChecks();
            builder.Services.AddHostedService<Worker>();

            var app = builder.Build();
            app.MapGrpcService<JobGrpcService>();
            app.MapThirdLambdaHealthChecks(requireHost: "*:8081");

            await app.RunAsync();
            return 0;
        });

        return command;
    }
}