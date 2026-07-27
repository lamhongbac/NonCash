using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Stores uploaded images to local wwwroot/uploads/ directory.
/// Used as a development fallback when MSA media service is unavailable.
/// Validates format (jpg, png, webp, gif) and max size (5 MB).
/// Returns a relative URL like /uploads/{entity}/{uniqueCode}.ext.
/// </summary>
public class LocalStorageImageService : IImageStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

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

    private readonly string _webRootPath;

    public LocalStorageImageService(string webRootPath)
    {
        _webRootPath = webRootPath;
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

        // Delete previous files for this entity+uniqueCode (mimics MSA delete-before-upload)
        await DeleteAsync(entity, uniqueCode, cancellationToken);

        // Ensure upload directory exists: wwwroot/uploads/{entity}/
        var entityDir = Path.Combine(_webRootPath, "uploads", entity.ToLowerInvariant());
        if (!Directory.Exists(entityDir))
        {
            Directory.CreateDirectory(entityDir);
        }

        // Sanitize uniqueCode for filesystem use
        var safeCode = SanitizeFileName(uniqueCode);
        var storedFileName = $"{safeCode}{ext.ToLowerInvariant()}";
        var filePath = Path.Combine(entityDir, storedFileName);

        // Write file
        await using var fs = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fs, cancellationToken);

        var relativeUrl = $"/uploads/{entity.ToLowerInvariant()}/{storedFileName}";
        return new ImageStorageResult(true, Url: relativeUrl);
    }

    public Task DeleteAsync(string entity, string uniqueCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(uniqueCode))
            return Task.CompletedTask;

        var entityDir = Path.Combine(_webRootPath, "uploads", entity.ToLowerInvariant());
        if (!Directory.Exists(entityDir))
            return Task.CompletedTask;

        var safeCode = SanitizeFileName(uniqueCode);

        // Delete any file starting with the safeCode (covers all extensions)
        foreach (var file in Directory.GetFiles(entityDir, $"{safeCode}.*"))
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string input)
    {
        // Replace characters that are invalid in file names
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(input.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return result;
    }
}
