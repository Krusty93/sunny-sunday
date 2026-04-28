using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi;
using Quartz;
using Serilog;
using SunnySunday.Core.Contracts;
using SunnySunday.Server.Data;
using SunnySunday.Server.Endpoints;
using SunnySunday.Server.Infrastructure.Database;
using SunnySunday.Server.Infrastructure.Logging;
using SunnySunday.Server.Infrastructure.Smtp;
using SunnySunday.Server.Jobs;
using SunnySunday.Server.Services;

var dbPath = ".data/sunny.db";
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

var builder = WebApplication.CreateBuilder(args);

SerilogConfiguration.ConfigureLogging(builder, dbPath);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Smtp:Host"] = Environment.GetEnvironmentVariable("SMTP_HOST"),
    ["Smtp:Port"] = Environment.GetEnvironmentVariable("SMTP_PORT"),
    ["Smtp:Username"] = Environment.GetEnvironmentVariable("SMTP_USER"),
    ["Smtp:Password"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
    ["Smtp:FromAddress"] = Environment.GetEnvironmentVariable("SMTP_FROM_ADDRESS"),
});

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

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

    IncludeXmlCommentsIfPresent(options, Assembly.GetExecutingAssembly());
    IncludeXmlCommentsIfPresent(options, typeof(SettingsResponse).Assembly);
});

builder.Services.AddScoped<IDbConnection>(_ =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var pragma = conn.CreateCommand();
    pragma.CommandText = "PRAGMA foreign_keys = ON;";
    pragma.ExecuteNonQuery();
    return conn;
});

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SyncRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<StatusRepository>();
builder.Services.AddScoped<ExclusionRepository>();
builder.Services.AddScoped<WeightRepository>();
builder.Services.AddScoped<RecapRepository>();

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

builder.Services.AddSingleton<ISchedulerService, SchedulerService>();
builder.Services.AddTransient<RecapJob>();

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
app.MapSettingsEndpoints();
app.MapStatusEndpoints();
app.MapExclusionEndpoints();
app.MapWeightEndpoints();

var schemaBootstrap = new SchemaBootstrap();
await schemaBootstrap.ApplyAsync(dbPath);

// Schedule initial recap trigger from persisted settings
{
    await using var scope = app.Services.CreateAsyncScope();
    var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
    var settingsRepo = scope.ServiceProvider.GetRequiredService<SettingsRepository>();
    var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

    var userId = await userRepo.EnsureUserAsync();
    var settings = await settingsRepo.GetByUserIdAsync(userId);
    await schedulerService.ScheduleAsync(settings);
}

Log.Information("Sunny Sunday server started. Database: {DbPath}", dbPath);

await app.RunAsync();

static void IncludeXmlCommentsIfPresent(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options, Assembly assembly)
{
    var xmlFile = $"{assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
}
