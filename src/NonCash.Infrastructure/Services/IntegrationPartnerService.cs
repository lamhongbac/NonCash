using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// EF-backed implementation of IIntegrationPartnerService.
/// Uses BCrypt for API key hashing and generates random 32-byte hex API keys.
/// </summary>
public class IntegrationPartnerService : IIntegrationPartnerService
{
    private readonly ApplicationDbContext _context;

    public IntegrationPartnerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IntegrationPartner> CreateAsync(
        string name, string contactEmail, string callbackUrl,
        List<Guid> brandIds, CancellationToken cancellationToken = default)
    {
        // Generate initial API key
        var (apiKey, prefix, hash) = GenerateKeyMaterial();
        var webhookSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var partner = new IntegrationPartner
        {
            Name = name,
            ContactEmail = contactEmail,
            CallbackUrl = callbackUrl,
            ApiKeyPrefix = prefix,
            ApiKeyHash = hash,
            WebhookSecret = webhookSecret,
            IsActive = true
        };

        _context.Set<IntegrationPartner>().Add(partner);
        await _context.SaveChangesAsync(cancellationToken);

        // Add brand associations
        foreach (var brandId in brandIds)
        {
            _context.Set<PartnerBrand>().Add(new PartnerBrand
            {
                PartnerId = partner.Id,
                BrandId = brandId
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        return partner;
    }

    public async Task<IntegrationPartner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<IntegrationPartner>()
            .Include(p => p.PartnerBrands)
            .ThenInclude(pb => pb.Brand)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationPartner>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<IntegrationPartner>()
            .Include(p => p.PartnerBrands)
            .ThenInclude(pb => pb.Brand)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IntegrationPartner?> UpdateAsync(
        Guid id, string name, string contactEmail, string callbackUrl, bool isActive,
        CancellationToken cancellationToken = default)
    {
        var partner = await _context.Set<IntegrationPartner>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (partner == null) return null;

        partner.Name = name;
        partner.ContactEmail = contactEmail;
        partner.CallbackUrl = callbackUrl;
        partner.IsActive = isActive;

        await _context.SaveChangesAsync(cancellationToken);
        return partner;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await _context.Set<IntegrationPartner>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (partner == null) return false;

        _context.Set<IntegrationPartner>().Remove(partner);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(string ApiKey, string Prefix)> GenerateApiKeyAsync(
        Guid partnerId, CancellationToken cancellationToken = default)
    {
        var partner = await _context.Set<IntegrationPartner>()
            .FirstOrDefaultAsync(p => p.Id == partnerId, cancellationToken);

        if (partner == null)
            throw new InvalidOperationException($"Partner {partnerId} not found.");

        var (apiKey, prefix, hash) = GenerateKeyMaterial();
        partner.ApiKeyPrefix = prefix;
        partner.ApiKeyHash = hash;
        await _context.SaveChangesAsync(cancellationToken);

        return (apiKey, prefix);
    }

    public async Task<(IntegrationPartner? Partner, List<Guid> BrandIds)> ValidateApiKeyAsync(
        string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (null, new List<Guid>());

        var prefix = apiKey.Length >= 8 ? apiKey[..8] : apiKey;

        var partner = await _context.Set<IntegrationPartner>()
            .Include(p => p.PartnerBrands)
            .FirstOrDefaultAsync(p => p.ApiKeyPrefix == prefix && p.IsActive, cancellationToken);

        if (partner == null)
            return (null, new List<Guid>());

        if (!BCrypt.Net.BCrypt.Verify(apiKey, partner.ApiKeyHash))
            return (null, new List<Guid>());

        var brandIds = partner.PartnerBrands.Select(pb => pb.BrandId).ToList();
        return (partner, brandIds);
    }

    public async Task SetPartnerBrandsAsync(
        Guid partnerId, List<Guid> brandIds, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Set<PartnerBrand>()
            .Where(pb => pb.PartnerId == partnerId)
            .ToListAsync(cancellationToken);

        _context.Set<PartnerBrand>().RemoveRange(existing);

        foreach (var brandId in brandIds)
        {
            _context.Set<PartnerBrand>().Add(new PartnerBrand
            {
                PartnerId = partnerId,
                BrandId = brandId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (string apiKey, string prefix, string hash) GenerateKeyMaterial()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var apiKey = Convert.ToHexString(keyBytes).ToLowerInvariant();
        var prefix = apiKey[..8];
        var hash = BCrypt.Net.BCrypt.HashPassword(apiKey);
        return (apiKey, prefix, hash);
    }
}
