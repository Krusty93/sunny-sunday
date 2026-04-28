using System.Reflection;
using Microsoft.Data.Sqlite;

namespace SunnySunday.Server.Infrastructure.Database;

/// <summary>
/// Initializes Quartz.NET persistent store tables from the embedded SQL script.
/// </summary>
public static class QuartzSchemaInitializer
{
    private const string ResourceName = "SunnySunday.Server.Infrastructure.Database.quartz-sqlite.sql";

    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = ReadEmbeddedSql();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ApplyAsync(connection, cancellationToken);
    }

    private static string ReadEmbeddedSql()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
