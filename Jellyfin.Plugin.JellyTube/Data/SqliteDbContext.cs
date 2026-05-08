using System.IO;
using Dapper;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.JellyTube.Data;

/// <summary>
/// Represents the SQLite database context.
/// </summary>
public class SqliteDbContext
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDbContext"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths configuration.</param>
    public SqliteDbContext(IApplicationPaths applicationPaths)
    {
        var dbPath = Path.Combine(applicationPaths.PluginConfigurationsPath, "youtube_plugin.db");
        _connectionString = $"Data Source={dbPath};";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS DMJT_Sources (
                Id TEXT PRIMARY KEY,
                Url TEXT NOT NULL UNIQUE,
                Type TEXT NOT NULL,
                Title TEXT,
                Uploader TEXT,
                Description TEXT,
                ThumbnailUrl TEXT,
                ChannelId TEXT,
                LibraryId TEXT,
                LastSyncedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS DMJT_Videos (
                Id TEXT PRIMARY KEY,
                SourceId TEXT NOT NULL,
                Title TEXT,
                Uploader TEXT,
                Duration INTEGER,
                ThumbnailUrl TEXT,
                Description TEXT,
                UploadDate TEXT,
                ViewCount INTEGER,
                FOREIGN KEY(SourceId) REFERENCES DMJT_Sources(Id)
            );

            CREATE TABLE IF NOT EXISTS DMJT_StreamCache (
                VideoId TEXT PRIMARY KEY,
                StreamUrlsJson TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL
            );
        ");

        try
        {
            connection.Execute("ALTER TABLE DMJT_Sources ADD COLUMN LibraryId TEXT");
        }
        catch
        {
        }
    }

    /// <summary>
    /// Gets a new SQLite database connection.
    /// </summary>
    /// <returns>A new instance of <see cref="SqliteConnection"/>.</returns>
    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA foreign_keys = OFF");
        return connection;
    }
}
