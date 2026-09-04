using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.Infrastructure.Services;

public class CsvCustomerImportService : ICustomerImportService
{
    private const string TemplateHint =
        " Please use the 'Download Excel template' button in the Import dialog, fill in your data and upload the filled file.";

    // Accepted header spellings (compared trimmed + lower-cased).
    private static readonly string[] PhoneHeaders =
    {
        "phone", "phonenumber", "phone number", "mobile", "mobilenumber", "mobile number",
        "sdt", "sđt", "so dien thoai", "số điện thoại", "dien thoai", "điện thoại"
    };

    private static readonly string[] NameHeaders =
    {
        "fullname", "full name", "name", "customername", "customer name", "customer",
        "hoten", "họ tên", "ho va ten", "họ và tên", "ten khach hang", "tên khách hàng", "ten", "tên"
    };

    private static readonly string[] EmailHeaders =
    {
        "email", "e-mail", "emailaddress", "email address", "thu dien tu", "thư điện tử"
    };

    private readonly CustomerService _customerService;

    public CsvCustomerImportService(CustomerService customerService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
    }

    public async Task<CustomerImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default, Guid? brandId = null, Guid? createdBy = null)
    {
        // Buffer once so we can sniff the format and re-read freely.
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var records = IsXlsx(buffer) ? ReadXlsx(buffer) : ReadCsv(buffer);
        return await _customerService.UpsertAsync(records, cancellationToken, brandId, createdBy);
    }

    // ---------- format sniffing ----------

    private static bool IsXlsx(Stream stream)
    {
        if (stream.Length < 4) return false;
        var position = stream.Position;
        Span<byte> magic = stackalloc byte[4];
        stream.ReadExactly(magic);
        stream.Position = position;
        return magic[0] == 0x50 && magic[1] == 0x4B && magic[2] == 0x03 && magic[3] == 0x04; // ZIP container (xlsx)
    }

    // ---------- CSV path ----------

    private static List<CustomerImportRecord> ReadCsv(Stream stream)
    {
        using var peekReader = new StreamReader(stream, leaveOpen: true);
        var firstLine = peekReader.ReadLine();
        if (string.IsNullOrWhiteSpace(firstLine))
            return new List<CustomerImportRecord>();

        var delimiter = DetectDelimiter(firstLine);
        var hasHeader = LooksLikeHeader(firstLine, delimiter);

        stream.Position = 0;
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = hasHeader,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null
        };

        try
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, config);

            var phoneCol = 0;
            var nameCol = 1;
            var emailCol = 2;

            if (hasHeader)
            {
                csv.Read();
                csv.ReadHeader();
                if (csv.HeaderRecord != null)
                    TryMapHeader(csv.HeaderRecord, out phoneCol, out nameCol, out emailCol);
            }

            var records = new List<CustomerImportRecord>();
            while (csv.Read())
            {
                var row = csv.Parser.Row; // physical row in the file (header = row 1)
                var record = new ParsedRow
                {
                    PhoneNumber = Clean(csv.GetField(phoneCol)),
                    FullName = Clean(csv.GetField(nameCol)),
                    Email = Clean(csv.GetField(emailCol))
                };

                if (record.IsBlank) continue; // skip blank lines silently
                records.Add(new CustomerImportRecord(record.PhoneNumber, record.FullName, record.Email, row));
            }

            return records;
        }
        catch (Exception ex) when (ex is not CustomerImportParseException)
        {
            throw new CustomerImportParseException(
                "The file could not be read as a customer list." + TemplateHint, ex);
        }
    }

    private static string DetectDelimiter(string line)
    {
        var tabs = line.Count(c => c == '\t');
        var commas = line.Count(c => c == ',');
        var semis = line.Count(c => c == ';');

        if (tabs > 0 && tabs >= commas && tabs >= semis) return "\t";
        if (semis > commas) return ";";
        return ",";
    }

    private static bool LooksLikeHeader(string line, string delimiter)
    {
        var cells = line.Split(delimiter);
        return cells.Any(c => IsHeaderCell(c));
    }

    private static bool IsHeaderCell(string cell)
    {
        var normalized = cell.Trim().Trim('"').ToLowerInvariant();
        return PhoneHeaders.Contains(normalized) || NameHeaders.Contains(normalized) || EmailHeaders.Contains(normalized);
    }

    // ---------- Excel path ----------

    private static List<CustomerImportRecord> ReadXlsx(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                throw new CustomerImportParseException("The Excel file contains no worksheets." + TemplateHint);

            var phoneCol = 1;
            var nameCol = 2;
            var emailCol = 3;
            var firstRowNumber = -1;

            var firstRow = worksheet.FirstRowUsed();
            if (firstRow != null)
            {
                var cells = Enumerable.Range(1, firstRow.LastCellUsed()?.Address.ColumnNumber ?? 3)
                    .Select(c => firstRow.Cell(c).GetString())
                    .ToList();

                if (TryMapHeader(cells, out phoneCol, out nameCol, out emailCol))
                {
                    // TryMapHeader returns 0-based positions; ClosedXML cells are 1-based.
                    phoneCol++;
                    nameCol++;
                    emailCol++;
                    firstRowNumber = firstRow.RowNumber();
                }
            }

            var records = new List<CustomerImportRecord>();
            foreach (var row in worksheet.RowsUsed())
            {
                if (row.RowNumber() == firstRowNumber) continue; // header row

                var record = new ParsedRow
                {
                    PhoneNumber = Clean(row.Cell(phoneCol).GetString()),
                    FullName = Clean(row.Cell(nameCol).GetString()),
                    Email = Clean(row.Cell(emailCol).GetString())
                };

                if (record.IsBlank) continue;
                records.Add(new CustomerImportRecord(record.PhoneNumber, record.FullName, record.Email, row.RowNumber()));
            }

            return records;
        }
        catch (Exception ex) when (ex is not CustomerImportParseException)
        {
            throw new CustomerImportParseException(
                "The Excel file could not be read." + TemplateHint, ex);
        }
    }

    // ---------- shared helpers ----------

    /// <summary>Maps a header row to column positions (0-based for CSV, converted by callers). Returns false when no known column name is present.</summary>
    private static bool TryMapHeader(IReadOnlyList<string?> cells, out int phoneCol, out int nameCol, out int emailCol)
    {
        phoneCol = 0;
        nameCol = 1;
        emailCol = 2;

        var found = false;
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = (cells[i] ?? string.Empty).Trim().Trim('"').ToLowerInvariant();
            if (PhoneHeaders.Contains(cell)) { phoneCol = i; found = true; }
            else if (NameHeaders.Contains(cell)) { nameCol = i; found = true; }
            else if (EmailHeaders.Contains(cell)) { emailCol = i; found = true; }
        }

        return found;
    }

    /// <summary>Trims a raw cell value and turns placeholder tokens (NULL, N/A, -) into empty.</summary>
    private static string Clean(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || trimmed == "-"
            ? string.Empty
            : trimmed;
    }

    private sealed class ParsedRow
    {
        public string PhoneNumber { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool IsBlank => string.IsNullOrEmpty(PhoneNumber) && string.IsNullOrEmpty(FullName) && string.IsNullOrEmpty(Email);
    }
}
