using ClosedXML.Excel;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Builds the downloadable Excel template for customer import,
/// so users always start from the correct column layout.
/// </summary>
public static class CustomerImportTemplate
{
    public static byte[] BuildXlsx()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Customers");

        // Header row - must stay in this order: Phone, FullName, Email.
        sheet.Cell(1, 1).Value = "Phone";
        sheet.Cell(1, 2).Value = "FullName";
        sheet.Cell(1, 3).Value = "Email";
        sheet.Row(1).Style.Font.Bold = true;

        // Sample rows showing the expected shape (Email is optional).
        sheet.Cell(2, 1).Value = "0901234567";
        sheet.Cell(2, 2).Value = "Nguyen Van A";
        sheet.Cell(2, 3).Value = "nguyenvana@example.com";

        sheet.Cell(3, 1).Value = "0912345678";
        sheet.Cell(3, 2).Value = "Tran Thi B";

        // Keep phones as text so leading zeros survive Excel editing.
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(1).Width = 18;
        sheet.Column(2).Width = 30;
        sheet.Column(3).Width = 35;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
