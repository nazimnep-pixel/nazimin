namespace AzMepCad.Core.Catalog;

using System.Text.Json;
using AzMepCad.Core.Interfaces;
using Microsoft.Data.Sqlite;

public sealed class SqliteMepCatalog : IMepCatalog, IAsyncDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public SqliteMepCatalog(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates DB from schema.sql and optional JSON seeds.</summary>
    public static async Task CreateFromSeedAsync(
        string schemaSqlPath,
        string? seedDirectory,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await CatalogSchemaBootstrap.ApplySchemaAsync(schemaSqlPath, databasePath, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(seedDirectory))
        {
            return;
        }

        await JsonSeedImporter.ImportFromDemoSeedAsync(seedDirectory, databasePath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogComponent>> AxtarAsync(
        CatalogSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<CatalogComponent>();

        var sql = """
            SELECT id, ad, kod, sistem, diametr_mm, uzunluq_mm, material, istehsalci, teyinat, elave_json
            FROM mep_components
            WHERE (@sistem IS NULL OR sistem = @sistem)
              AND (@material IS NULL OR material = @material)
              AND (@minD IS NULL OR diametr_mm >= @minD)
              AND (@maxD IS NULL OR diametr_mm <= @maxD)
              AND (@q IS NULL OR ad LIKE '%' || @q || '%' OR kod LIKE '%' || @q || '%')
            ORDER BY ad
            """;

        await using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@sistem", filter.Sistem?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@material", filter.Material ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@minD", filter.MinDiametr ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@maxD", filter.MaxDiametr ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@q", filter.AxtarisMetni ?? (object)DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapRow(reader));
        }

        return results;
    }

    public async Task<CatalogComponent?> IdIleGetirAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            SELECT id, ad, kod, sistem, diametr_mm, uzunluq_mm, material, istehsalci, teyinat, elave_json
            FROM mep_components WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapRow(reader) : null;
    }

    public async Task<PipeSizeStandard?> DiametrStandartiniTapAsync(
        string standartKodu,
        double diametrMm,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            SELECT standart_kodu, nominal_ad, xarici_diametr_mm, daxili_diametr_mm, divar_qalinligi_mm
            FROM pipe_size_standards
            WHERE standart_kodu = @kod
              AND ABS(xarici_diametr_mm - @d) < 0.01
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@kod", standartKodu);
        cmd.Parameters.AddWithValue("@d", diametrMm);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new PipeSizeStandard(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            reader.GetDouble(4));
    }

    public async Task<IReadOnlyList<PipeSizeStandard>> GetPipeSizesAsync(
        string standard,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<PipeSizeStandard>();

        await using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            SELECT standart_kodu, nominal_ad, xarici_diametr_mm, daxili_diametr_mm, divar_qalinligi_mm
            FROM pipe_size_standards
            WHERE standart_kodu = @kod
            ORDER BY xarici_diametr_mm
            """;
        cmd.Parameters.AddWithValue("@kod", standard);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new PipeSizeStandard(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4)));
        }

        return results;
    }

    public async Task<CatalogFitting?> GetFittingAsync(
        FittingNovu type,
        double dn,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            SELECT id, ad, kod, nov, dn_mm, material
            FROM mep_fittings
            WHERE nov = @nov AND ABS(dn_mm - @dn) < 0.01
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@nov", type.ToString());
        cmd.Parameters.AddWithValue("@dn", dn);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        Enum.TryParse<FittingNovu>(reader.GetString(3), out var nov);
        return new CatalogFitting(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            nov,
            reader.GetDouble(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static CatalogComponent MapRow(SqliteDataReader reader)
    {
        var extraJson = reader.IsDBNull(9) ? null : reader.GetString(9);
        var extra = string.IsNullOrWhiteSpace(extraJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(extraJson!) ?? new Dictionary<string, string>();

        Enum.TryParse<MepDiscipline>(reader.GetString(3), out var sistem);

        return new CatalogComponent
        {
            Id = reader.GetString(0),
            Ad = reader.GetString(1),
            Kod = reader.IsDBNull(2) ? null : reader.GetString(2),
            Sistem = sistem,
            Diametr = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            Uzunluq = reader.IsDBNull(5) ? null : reader.GetDouble(5),
            Material = reader.IsDBNull(6) ? null : reader.GetString(6),
            Istehsalci = reader.IsDBNull(7) ? null : reader.GetString(7),
            Teyinat = reader.IsDBNull(8) ? null : reader.GetString(8),
            ElaveAtributlar = extra,
        };
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
