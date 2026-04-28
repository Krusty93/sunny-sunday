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
            count         INTEGER NOT NULL DEFAULT 3 CHECK(count BETWEEN 1 AND 15),
            timezone      TEXT    NOT NULL DEFAULT 'UTC'
        );

        CREATE TABLE IF NOT EXISTS recap_jobs (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id       INTEGER NOT NULL REFERENCES users(id),
            scheduled_for TEXT    NOT NULL,
            status        TEXT    NOT NULL DEFAULT 'pending',
            attempt_count INTEGER NOT NULL DEFAULT 0,
            error_message TEXT    NULL,
            created_at    TEXT    NOT NULL,
            delivered_at  TEXT    NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_authors_name
            ON authors(name);

        CREATE UNIQUE INDEX IF NOT EXISTS uq_books_user_author_title
            ON books(user_id, author_id, title);

        CREATE UNIQUE INDEX IF NOT EXISTS uq_highlights_user_book_text
            ON highlights(user_id, book_id, text);

        CREATE UNIQUE INDEX IF NOT EXISTS uq_recap_jobs_user_slot
            ON recap_jobs(user_id, scheduled_for);
        """;

    // Quartz.NET ADO.NET persistent job store schema for SQLite.
    // Source: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_sqlite.sql
    private const string QuartzSchemaSql = """
        CREATE TABLE IF NOT EXISTS QRTZ_JOB_DETAILS (
            SCHED_NAME    NVARCHAR(120) NOT NULL,
            JOB_NAME      NVARCHAR(150) NOT NULL,
            JOB_GROUP     NVARCHAR(150) NOT NULL,
            DESCRIPTION   NVARCHAR(250) NULL,
            JOB_CLASS_NAME NVARCHAR(250) NOT NULL,
            IS_DURABLE    BIT NOT NULL,
            IS_NONCONCURRENT BIT NOT NULL,
            IS_UPDATE_DATA BIT NOT NULL,
            REQUESTS_RECOVERY BIT NOT NULL,
            JOB_DATA      BLOB NULL,
            PRIMARY KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_TRIGGERS (
            SCHED_NAME     NVARCHAR(120) NOT NULL,
            TRIGGER_NAME   NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP  NVARCHAR(150) NOT NULL,
            JOB_NAME       NVARCHAR(150) NOT NULL,
            JOB_GROUP      NVARCHAR(150) NOT NULL,
            DESCRIPTION    NVARCHAR(250) NULL,
            NEXT_FIRE_TIME BIGINT NULL,
            PREV_FIRE_TIME BIGINT NULL,
            PRIORITY       INTEGER NULL,
            TRIGGER_STATE  NVARCHAR(16) NOT NULL,
            TRIGGER_TYPE   NVARCHAR(8) NOT NULL,
            START_TIME     BIGINT NOT NULL,
            END_TIME       BIGINT NULL,
            CALENDAR_NAME  NVARCHAR(200) NULL,
            MISFIRE_INSTR  INTEGER NULL,
            MISFIRE_ORIG_FIRE_TIME INTEGER NULL,
            EXECUTION_GROUP NVARCHAR(200) NULL,
            JOB_DATA       BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
                REFERENCES QRTZ_JOB_DETAILS(SCHED_NAME, JOB_NAME, JOB_GROUP)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_SIMPLE_TRIGGERS (
            SCHED_NAME     NVARCHAR(120) NOT NULL,
            TRIGGER_NAME   NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP  NVARCHAR(150) NOT NULL,
            REPEAT_COUNT   BIGINT NOT NULL,
            REPEAT_INTERVAL BIGINT NOT NULL,
            TIMES_TRIGGERED BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS QRTZ_SIMPROP_TRIGGERS (
            SCHED_NAME     NVARCHAR(120) NOT NULL,
            TRIGGER_NAME   NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP  NVARCHAR(150) NOT NULL,
            STR_PROP_1     NVARCHAR(512) NULL,
            STR_PROP_2     NVARCHAR(512) NULL,
            STR_PROP_3     NVARCHAR(512) NULL,
            INT_PROP_1     INT NULL,
            INT_PROP_2     INT NULL,
            LONG_PROP_1    BIGINT NULL,
            LONG_PROP_2    BIGINT NULL,
            DEC_PROP_1     NUMERIC NULL,
            DEC_PROP_2     NUMERIC NULL,
            BOOL_PROP_1    BIT NULL,
            BOOL_PROP_2    BIT NULL,
            TIME_ZONE_ID   NVARCHAR(80) NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS QRTZ_CRON_TRIGGERS (
            SCHED_NAME      NVARCHAR(120) NOT NULL,
            TRIGGER_NAME    NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP   NVARCHAR(150) NOT NULL,
            CRON_EXPRESSION NVARCHAR(250) NOT NULL,
            TIME_ZONE_ID    NVARCHAR(80),
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS QRTZ_BLOB_TRIGGERS (
            SCHED_NAME     NVARCHAR(120) NOT NULL,
            TRIGGER_NAME   NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP  NVARCHAR(150) NOT NULL,
            BLOB_DATA      BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS QRTZ_CALENDARS (
            SCHED_NAME    NVARCHAR(120) NOT NULL,
            CALENDAR_NAME NVARCHAR(200) NOT NULL,
            CALENDAR      BLOB NOT NULL,
            PRIMARY KEY (SCHED_NAME, CALENDAR_NAME)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_PAUSED_TRIGGER_GRPS (
            SCHED_NAME    NVARCHAR(120) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_GROUP)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_FIRED_TRIGGERS (
            SCHED_NAME     NVARCHAR(120) NOT NULL,
            ENTRY_ID       NVARCHAR(140) NOT NULL,
            TRIGGER_NAME   NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP  NVARCHAR(150) NOT NULL,
            INSTANCE_NAME  NVARCHAR(200) NOT NULL,
            FIRED_TIME     BIGINT NOT NULL,
            SCHED_TIME     BIGINT NOT NULL,
            PRIORITY       INTEGER NOT NULL,
            STATE          NVARCHAR(16) NOT NULL,
            JOB_NAME       NVARCHAR(150) NULL,
            JOB_GROUP      NVARCHAR(150) NULL,
            IS_NONCONCURRENT BIT NULL,
            REQUESTS_RECOVERY BIT NULL,
            EXECUTION_GROUP NVARCHAR(200) NULL,
            PRIMARY KEY (SCHED_NAME, ENTRY_ID)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_SCHEDULER_STATE (
            SCHED_NAME      NVARCHAR(120) NOT NULL,
            INSTANCE_NAME   NVARCHAR(200) NOT NULL,
            LAST_CHECKIN_TIME BIGINT NOT NULL,
            CHECKIN_INTERVAL BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, INSTANCE_NAME)
        );

        CREATE TABLE IF NOT EXISTS QRTZ_LOCKS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            LOCK_NAME  NVARCHAR(40) NOT NULL,
            PRIMARY KEY (SCHED_NAME, LOCK_NAME)
        );
        """;

    public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var quartzCommand = connection.CreateCommand();
        quartzCommand.CommandText = QuartzSchemaSql;
        await quartzCommand.ExecuteNonQueryAsync(cancellationToken);
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

        await using var quartzCommand = connection.CreateCommand();
        quartzCommand.CommandText = QuartzSchemaSql;
        await quartzCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
