using System.Text;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class ContractService : IContractService
{
    public Task<string> GenerateContractHtmlAsync(
        string businessName,
        string brandName,
        string taxCode,
        string? representativeName,
        string policyTemplateName,
        int welcomeCredits,
        int? welcomeCreditExpiryMonths,
        CancellationToken cancellationToken = default)
    {
        var expiryText = welcomeCreditExpiryMonths.HasValue
            ? $"{welcomeCreditExpiryMonths.Value} month(s) from the activation date"
            : "no expiry";

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8' />");
        sb.AppendLine("<title>NonCash Platform Agreement</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; max-width: 800px; margin: 40px auto; padding: 20px; }");
        sb.AppendLine("h1 { text-align: center; }");
        sb.AppendLine(".section { margin-bottom: 20px; }");
        sb.AppendLine(".signature { margin-top: 60px; display: flex; justify-content: space-between; }");
        sb.AppendLine(".signature-box { width: 45%; border-top: 1px solid #000; padding-top: 8px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>NONCASH PLATFORM AGREEMENT</h1>");
        sb.AppendLine($"<p><strong>Date:</strong> {today}</p>");

        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h2>1. Business Information</h2>");
        sb.AppendLine($"<p><strong>Business Name:</strong> {HtmlEncode(businessName)}</p>");
        sb.AppendLine($"<p><strong>Brand Name:</strong> {HtmlEncode(brandName)}</p>");
        sb.AppendLine($"<p><strong>Tax Code:</strong> {HtmlEncode(taxCode)}</p>");
        if (!string.IsNullOrWhiteSpace(representativeName))
            sb.AppendLine($"<p><strong>Representative:</strong> {HtmlEncode(representativeName)}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h2>2. Welcome Policy</h2>");
        sb.AppendLine($"<p><strong>Policy:</strong> {HtmlEncode(policyTemplateName)}</p>");
        sb.AppendLine($"<p><strong>Welcome Credits:</strong> {welcomeCredits:N0}</p>");
        sb.AppendLine($"<p><strong>Credit Expiry:</strong> {expiryText}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h2>3. Pricing</h2>");
        sb.AppendLine("<p>Credit pricing is set at the Brand level. Each Brand operated under this Business may have its own unit price, credit expiry, and related commercial terms. The applicable pricing for each Brand is listed in <strong>Appendix A — Brand Pricing</strong>, which forms part of this agreement.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h2>4. Terms and Conditions</h2>");
        sb.AppendLine("<p>The business agrees to use the NonCash platform in accordance with the platform terms and conditions. Settlement, redemption, and credit rules are governed by the platform policies published at the time of use.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h2>Appendix A — Brand Pricing</h2>");
        sb.AppendLine($"<p><strong>Brand:</strong> {HtmlEncode(brandName)}</p>");
        sb.AppendLine("<p>Pricing details for this Brand will be provided in a separate pricing appendix agreed by both parties.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='signature'>");
        sb.AppendLine("<div class='signature-box'>");
        sb.AppendLine("<strong>Platform Representative</strong><br/>NonCash Platform");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='signature-box'>");
        sb.AppendLine($"<strong>Business Representative</strong><br/>{HtmlEncode(representativeName ?? businessName)}");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return Task.FromResult(sb.ToString());
    }

    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
