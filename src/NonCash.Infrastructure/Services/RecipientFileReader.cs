using ClosedXML.Excel;
using NonCash.Core.Services;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Reads a recipient list (one phone number or email per row) from an uploaded
/// file. Accepts Excel (.xlsx) and plain text/CSV (comma, semicolon or tab
/// separated; header optional; the first column is used).
/// </summary>
public static class RecipientFileReader
{
    private static readonly string[] RecipientHeaders =
    {
        "phone", "phonenumber", "phone number", "mobile", "mobilenumber", "email", "emailaddress",
        "phone / email", "phone/email", "phone number / email", "recipient", "recipients",
        "sdt", "sđt", "so dien thoai", "số điện thoại"
    };

    public static async Task<List<string>> ReadAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        // Buffer once so we can sniff the format and re-read freely.
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return IsXlsx(buffer) ? ReadXlsx(buffer) : ReadText(buffer);
    }

    private static bool IsXlsx(Stream stream)
    {
        if (stream.Length < 4) return false;
        var position = stream.Position;
        Span<byte> magic = stackalloc byte[4];
        stream.ReadExactly(magic);
        stream.Position = position;
        return magic[0] == 0x50 && magic[1] == 0x4B && magic[2] == 0x03 && magic[3] == 0x04; // ZIP container (xlsx)
    }

    private static List<string> ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var recipients = new List<string>();
        var isFirstLine = true;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // Skip header row if it does not look like a recipient (has letters but no email @)
            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.Any(char.IsLetter) && !line.Contains('@'))
                    continue;
            }

            // Take the first column from CSV
            var firstCol = Clean(line.Split(',', '\t', ';')[0]);
            if (!string.IsNullOrWhiteSpace(firstCol))
                recipients.Add(firstCol);
        }

        return recipients;
    }

    private static List<string> ReadXlsx(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return new List<string>();

            var column = 1;
            var headerRowNumber = -1;

            var firstRow = worksheet.FirstRowUsed();
            if (firstRow != null)
            {
                var lastColumn = firstRow.LastCellUsed()?.Address.ColumnNumber ?? 1;
                for (var c = 1; c <= lastColumn; c++)
                {
                    var header = firstRow.Cell(c).GetString().Trim().Trim('"').ToLowerInvariant();
                    if (RecipientHeaders.Contains(header))
                    {
                        column = c;
                        headerRowNumber = firstRow.RowNumber();
                        break;
                    }
                }
            }

            var recipients = new List<string>();
            foreach (var row in worksheet.RowsUsed())
            {
                if (row.RowNumber() == headerRowNumber) continue;
                var value = Clean(row.Cell(column).GetString());
                if (!string.IsNullOrWhiteSpace(value))
                    recipients.Add(value);
            }

            return recipients;
        }
        catch (Exception ex) when (ex is not CustomerImportParseException)
        {
            throw new CustomerImportParseException(
                "The uploaded file could not be read as a recipient list. Please use the 'Download template' button, fill in your recipients and upload the filled file.",
                ex);
        }
    }

    private static string Clean(string? value)
    {
        var trimmed = value?.Trim().Trim('"') ?? string.Empty;
        return trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || trimmed == "-"
            ? string.Empty
            : trimmed;
    }
}
