using System.Data;
using Microsoft.Data.Sqlite;
using Serilog;
using SunnySunday.Server.Data;
using SunnySunday.Server.Endpoints;
using SunnySunday.Server.Infrastructure.Database;
using SunnySunday.Server.Infrastructure.Logging;

var dbPath = ".data/sunny.db";
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

var builder = WebApplication.CreateBuilder(args);

SerilogConfiguration.ConfigureLogging(builder, dbPath);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IDbConnection>(_ =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SyncRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Sunny Sunday server is running.");

app.MapSyncEndpoints();

var schemaBootstrap = new SchemaBootstrap();
await schemaBootstrap.ApplyAsync(dbPath);

Log.Information("Sunny Sunday server started. Database: {DbPath}", dbPath);

await app.RunAsync();

public partial class Program { }
