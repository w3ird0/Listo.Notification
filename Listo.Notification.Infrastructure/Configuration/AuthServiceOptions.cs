using System.ComponentModel.DataAnnotations;

namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Listo.Auth service integration.
/// </summary>
public class AuthServiceOptions
{
    /// <summary>
    /// Base URL of the Listo.Auth service.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Service-to-service shared secret for authentication.
    /// Added as X-Service-Secret header.
    /// </summary>
    [Required]
    public string ServiceSecret { get; set; } = string.Empty;

    /// <summary>
    /// HTTP client timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
