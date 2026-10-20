namespace AzMepCad.Core.Catalog;

using Microsoft.Data.Sqlite;

/// <summary>
/// Applies schema.sql to a SQLite database file.
/// </summary>
public static class CatalogSchemaBootstrap
{
    public static async Task ApplySchemaAsync(string schemaSqlPath, string databasePath, CancellationToken cancellationToken = default)
    {
        var schema = await File.ReadAllTextAsync(schemaSqlPath, cancellationToken).ConfigureAwait(false);
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = schema;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
