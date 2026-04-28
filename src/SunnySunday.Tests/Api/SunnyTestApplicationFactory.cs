using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SunnySunday.Server.Infrastructure.Database;

namespace SunnySunday.Tests.Api;

public sealed class SunnyTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public SunnyTestApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        var bootstrap = new SchemaBootstrap();
        bootstrap.ApplyAsync(_connection).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnection));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddSingleton<IDbConnection>(_ => _connection);

            // Override Quartz to use in-memory store for tests
            // (AdoJobStore can't share the in-memory SQLite connection)
            services.PostConfigure<QuartzOptions>(options =>
            {
                // Remove all persistent store and data source properties
                foreach (var key in options.Keys.Cast<string>().ToList())
                {
                    if (key.StartsWith("quartz.jobStore.") ||
                        key.StartsWith("quartz.dataSource.") ||
                        key == "quartz.serializer.type")
                    {
                        options.Remove(key);
                    }
                }

                options["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz";
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
