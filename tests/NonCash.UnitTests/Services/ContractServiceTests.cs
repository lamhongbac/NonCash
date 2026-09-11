using FluentAssertions;
using NSubstitute;
using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// CR-2026-09-07-15. The contract is the document a business signs, so the customer-data ownership
/// clause is a legal commitment rather than decoration: it has to reach the generated HTML. The dev
/// database has no row in contract_templates, which means the code-resident fallback is what
/// actually renders — so that fallback is what these tests pin down.
/// </summary>
public class ContractServiceTests
{
    private readonly IContractTemplateService _templateService = Substitute.For<IContractTemplateService>();
    private readonly ISubscriptionFeePolicyService _subscriptionPolicyService = Substitute.For<ISubscriptionFeePolicyService>();
    private readonly ContractService _sut;

    public ContractServiceTests()
    {
        // No template on file and no policy in force: the fallback body and the config defaults render.
        _templateService.GetDefaultTemplateAsync(Arg.Any<CancellationToken>())
            .Returns((ContractTemplate?)null);
        _templateService.GetTemplateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ContractTemplate?)null);
        _subscriptionPolicyService.GetEffectivePolicyAsync(Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((SubscriptionFeePolicy?)null);

        _sut = new ContractService(_templateService, _subscriptionPolicyService);
    }

    private static ContractData SampleData() => new(
        "Cong ty TNHH Test",
        "Test Brand",
        "0312345678",
        "Nguyen Van A",
        "Welcome 500",
        500,
        12);

    [Fact]
    public async Task GenerateContractHtml_WithNoTemplateOnFile_SubstitutesTheRegistrationValues()
    {
        var html = await _sut.GenerateContractHtmlAsync(SampleData(), new CreditConfig());

        html.Should().Contain("NONCASH PLATFORM AGREEMENT");
        html.Should().Contain("Cong ty TNHH Test");
        html.Should().Contain("Test Brand");
        html.Should().Contain("0312345678");
        html.Should().Contain("Nguyen Van A");
        html.Should().NotContain("{{");
    }

    [Fact]
    public async Task GenerateContractHtml_IncludesTheThreeLayerCustomerDataOwnershipClause()
    {
        var html = await _sut.GenerateContractHtmlAsync(SampleData(), new CreditConfig());

        html.Should().Contain("6. Customer Data Ownership");
        html.Should().Contain("6.1 Identity record");
        html.Should().Contain("6.2 Relationship data");
        html.Should().Contain("6.3 Platform data");
        html.Should().Contain("6.4 Import warranty");
        html.Should().Contain("6.5 Curation");
        html.Should().Contain("6.6 Exit");
    }

    [Fact]
    public async Task GenerateContractHtml_OwnershipClause_StatesTheGuaranteesTheProductActuallyEnforces()
    {
        var html = await _sut.GenerateContractHtmlAsync(SampleData(), new CreditConfig());

        // Fill-empty-only: the brand contributes to the identity record but cannot overwrite it.
        html.Should().Contain("has no right to overwrite");
        // No cross-brand sharing of relationship data.
        html.Should().Contain("never shown, sold or shared with any other Brand");
        // Curation is audited, and the identity record outlives offboarding.
        html.Should().Contain("audit trail");
        html.Should().Contain("remains on the Platform");
    }

    [Fact]
    public async Task GenerateContractHtml_PlacesTheOwnershipClauseBeforeTheSignatureBlock()
    {
        var html = await _sut.GenerateContractHtmlAsync(SampleData(), new CreditConfig());

        html.IndexOf("6. Customer Data Ownership", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Business Representative", StringComparison.Ordinal));
    }
}
