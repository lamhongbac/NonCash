using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NonCash.API.Controllers;

/// <summary>
/// Proxies MSA (CDN) media content over the API's own scheme (HTTPS).
/// The DB stores only the MSA RelativeUrl; embedded &lt;img&gt; tags on HTTPS pages
/// cannot load the plain-HTTP CDN directly (mixed content), so they point here instead.
/// GET /api/v1/media/content?path={relativeUrl}
/// </summary>
[ApiController]
[Route("api/v1/media")]
public class MediaContentController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MediaContentController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("content")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetContent([FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "The 'path' query parameter is required." });

        var cdnBase = _configuration["MediaServiceConfig:CDNEndpointURL"];
        if (string.IsNullOrWhiteSpace(cdnBase))
            return StatusCode(500, new { error = "MediaServiceConfig:CDNEndpointURL is not configured." });

        var url = $"{cdnBase.TrimEnd('/')}/{path.TrimStart('/')}";
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return NotFound(new { error = "Media not found on the storage service." });

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return File(bytes, contentType);
        }
        catch (Exception)
        {
            return StatusCode(502, new { error = "Unable to reach the media storage service." });
        }
    }
}
