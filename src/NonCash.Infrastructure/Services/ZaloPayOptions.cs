namespace NonCash.Infrastructure.Services;

public class ZaloPayOptions
{
    public string Endpoint { get; set; } = "https://sb-openapi.zalopay.vn/v2/create";
    public int AppId { get; set; }
    public string Key1 { get; set; } = string.Empty;
    public string Key2 { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string AppUserPrefix { get; set; } = "noncash";
}
