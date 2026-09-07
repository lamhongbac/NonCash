using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

public class CustomerImportServiceTests
{
    private readonly ICustomerRepository _customerRepository = Substitute.For<ICustomerRepository>();
    private readonly IBrandCustomerRepository _brandCustomerRepository = Substitute.For<IBrandCustomerRepository>();
    private readonly IRepository<CustomerAuditLog> _auditLogRepository = Substitute.For<IRepository<CustomerAuditLog>>();
    private readonly List<Customer> _added = new();
    private readonly CsvCustomerImportService _sut;

    public CustomerImportServiceTests()
    {
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var customer = ci.Arg<Customer>();
                _added.Add(customer);
                return customer;
            });

        _sut = new CsvCustomerImportService(new CustomerService(_customerRepository, _brandCustomerRepository, _auditLogRepository));
    }

    private static Stream Csv(string content) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task TabSeparated_WithStandardHeader_ImportsAllRows()
    {
        // Regression: Excel "Text (Tab delimited)" exports used to fail with a raw parse exception.
        var result = await _sut.ImportAsync(Csv(
            "Phone\tFullName\tEmail\n0937551218\tTran My\tmy.hatec@gmail.com\n0909736099\tPham Anh Dung\tNULL\n"));

        result.Errors.Should().BeEmpty();
        result.Created.Should().Be(2);
        _added[0].PhoneNumber.Should().Be("0937551218");
        _added[1].Email.Should().BeNull(); // literal NULL treated as "no email"
    }

    [Fact]
    public async Task MobileHeaderAlias_IsRecognised()
    {
        // Regression: files with a "Mobile" header used to fail every row.
        var result = await _sut.ImportAsync(Csv("Mobile,FullName,Email\n0937551218,Tran My,my.hatec@gmail.com\n"));

        result.Errors.Should().BeEmpty();
        result.Created.Should().Be(1);
        _added[0].PhoneNumber.Should().Be("0937551218");
        _added[0].FullName.Should().Be("Tran My");
    }

    [Fact]
    public async Task HeaderlessFile_UsesPositionalColumns()
    {
        var result = await _sut.ImportAsync(Csv("0937551218,Tran My,my.hatec@gmail.com\n"));

        result.Errors.Should().BeEmpty();
        result.Created.Should().Be(1);
        _added[0].FullName.Should().Be("Tran My");
        _added[0].Email.Should().Be("my.hatec@gmail.com");
    }

    [Fact]
    public async Task ExcelTemplate_RoundTrips_AndKeepsLeadingZeros()
    {
        var template = CustomerImportTemplate.BuildXlsx();

        var result = await _sut.ImportAsync(new MemoryStream(template));

        result.Errors.Should().BeEmpty();
        result.Created.Should().Be(2);
        _added[0].PhoneNumber.Should().Be("0901234567");
        _added[0].Email.Should().Be("nguyenvana@example.com");
        _added[1].Email.Should().BeNull();
    }

    [Fact]
    public async Task CorruptXlsx_ThrowsFriendlyParseException()
    {
        var corrupt = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8 };

        var act = () => _sut.ImportAsync(new MemoryStream(corrupt));

        await act.Should().ThrowAsync<CustomerImportParseException>()
            .WithMessage("*Download Excel template*");
    }
}
