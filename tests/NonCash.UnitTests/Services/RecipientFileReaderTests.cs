using FluentAssertions;
using NonCash.Core.Services;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

public class RecipientFileReaderTests
{
    private static Stream Csv(string content) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task TabSeparated_WithHeader_SkipsHeaderAndReadsFirstColumn()
    {
        var recipients = await RecipientFileReader.ReadAsync(Csv("Phone\tFullName\n0912345678\tTran My\n0909736099\tPham Dung\n"));

        recipients.Should().BeEquivalentTo("0912345678", "0909736099");
    }

    [Fact]
    public async Task HeaderlessText_ReadsEveryRow()
    {
        var recipients = await RecipientFileReader.ReadAsync(Csv("0912345678\njane@example.com\n"));

        recipients.Should().BeEquivalentTo("0912345678", "jane@example.com");
    }

    [Fact]
    public async Task ExcelTemplate_RoundTrips()
    {
        var template = RecipientListTemplate.BuildXlsx();

        var recipients = await RecipientFileReader.ReadAsync(new MemoryStream(template));

        recipients.Should().BeEquivalentTo("0912345678", "jane@example.com");
    }

    [Fact]
    public async Task CorruptXlsx_ThrowsFriendlyParseException()
    {
        var corrupt = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8 };

        var act = () => RecipientFileReader.ReadAsync(new MemoryStream(corrupt));

        await act.Should().ThrowAsync<CustomerImportParseException>()
            .WithMessage("*Download template*");
    }
}
