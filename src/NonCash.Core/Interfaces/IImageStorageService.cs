namespace NonCash.Core.Interfaces;

/// <summary>
/// Abstraction for storing uploaded images (cover images, icons, etc.).
/// Returns the RelativeUrl from the backing media service (MSA or local).
/// Database stores RelativeUrl only; full CDN URL is composed at presentation layer.
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Stores an image file and returns the RelativeUrl.
    /// Validates format (jpg, png, webp) and max size (5 MB).
    /// For MSA: performs delete-before-upload automatically to avoid orphaned files.
    /// </summary>
    /// <param name="stream">The file stream.</param>
    /// <param name="fileName">Original file name (used for extension validation).</param>
    /// <param name="contentType">MIME type of the file.</param>
    /// <param name="entity">Business entity name (e.g., "voucher_plan_headers"). Used by MSA to organize storage.</param>
    /// <param name="uniqueCode">Unique record identifier including field name (e.g., "{planId}_cover_image"). Used by MSA for deduplication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ImageStorageResult> StoreAsync(
        Stream stream,
        string fileName,
        string contentType,
        string entity,
        string uniqueCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes media files associated with the given entity + uniqueCode.
    /// </summary>
    Task DeleteAsync(string entity, string uniqueCode, CancellationToken cancellationToken = default);
}

public record ImageStorageResult(bool Success, string? Url = null, string? Error = null);
