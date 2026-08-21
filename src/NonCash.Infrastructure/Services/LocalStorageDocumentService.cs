using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Stores uploaded documents to local wwwroot/uploads/ directory.
/// Used as a development fallback when MSA media service is unavailable.
/// Validates format (pdf, jpg, png) and max size (10 MB).
/// Returns a relative URL like /uploads/{entity}/{uniqueCode}.ext.
/// </summary>
public class LocalStorageDocumentService : IDocumentStorageService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

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

    private readonly string _webRootPath;

    public LocalStorageDocumentService(string webRootPath)
    {
        _webRootPath = webRootPath;
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

        await DeleteAsync(entity, uniqueCode, cancellationToken);

        var entityDir = Path.Combine(_webRootPath, "uploads", entity.ToLowerInvariant());
        if (!Directory.Exists(entityDir))
        {
            Directory.CreateDirectory(entityDir);
        }

        var safeCode = SanitizeFileName(uniqueCode);
        var storedFileName = $"{safeCode}{ext.ToLowerInvariant()}";
        var filePath = Path.Combine(entityDir, storedFileName);

        await using var fs = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fs, cancellationToken);

        var relativeUrl = $"/uploads/{entity.ToLowerInvariant()}/{storedFileName}";
        return new DocumentStorageResult(true, Url: relativeUrl);
    }

    public Task DeleteAsync(string entity, string uniqueCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(uniqueCode))
            return Task.CompletedTask;

        var entityDir = Path.Combine(_webRootPath, "uploads", entity.ToLowerInvariant());
        if (!Directory.Exists(entityDir))
            return Task.CompletedTask;

        var safeCode = SanitizeFileName(uniqueCode);

        foreach (var file in Directory.GetFiles(entityDir, $"{safeCode}.*"))
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(input.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return result;
    }
}
