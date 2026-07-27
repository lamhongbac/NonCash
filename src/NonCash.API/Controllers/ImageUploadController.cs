using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/upload")]
[Authorize]
public class ImageUploadController : ControllerBase
{
    private readonly IImageStorageService _imageStorageService;

    public ImageUploadController(IImageStorageService imageStorageService)
    {
        _imageStorageService = imageStorageService;
    }

    /// <summary>
    /// Uploads an image file (multipart form). Returns the RelativeUrl from the backing media service.
    /// Accepted formats: jpg, png, webp, gif. Max size: 5 MB.
    /// 
    /// Form fields:
    /// - file: The image file.
    /// - entity: Business entity name (e.g., "voucher_plan_headers"). Used by MSA to organize storage.
    /// - uniqueCode: Unique record identifier (e.g., "{planId}_cover_image"). Used for deduplication.
    /// 
    /// The returned RelativeUrl is stored directly in the database.
    /// Full CDN URL is composed at display time: {CDNEndpointURL}/{RelativeUrl}.
    /// </summary>
    [HttpPost("image")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB request limit (file + overhead)
    public async Task<ActionResult<UploadResponse>> UploadImage(
        IFormFile file,
        [FromForm] string entity,
        [FromForm] string uniqueCode,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new UploadResponse(false, Error: "No file uploaded."));
        }

        if (string.IsNullOrWhiteSpace(entity))
        {
            return BadRequest(new UploadResponse(false, Error: "The 'entity' field is required (e.g., 'voucher_plan_headers')."));
        }

        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return BadRequest(new UploadResponse(false, Error: "The 'uniqueCode' field is required (e.g., '{planId}_cover_image')."));
        }

        await using var stream = file.OpenReadStream();
        var result = await _imageStorageService.StoreAsync(
            stream,
            file.FileName,
            file.ContentType,
            entity,
            uniqueCode,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new UploadResponse(false, Error: result.Error));
        }

        // Return only the RelativeUrl — DB stores this value as-is.
        // Full CDN URL is composed at presentation layer: {CDNEndpointURL}/{RelativeUrl}
        return Ok(new UploadResponse(true, Url: result.Url));
    }
}

public record UploadResponse(bool Success, string? Url = null, string? Error = null);
