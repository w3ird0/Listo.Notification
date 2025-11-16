namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for managing notification attachments in Azure Blob Storage.
/// </summary>
public interface IAttachmentStorageService
{
    /// <summary>
    /// Uploads an attachment to Azure Blob Storage.
    /// </summary>
    /// <param name="tenantId">Tenant identifier for storage partitioning</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="content">File content stream</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blob URL and metadata</returns>
    Task<AttachmentUploadResult> UploadAttachmentAsync(
        string tenantId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an attachment from Azure Blob Storage.
    /// </summary>
    /// <param name="blobUrl">Blob URL to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content stream and metadata</returns>
    Task<AttachmentDownloadResult> DownloadAttachmentAsync(
        string blobUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment from Azure Blob Storage.
    /// </summary>
    /// <param name="blobUrl">Blob URL to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAttachmentAsync(
        string blobUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a time-limited SAS URL for secure file access.
    /// </summary>
    /// <param name="blobUrl">Blob URL</param>
    /// <param name="expiryMinutes">Expiry time in minutes (default 60)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SAS URL with read permissions</returns>
    Task<string> GetSasUrlAsync(
        string blobUrl,
        int expiryMinutes = 60,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of attachment upload operation.
/// </summary>
public record AttachmentUploadResult(
    string BlobUrl,
    string BlobName,
    long FileSizeBytes,
    string ContentType);

/// <summary>
/// Result of attachment download operation.
/// </summary>
public record AttachmentDownloadResult(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes);
