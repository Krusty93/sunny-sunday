using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi;
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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
       "v1",
        new OpenApiInfo
        {
            Title = "Sunny Sunday APIs",
            Version = "v1",
            Contact = new OpenApiContact
            {
                Name = "Sunny Sunday",
                Url = new Uri("https://github.com/Krusty93/sunny-sunday"),
            }
        }
    );

    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

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
    app.UseSwaggerUI(options =>
    {
        options.DisplayRequestDuration();
    });
}

app.MapGet("/", () => "Sunny Sunday server is running.");

app.MapSyncEndpoints();

var schemaBootstrap = new SchemaBootstrap();
await schemaBootstrap.ApplyAsync(dbPath);

Log.Information("Sunny Sunday server started. Database: {DbPath}", dbPath);

await app.RunAsync();
