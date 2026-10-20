namespace AzMepCad.Core.Catalog;

using System.Text.Json;
using Microsoft.Data.Sqlite;

/// <summary>
/// Populates pipe_size_standards from demo JSON seed files (public dimensions only).
/// </summary>
public static class JsonSeedImporter
{
    public static async Task ImportPipeSizesAsync(
        string jsonPath,
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(jsonPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        if (!root.TryGetProperty("standart", out var standartEl))
        {
            throw new InvalidDataException("JSON seed: 'standart' sahəsi tələb olunur.");
        }

        var standartKodu = standartEl.GetString()
            ?? throw new InvalidDataException("JSON seed: standart kodu boş ola bilməz.");

        if (!root.TryGetProperty("boru_olculeri", out var sizes) || sizes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in sizes.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nominal = item.GetProperty("nominal").GetString()
                ?? throw new InvalidDataException("boru_olculeri: nominal tələb olunur.");
            var xarici = item.GetProperty("Diametr_mm").GetDouble();
            var daxili = item.GetProperty("daxili_diametr_mm").GetDouble();
            var qalinliq = item.GetProperty("divar_qalinligi_mm").GetDouble();
            var id = $"{standartKodu}-{nominal}".ToLowerInvariant().Replace(' ', '-');

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO pipe_size_standards
                    (id, standart_kodu, nominal_ad, xarici_diametr_mm, daxili_diametr_mm, divar_qalinligi_mm)
                VALUES (@id, @kod, @nom, @x, @d, @q)
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@kod", standartKodu);
            cmd.Parameters.AddWithValue("@nom", nominal);
            cmd.Parameters.AddWithValue("@x", xarici);
            cmd.Parameters.AddWithValue("@d", daxili);
            cmd.Parameters.AddWithValue("@q", qalinliq);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task ImportFittingsAsync(
        string jsonPath,
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jsonPath))
        {
            return;
        }

        await using var stream = File.OpenRead(jsonPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        if (!root.TryGetProperty("armaturlar", out var fittings) || fittings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in fittings.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = item.GetProperty("id").GetString()!;
            var nov = item.GetProperty("nov").GetString()!;
            var dn = item.GetProperty("dn_mm").GetDouble();
            var ad = item.GetProperty("ad").GetString()!;
            var kod = item.TryGetProperty("kod", out var kodEl) ? kodEl.GetString() : null;
            var material = item.TryGetProperty("material", out var matEl) ? matEl.GetString() : null;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO mep_fittings (id, nov, dn_mm, ad, kod, material)
                VALUES (@id, @nov, @dn, @ad, @kod, @mat)
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@nov", nov);
            cmd.Parameters.AddWithValue("@dn", dn);
            cmd.Parameters.AddWithValue("@ad", ad);
            cmd.Parameters.AddWithValue("@kod", kod ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@mat", material ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task ImportFromDemoSeedAsync(
        string seedDirectory,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        var pipeJson = Path.Combine(seedDirectory, "demo-pipe-sizes.json");
        var fittingsJson = Path.Combine(seedDirectory, "demo-fittings.json");

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (File.Exists(pipeJson))
        {
            await ImportPipeSizesAsync(pipeJson, connection, cancellationToken).ConfigureAwait(false);
        }

        await ImportFittingsAsync(fittingsJson, connection, cancellationToken).ConfigureAwait(false);
    }
}
