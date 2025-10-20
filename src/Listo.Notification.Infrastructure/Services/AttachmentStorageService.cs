using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Listo.Notification.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation for managing notification attachments.
/// Files are organized by tenant: {container}/{tenantId}/{yyyy/MM/dd}/{guid}_{filename}
/// </summary>
public class AttachmentStorageService : IAttachmentStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AttachmentStorageService> _logger;

    public AttachmentStorageService(
        IConfiguration configuration,
        ILogger<AttachmentStorageService> logger)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Azure Storage connection string is not configured");

        _containerName = configuration["AzureStorage:AttachmentsContainer"] ?? "notification-attachments";
        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
    }

    public async Task<AttachmentUploadResult> UploadAttachmentAsync(
        string tenantId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            // Generate unique blob name: {tenantId}/{yyyy/MM/dd}/{guid}_{filename}
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            var uniqueId = Guid.NewGuid();
            var safeFileName = SanitizeFileName(fileName);
            var blobName = $"{tenantId}/{dateFolder}/{uniqueId}_{safeFileName}";

            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload with metadata
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = new Dictionary<string, string>
                {
                    { "OriginalFileName", fileName },
                    { "TenantId", tenantId },
                    { "UploadDate", DateTime.UtcNow.ToString("o") }
                }
            };

            var response = await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

            _logger.LogInformation(
                "Uploaded attachment {BlobName} for tenant {TenantId}, size {Size} bytes",
                blobName, tenantId, content.Length);

            return new AttachmentUploadResult(
                BlobUrl: blobClient.Uri.ToString(),
                BlobName: blobName,
                FileSizeBytes: content.Length,
                ContentType: contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload attachment {FileName} for tenant {TenantId}", fileName, tenantId);
            throw;
        }
    }

    public async Task<AttachmentDownloadResult> DownloadAttachmentAsync(
        string blobUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(blobUrl));
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            var content = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

            var fileName = properties.Value.Metadata.TryGetValue("OriginalFileName", out var originalName)
                ? originalName
                : Path.GetFileName(blobClient.Name);

            _logger.LogInformation("Downloaded attachment {BlobUrl}", blobUrl);

            return new AttachmentDownloadResult(
                Content: content,
                FileName: fileName,
                ContentType: properties.Value.ContentType,
                FileSizeBytes: properties.Value.ContentLength);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download attachment from {BlobUrl}", blobUrl);
            throw;
        }
    }

    public async Task DeleteAttachmentAsync(
        string blobUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(blobUrl));
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted attachment {BlobUrl}", blobUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete attachment {BlobUrl}", blobUrl);
            throw;
        }
    }

    public async Task<string> GetSasUrlAsync(
        string blobUrl,
        int expiryMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(blobUrl));

            // Check if blob exists
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                throw new FileNotFoundException($"Blob not found: {blobUrl}");
            }

            // Generate SAS token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b", // Blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUrl = blobClient.GenerateSasUri(sasBuilder).ToString();

            _logger.LogInformation(
                "Generated SAS URL for {BlobUrl}, expires in {ExpiryMinutes} minutes",
                blobUrl, expiryMinutes);

            return sasUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for {BlobUrl}", blobUrl);
            throw;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
