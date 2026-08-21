using System.Text;
using NonCash.Core.Configuration;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class ContractService : IContractService
{
    private readonly IContractTemplateService _templateService;
    private readonly ISubscriptionFeePolicyService _subscriptionFeePolicyService;

    public ContractService(IContractTemplateService templateService, ISubscriptionFeePolicyService subscriptionFeePolicyService)
    {
        _templateService = templateService;
        _subscriptionFeePolicyService = subscriptionFeePolicyService;
    }

    public async Task<string> GenerateContractHtmlAsync(
        ContractData data,
        CreditConfig platformTerms,
        Guid? templateId = null,
        CancellationToken cancellationToken = default)
    {
        var templateHtml = templateId.HasValue
            ? (await _templateService.GetTemplateAsync(templateId.Value, cancellationToken))?.HtmlTemplate
            : (await _templateService.GetDefaultTemplateAsync(cancellationToken))?.HtmlTemplate;

        templateHtml ??= GetFallbackTemplateHtml();

        var today = DateTime.UtcNow;
        var todayText = today.ToString("yyyy-MM-dd");
        var expiryText = data.WelcomeCreditExpiryMonths.HasValue
            ? $"{data.WelcomeCreditExpiryMonths.Value} month(s) from the activation date"
            : "no expiry";

        var subscriptionPolicy = await _subscriptionFeePolicyService.GetEffectivePolicyAsync(today, cancellationToken);
        var subscriptionFeeVnd = subscriptionPolicy?.IsFree == false ? subscriptionPolicy.AmountVnd : 0m;
        var minimumCommitmentMonths = subscriptionPolicy?.MinimumCommitmentMonths ?? platformTerms.MinimumCommitmentMonths;

        var rendered = templateHtml
            .Replace("{{BusinessName}}", HtmlEncode(data.BusinessName))
            .Replace("{{BrandName}}", HtmlEncode(data.BrandName))
            .Replace("{{TaxCode}}", HtmlEncode(data.TaxCode))
            .Replace("{{RepresentativeName}}", HtmlEncode(data.RepresentativeName ?? string.Empty))
            .Replace("{{PolicyTemplateName}}", HtmlEncode(data.PolicyTemplateName))
            .Replace("{{WelcomeCredits}}", data.WelcomeCredits.ToString("N0"))
            .Replace("{{WelcomeCreditExpiryMonths}}", data.WelcomeCreditExpiryMonths?.ToString() ?? "")
            .Replace("{{WelcomeCreditExpiryText}}", HtmlEncode(expiryText))
            .Replace("{{SubscriptionFeeVnd}}", subscriptionFeeVnd.ToString("N0"))
            .Replace("{{MinimumCommitmentMonths}}", minimumCommitmentMonths.ToString())
            .Replace("{{PricePerCreditVnd}}", platformTerms.PricePerCreditVnd.ToString("N0"))
            .Replace("{{Today}}", todayText);

        return WrapInDocument(rendered, todayText);
    }

    private static string WrapInDocument(string bodyHtml, string today)
    {
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
        sb.AppendLine(bodyHtml);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    /// Default contract body used when no contract template has been created in the database yet.
    /// Admins can later edit this content via the Contract Templates admin page.
    /// </summary>
    private static string GetFallbackTemplateHtml()
    {
        return """
            <div class='section'>
                <h2>1. Business Information</h2>
                <p><strong>Business Name:</strong> {{BusinessName}}</p>
                <p><strong>Brand Name:</strong> {{BrandName}}</p>
                <p><strong>Tax Code:</strong> {{TaxCode}}</p>
                <p><strong>Representative:</strong> {{RepresentativeName}}</p>
            </div>

            <div class='section'>
                <h2>2. Welcome Policy</h2>
                <p><strong>Policy:</strong> {{PolicyTemplateName}}</p>
                <p><strong>Welcome Credits:</strong> {{WelcomeCredits}}</p>
                <p><strong>Credit Expiry:</strong> {{WelcomeCreditExpiryText}}</p>
            </div>

            <div class='section'>
                <h2>3. Pricing</h2>
                <p>Credit pricing is set at the Brand level. Each Brand operated under this Business may have its own unit price, credit expiry, and related commercial terms. The applicable pricing for each Brand is listed in <strong>Appendix A — Brand Pricing</strong>, which forms part of this agreement.</p>
            </div>

            <div class='section'>
                <h2>4. Subscription Fee</h2>
                <p>During the MVP period, the Platform Subscription Fee is waived (0 VND). After the MVP period, the Business shall pay a Platform Subscription Fee of <strong>{{SubscriptionFeeVnd}} VND</strong> per <strong>{{MinimumCommitmentMonths}}-month</strong> term, unless otherwise agreed in writing by both parties.</p>
            </div>

            <div class='section'>
                <h2>5. Terms and Conditions</h2>
                <p>The business agrees to use the NonCash platform in accordance with the platform terms and conditions. Settlement, redemption, and credit rules are governed by the platform policies published at the time of use.</p>
            </div>

            <div class='section'>
                <h2>Appendix A — Brand Pricing</h2>
                <p><strong>Brand:</strong> {{BrandName}}</p>
                <p>Pricing details for this Brand will be provided in a separate pricing appendix agreed by both parties.</p>
            </div>

            <div class='signature'>
                <div class='signature-box'>
                    <strong>Platform Representative</strong><br/>NonCash Platform
                </div>
                <div class='signature-box'>
                    <strong>Business Representative</strong><br/>{{RepresentativeName}}
                </div>
            </div>
            """;
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
