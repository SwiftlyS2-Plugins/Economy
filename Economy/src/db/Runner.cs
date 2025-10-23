using System.Data;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace Economy.Database;

public class MigrationRunner
{
    public static void RunMigrations(IDbConnection dbConnection, string dbConnString)
    {
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner((rb) =>
            {
                if (dbConnString.StartsWith("mysql", StringComparison.OrdinalIgnoreCase))
                {
                    rb.AddMySql5();
                }
                else if (dbConnString.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase))
                {
                    rb.AddPostgres();
                }
                else throw new Exception("Unsupported database type.");

                rb.WithGlobalConnectionString(dbConnection.ConnectionString).ScanIn(typeof(MigrationRunner).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);

        using (var scope = serviceProvider.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }
    }
}