using Microsoft.Extensions.Logging;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// IImageStorageService implementation backed by MSA (Media Storage Agency).
/// Performs delete-before-upload to avoid orphaned files on MSA.
/// Returns only RelativeUrl (no domain) — DB stores this value as-is.
/// Full display URL is composed at presentation layer: {CDNEndpointURL}/{RelativeUrl}.
/// </summary>
public class MsaImageStorageService : IImageStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const string MediaType = "images";

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly MsaMediaClient _msaClient;
    private readonly ILogger<MsaImageStorageService> _logger;

    public MsaImageStorageService(MsaMediaClient msaClient, ILogger<MsaImageStorageService> logger)
    {
        _msaClient = msaClient;
        _logger = logger;
    }

    public async Task<ImageStorageResult> StoreAsync(
        Stream stream,
        string fileName,
        string contentType,
        string entity,
        string uniqueCode,
        CancellationToken cancellationToken = default)
    {
        // Validate content type
        if (!AllowedContentTypes.Contains(contentType))
        {
            return new ImageStorageResult(false, Error: $"Invalid image format '{contentType}'. Allowed: jpg, png, webp, gif.");
        }

        // Validate extension
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return new ImageStorageResult(false, Error: $"Invalid file extension '{ext}'. Allowed: .jpg, .jpeg, .png, .webp, .gif.");
        }

        // Validate size
        if (stream.Length > MaxFileSizeBytes)
        {
            return new ImageStorageResult(false, Error: $"File too large ({stream.Length / 1024 / 1024} MB). Maximum is 5 MB.");
        }

        // Step 1: Delete previous files for this entity+uniqueCode (avoids orphaned files on MSA)
        _logger.LogDebug("MSA: Deleting previous media for entity={Entity}, uniqueCode={UniqueCode}", entity, uniqueCode);
        var deleteResult = await _msaClient.DeleteAsync(entity, uniqueCode, MediaType);
        if (!deleteResult)
        {
            _logger.LogWarning("MSA: Delete call returned non-success for entity={Entity}, uniqueCode={UniqueCode}. Continuing with upload.", entity, uniqueCode);
        }

        // Step 2: Upload new file to MSA
        _logger.LogDebug("MSA: Uploading file for entity={Entity}, uniqueCode={UniqueCode}, fileName={FileName}", entity, uniqueCode, fileName);
        var uploadResult = await _msaClient.UploadAsync(entity, uniqueCode, MediaType, stream, fileName);

        if (!uploadResult.IsSuccess)
        {
            _logger.LogError("MSA upload failed: {Message}", uploadResult.Message);
            return new ImageStorageResult(false, Error: uploadResult.Message);
        }

        _logger.LogInformation("MSA upload success. RelativeUrl={RelativeUrl}, Size={FileSize}", uploadResult.RelativeUrl, uploadResult.FileSize);

        // Return only the RelativeUrl — DB stores this, domain is composed at runtime
        return new ImageStorageResult(true, Url: uploadResult.RelativeUrl);
    }

    public async Task DeleteAsync(string entity, string uniqueCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(uniqueCode))
            return;

        _logger.LogDebug("MSA: Deleting media for entity={Entity}, uniqueCode={UniqueCode}", entity, uniqueCode);
        var result = await _msaClient.DeleteAsync(entity, uniqueCode, MediaType);
        if (!result)
        {
            _logger.LogWarning("MSA: Delete returned non-success for entity={Entity}, uniqueCode={UniqueCode}", entity, uniqueCode);
        }
    }
}
