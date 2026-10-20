namespace AzMepCad.Boq;

using System.Globalization;
using System.Text;
using AzMepCad.Core.Interfaces;
using ClosedXML.Excel;

/// <summary>
/// Excel BOQ export with az-AZ column headers.
/// </summary>
public sealed class ClosedXmlBoqExporter : IBoqExporter
{
    private static readonly string[] ExcelHeaders =
    [
        "Sıra",
        "Kod",
        "Təyinat",
        "Material",
        "Ölçü vahidi",
        "Miqdar",
        "Qeyd",
    ];

    public Task ExportAsync(BoqExportRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return request.Format switch
        {
            BoqExportFormat.Csv => ExportCsvAsync(request, output, cancellationToken),
            _ => ExportExcelAsync(request, output, cancellationToken),
        };
    }

    private static Task ExportExcelAsync(BoqExportRequest request, Stream output, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("SMETA");

        sheet.Cell(1, 1).Value = request.LayiheAdi;
        sheet.Range(1, 1, 1, ExcelHeaders.Length).Merge();

        for (var c = 0; c < ExcelHeaders.Length; c++)
        {
            sheet.Cell(2, c + 1).Value = ExcelHeaders[c];
        }

        var row = 3;
        foreach (var item in request.Setrler)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheet.Cell(row, 1).Value = item.Sira;
            sheet.Cell(row, 2).Value = item.Kod;
            sheet.Cell(row, 3).Value = item.Teyinat;
            sheet.Cell(row, 4).Value = item.Material;
            sheet.Cell(row, 5).Value = item.OlcuVahidi;
            sheet.Cell(row, 6).Value = item.Miqdar;
            sheet.Cell(row, 7).Value = item.Qeyd ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(output);
        return Task.CompletedTask;
    }

    private static Task ExportCsvAsync(BoqExportRequest request, Stream output, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.WriteLine(string.Join(';', ExcelHeaders));

        foreach (var item in request.Setrler)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteLine(string.Join(';',
                item.Sira.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(item.Kod),
                EscapeCsv(item.Teyinat),
                EscapeCsv(item.Material),
                EscapeCsv(item.OlcuVahidi),
                item.Miqdar.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(item.Qeyd ?? string.Empty)));
        }

        writer.Flush();
        return Task.CompletedTask;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
