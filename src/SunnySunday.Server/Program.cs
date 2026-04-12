using SunnySunday.Server.Infrastructure.Database;

var dbPath = ".data/sunny.db";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var schemaBootstrap = new SchemaBootstrap();
await schemaBootstrap.ApplyAsync(dbPath);

app.MapGet("/", () => "Sunny Sunday server is running.");

await app.RunAsync();
