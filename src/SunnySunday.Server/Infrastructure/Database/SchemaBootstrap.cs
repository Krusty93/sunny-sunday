using Microsoft.Data.Sqlite;

namespace SunnySunday.Server.Infrastructure.Database;

public sealed class SchemaBootstrap
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS users (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            kindle_email TEXT    NOT NULL UNIQUE,
            created_at   TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS authors (
            id   INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS books (
            id        INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id   INTEGER NOT NULL REFERENCES users(id),
            author_id INTEGER NOT NULL REFERENCES authors(id),
            title     TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS highlights (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id        INTEGER NOT NULL REFERENCES users(id),
            book_id        INTEGER NOT NULL REFERENCES books(id),
            text           TEXT    NOT NULL,
            weight         INTEGER NOT NULL DEFAULT 3 CHECK(weight BETWEEN 1 AND 5),
            excluded       INTEGER NOT NULL DEFAULT 0,
            last_seen      TEXT    NULL,
            delivery_count INTEGER NOT NULL DEFAULT 0,
            created_at     TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS excluded_books (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id     INTEGER NOT NULL REFERENCES users(id),
            book_id     INTEGER NOT NULL REFERENCES books(id),
            excluded_at TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS excluded_authors (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id     INTEGER NOT NULL REFERENCES users(id),
            author_id   INTEGER NOT NULL REFERENCES authors(id),
            excluded_at TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS settings (
            user_id       INTEGER PRIMARY KEY REFERENCES users(id),
            schedule      TEXT    NOT NULL DEFAULT 'weekly',
            delivery_day  TEXT    NULL,
            delivery_time TEXT    NOT NULL DEFAULT '18:00',
            count         INTEGER NOT NULL DEFAULT 3 CHECK(count BETWEEN 1 AND 15)
        );
        """;

    public void Apply(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();
    }

    public async Task ApplyAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
