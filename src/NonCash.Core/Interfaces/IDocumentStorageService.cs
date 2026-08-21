namespace NonCash.Core.Interfaces;

/// <summary>
/// Abstraction for storing uploaded documents (signed contracts, PDFs, scanned images).
/// Returns the RelativeUrl from the backing media service (MSA or local).
/// Database stores RelativeUrl only; full CDN URL is composed at presentation layer.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Stores a document file and returns the RelativeUrl.
    /// Validates format (pdf, jpg, png) and max size (10 MB).
    /// For MSA: performs delete-before-upload automatically to avoid orphaned files.
    /// </summary>
    /// <param name="stream">The file stream.</param>
    /// <param name="fileName">Original file name (used for extension validation).</param>
    /// <param name="contentType">MIME type of the file.</param>
    /// <param name="entity">Business entity name (e.g., "signed_contracts"). Used by MSA to organize storage.</param>
    /// <param name="uniqueCode">Unique record identifier (e.g., "{requestId}"). Used for deduplication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DocumentStorageResult> StoreAsync(
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

public record DocumentStorageResult(bool Success, string? Url = null, string? Error = null);
