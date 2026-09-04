using NSubstitute;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Services;

var repo = Substitute.For<ICustomerRepository>();
var service = new CsvCustomerImportService(new CustomerService(repo));

foreach (var path in args)
{
    using var stream = File.OpenRead(path);
    try
    {
        var result = await service.ImportAsync(stream);
        Console.WriteLine($"{Path.GetFileName(path)}: created={result.Created} updated={result.Updated} errors={result.Errors.Count}");
        foreach (var e in result.Errors.Take(5))
            Console.WriteLine($"  row {e.Row}: {e.Message} [{e.PhoneNumber} / {e.FullName}]");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{Path.GetFileName(path)}: EXCEPTION -> {ex.Message}");
    }
}
