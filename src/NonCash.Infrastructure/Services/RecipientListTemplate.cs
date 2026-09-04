using ClosedXML.Excel;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Builds the downloadable Excel template for the batch promotion recipient
/// list, so users always start from the correct single-column layout.
/// </summary>
public static class RecipientListTemplate
{
    public static byte[] BuildXlsx()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Recipients");

        // One recipient per row - phone number or email of an existing customer.
        sheet.Cell(1, 1).Value = "Phone / Email";
        sheet.Row(1).Style.Font.Bold = true;

        sheet.Cell(2, 1).Value = "0912345678";
        sheet.Cell(3, 1).Value = "jane@example.com";

        // Keep phones as text so leading zeros survive Excel editing.
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(1).Width = 30;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
