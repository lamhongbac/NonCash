using Npgsql;

var connStr = "Host=45.119.87.247;Database=noncash;Username=noncash_app;Password=NonCashMachine@2026;SSL Mode=Require";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

async Task RunQuery(string title, string sql)
{
    Console.WriteLine($"=== {title} ===");
    try
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var cols = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));
        Console.WriteLine(string.Join(" | ", cols));
        var rows = 0;
        while (await reader.ReadAsync())
        {
            var vals = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var v = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                vals.Add(v);
            }
            Console.WriteLine(string.Join(" | ", vals));
            rows++;
        }
        await reader.DisposeAsync();
        Console.WriteLine($"({rows} row(s))");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
    Console.WriteLine();
}

await RunQuery("latest brand_registration_requests",
    "SELECT id, status, submitted_at, brand_id, submitted_by_user_id FROM brand_registration_requests ORDER BY submitted_at DESC LIMIT 10");

await RunQuery("brands with lamhong.bac email",
    "SELECT id, name, tax_code, contact_email, status, created_at FROM brands WHERE contact_email ILIKE '%lamhong.bac%' ORDER BY created_at DESC");

await RunQuery("businesses with lamhong.bac email",
    "SELECT id, business_name, tax_code, contact_email, is_active, created_at FROM businesses WHERE contact_email ILIKE '%lamhong.bac%' ORDER BY created_at DESC");

await RunQuery("user_accounts with lamhong.bac email",
    "SELECT id, username, email, status, role, brand_id, created_at FROM user_accounts WHERE email ILIKE '%lamhong.bac%' ORDER BY created_at DESC");

await RunQuery("latest email_logs",
    "SELECT sent_at, to_address, template_name, success, error_message FROM email_logs ORDER BY sent_at DESC LIMIT 10");

await RunQuery("latest brands (any)",
    "SELECT id, name, contact_email, status, created_at FROM brands ORDER BY created_at DESC LIMIT 5");

await RunQuery("latest businesses (any)",
    "SELECT id, business_name, contact_email, is_active, created_at FROM businesses ORDER BY created_at DESC LIMIT 5");
