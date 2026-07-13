using System.Globalization;
using AstilCodex.Contracts;
using Microsoft.Data.Sqlite;

namespace AstilCodex.Memory;

public sealed class SqliteConversationStore : IConversationStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal) { "user", "assistant", "system", "tool" };

    private readonly string _connectionString;

    public SqliteConversationStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        };
        _connectionString = builder.ToString();
    }

    public string DatabasePath { get; }

    public static string GetDefaultDatabasePath()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            localData = AppContext.BaseDirectory;
        }

        return Path.Combine(localData, "AstilCodex", "data", "astil-codex.db");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS conversation_sessions (
                session_id TEXT PRIMARY KEY,
                mode INTEGER NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS conversation_messages (
                message_id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL CHECK (role IN ('user', 'assistant', 'system', 'tool')),
                content TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_messages_session_created
                ON conversation_messages(session_id, created_utc, message_id);

            CREATE INDEX IF NOT EXISTS ix_sessions_updated
                ON conversation_sessions(updated_utc);

            INSERT OR IGNORE INTO schema_migrations(version, applied_utc)
                VALUES ($version, $appliedUtc);
            """;
        command.Parameters.AddWithValue("$version", CurrentSchemaVersion);
        command.Parameters.AddWithValue("$appliedUtc", ToDatabaseTime(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertSessionAsync(
        string sessionId,
        AssistantMode mode,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        var now = ToDatabaseTime(DateTimeOffset.UtcNow);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO conversation_sessions(session_id, mode, created_utc, updated_utc)
            VALUES ($sessionId, $mode, $now, $now)
            ON CONFLICT(session_id) DO UPDATE SET
                mode = excluded.mode,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$mode", (int)mode);
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> AddMessageAsync(
        string sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        if (!AllowedRoles.Contains(role))
        {
            throw new ArgumentException("Role must be user, assistant, system, or tool.", nameof(role));
        }

        ArgumentNullException.ThrowIfNull(content);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = ToDatabaseTime(DateTimeOffset.UtcNow);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO conversation_messages(session_id, role, content, created_utc)
                VALUES ($sessionId, $role, $content, $now);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$sessionId", sessionId);
            insert.Parameters.AddWithValue("$role", role);
            insert.Parameters.AddWithValue("$content", content);
            insert.Parameters.AddWithValue("$now", now);
            var result = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var messageId = Convert.ToInt64(result, CultureInfo.InvariantCulture);

            await using var update = connection.CreateCommand();
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = """
                UPDATE conversation_sessions
                SET updated_utc = $now
                WHERE session_id = $sessionId;
                """;
            update.Parameters.AddWithValue("$sessionId", sessionId);
            update.Parameters.AddWithValue("$now", now);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return messageId;
        }
    }

    public async Task<IReadOnlyList<StoredChatMessage>> GetMessagesAsync(
        string sessionId,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        if (limit is < 1 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 2000.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT message_id, session_id, role, content, created_utc
            FROM (
                SELECT message_id, session_id, role, content, created_utc
                FROM conversation_messages
                WHERE session_id = $sessionId
                ORDER BY message_id DESC
                LIMIT $limit
            )
            ORDER BY message_id;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$limit", limit);

        var messages = new List<StoredChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(new StoredChatMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseDatabaseTime(reader.GetString(4))));
        }

        return messages;
    }

    public async Task<bool> DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM conversation_sessions WHERE session_id = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public async Task<int> PruneSessionsOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM conversation_sessions WHERE updated_utc < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", ToDatabaseTime(cutoff));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM conversation_sessions;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void ValidateSessionId(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (sessionId.Length > 128)
        {
            throw new ArgumentException("Session ID cannot exceed 128 characters.", nameof(sessionId));
        }
    }

    private static string ToDatabaseTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDatabaseTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
