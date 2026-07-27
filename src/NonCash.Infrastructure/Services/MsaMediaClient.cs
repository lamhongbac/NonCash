using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// HTTP client wrapper for the MSA (Media Storage Agency) microservice.
/// Handles upload (multipart form) and delete (JSON) operations.
/// Configuration is read from MediaServiceConfig section in appsettings.json.
/// </summary>
public class MsaMediaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _appCode;
    private readonly string _apiKey;
    private readonly string _uploadEndpoint;
    private readonly string _deleteEndpoint;

    public MsaMediaClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;

        var baseUrl = config["MediaServiceConfig:BaseURL"]
            ?? throw new ArgumentNullException("MediaServiceConfig:BaseURL is missing");
        _appCode = config["MediaServiceConfig:AppCode"]
            ?? throw new ArgumentNullException("MediaServiceConfig:AppCode is missing");
        _apiKey = config["MediaServiceConfig:ApiKey"]
            ?? throw new ArgumentNullException("MediaServiceConfig:ApiKey is missing");
        _uploadEndpoint = config["MediaServiceConfig:UploadEndpoint"] ?? "/api/Media/upload";
        _deleteEndpoint = config["MediaServiceConfig:DeleteEndpoint"] ?? "/api/Media/delete-by-metadata";

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
    }

    /// <summary>
    /// Uploads a file to MSA. Returns a RelativeUrl (no domain) on success.
    /// </summary>
    /// <param name="entity">Business entity name (lowercase, e.g., "voucher_plan_headers").</param>
    /// <param name="uniqueCode">Unique record identifier (lowercase, e.g., "{planId}_cover_image").</param>
    /// <param name="mediaType">Media type category ("images", "videos", "documents").</param>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="fileName">Original file name.</param>
    public async Task<MsaUploadResult> UploadAsync(
        string entity,
        string uniqueCode,
        string mediaType,
        Stream fileStream,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_appCode), "AppCode");
        form.Add(new StringContent(mediaType.ToLowerInvariant()), "MediaType");
        form.Add(new StringContent(entity.ToLowerInvariant()), "Entity");
        form.Add(new StringContent(uniqueCode.ToLowerInvariant()), "UniqueCode");

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "File", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, _uploadEndpoint)
        {
            Content = form
        };
        request.Headers.Add("X-Api-Key", _apiKey);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            return new MsaUploadResult
            {
                IsSuccess = false,
                Message = $"API Error: {response.StatusCode} - {errContent}"
            };
        }

        var json = await response.Content.ReadAsStringAsync();
        var mediaResponse = JsonSerializer.Deserialize<MediaResponseDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (mediaResponse == null)
        {
            return new MsaUploadResult { IsSuccess = false, Message = "Empty response from MSA." };
        }

        return new MsaUploadResult
        {
            IsSuccess = mediaResponse.IsSuccess,
            Message = mediaResponse.Message,
            RelativeUrl = mediaResponse.RelativeUrl,
            FullUrl = mediaResponse.FullUrl,
            FileSize = mediaResponse.FileSize,
            FileExtension = mediaResponse.FileExtension
        };
    }

    /// <summary>
    /// Deletes all media files associated with the given entity + uniqueCode.
    /// Uses MSA's metadata-based wildcard deletion.
    /// </summary>
    public async Task<bool> DeleteAsync(string entity, string uniqueCode, string mediaType)
    {
        var payload = new
        {
            appCode = _appCode,
            mediaType = mediaType.ToLowerInvariant(),
            entity = entity.ToLowerInvariant(),
            uniqueCode = uniqueCode.ToLowerInvariant(),
            fileName = ""
        };

        var request = new HttpRequestMessage(HttpMethod.Delete, _deleteEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Api-Key", _apiKey);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}

// ===== DTOs =====

public class MsaUploadResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RelativeUrl { get; set; } = string.Empty;
    public string FullUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileExtension { get; set; } = string.Empty;
}

public class MediaResponseDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RelativeUrl { get; set; } = string.Empty;
    public string FullUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileExtension { get; set; } = string.Empty;
}
