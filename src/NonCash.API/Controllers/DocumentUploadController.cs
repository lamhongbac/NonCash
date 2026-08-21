using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/upload")]
[Authorize]
public class DocumentUploadController : ControllerBase
{
    private readonly IDocumentStorageService _documentStorageService;

    public DocumentUploadController(IDocumentStorageService documentStorageService)
    {
        _documentStorageService = documentStorageService;
    }

    /// <summary>
    /// Uploads a document file (multipart form). Returns the RelativeUrl from the backing media service.
    /// Accepted formats: pdf, jpg, png. Max size: 10 MB.
    ///
    /// Form fields:
    /// - file: The document file.
    /// - entity: Business entity name (e.g., "signed_contracts"). Used by MSA to organize storage.
    /// - uniqueCode: Unique record identifier (e.g., "{requestId}"). Used for deduplication.
    ///
    /// The returned RelativeUrl is stored directly in the database.
    /// Full CDN URL is composed at display time: {CDNEndpointURL}/{RelativeUrl}.
    /// </summary>
    [HttpPost("document")]
    [RequestSizeLimit(12 * 1024 * 1024)] // 12 MB request limit (file + overhead)
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocument(
        IFormFile file,
        [FromForm] string entity,
        [FromForm] string uniqueCode,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new DocumentUploadResponse(false, Error: "No file uploaded."));
        }

        if (string.IsNullOrWhiteSpace(entity))
        {
            return BadRequest(new DocumentUploadResponse(false, Error: "The 'entity' field is required (e.g., 'signed_contracts')."));
        }

        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return BadRequest(new DocumentUploadResponse(false, Error: "The 'uniqueCode' field is required (e.g., '{requestId}')."));
        }

        await using var stream = file.OpenReadStream();
        var result = await _documentStorageService.StoreAsync(
            stream,
            file.FileName,
            file.ContentType,
            entity,
            uniqueCode,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new DocumentUploadResponse(false, Error: result.Error));
        }

        return Ok(new DocumentUploadResponse(true, Url: result.Url));
    }
}

public record DocumentUploadResponse(bool Success, string? Url = null, string? Error = null);
