using Serilog;
using SunnySunday.Server.Infrastructure.Database;
using SunnySunday.Server.Infrastructure.Logging;

var dbPath = ".data/sunny.db";

var builder = WebApplication.CreateBuilder(args);

SerilogConfiguration.ConfigureLogging(builder, dbPath);

var app = builder.Build();

var schemaBootstrap = new SchemaBootstrap();
await schemaBootstrap.ApplyAsync(dbPath);

Log.Information("Sunny Sunday server started. Database: {DbPath}", dbPath);

app.MapGet("/", () => "Sunny Sunday server is running.");

await app.RunAsync();
