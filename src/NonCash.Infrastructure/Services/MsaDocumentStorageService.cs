using Microsoft.Extensions.Logging;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// IDocumentStorageService implementation backed by MSA (Media Storage Agency).
/// Performs delete-before-upload to avoid orphaned files on MSA.
/// Returns only RelativeUrl (no domain) — DB stores this value as-is.
/// Full display URL is composed at presentation layer: {CDNEndpointURL}/{RelativeUrl}.
/// </summary>
public class MsaDocumentStorageService : IDocumentStorageService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const string MediaType = "documents";

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private readonly MsaMediaClient _msaClient;
    private readonly ILogger<MsaDocumentStorageService> _logger;

    public MsaDocumentStorageService(MsaMediaClient msaClient, ILogger<MsaDocumentStorageService> logger)
    {
        _msaClient = msaClient;
        _logger = logger;
    }

    public async Task<DocumentStorageResult> StoreAsync(
        Stream stream,
        string fileName,
        string contentType,
        string entity,
        string uniqueCode,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            return new DocumentStorageResult(false, Error: $"Invalid document format '{contentType}'. Allowed: pdf, jpg, png.");
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return new DocumentStorageResult(false, Error: $"Invalid file extension '{ext}'. Allowed: .pdf, .jpg, .jpeg, .png.");
        }

        if (stream.Length > MaxFileSizeBytes)
        {
            return new DocumentStorageResult(false, Error: $"File too large ({stream.Length / 1024 / 1024} MB). Maximum is 10 MB.");
        }

        _logger.LogDebug("MSA: Deleting previous media for entity={Entity}, uniqueCode={UniqueCode}", entity, uniqueCode);
        var deleteResult = await _msaClient.DeleteAsync(entity, uniqueCode, MediaType);
        if (!deleteResult)
        {
            _logger.LogWarning("MSA: Delete call returned non-success for entity={Entity}, uniqueCode={UniqueCode}. Continuing with upload.", entity, uniqueCode);
        }

        _logger.LogDebug("MSA: Uploading document for entity={Entity}, uniqueCode={UniqueCode}, fileName={FileName}", entity, uniqueCode, fileName);
        var uploadResult = await _msaClient.UploadAsync(entity, uniqueCode, MediaType, stream, fileName);

        if (!uploadResult.IsSuccess)
        {
            _logger.LogError("MSA document upload failed: {Message}", uploadResult.Message);
            return new DocumentStorageResult(false, Error: uploadResult.Message);
        }

        _logger.LogInformation("MSA document upload success. RelativeUrl={RelativeUrl}, Size={FileSize}", uploadResult.RelativeUrl, uploadResult.FileSize);

        return new DocumentStorageResult(true, Url: uploadResult.RelativeUrl);
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
