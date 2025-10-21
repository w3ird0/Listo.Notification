# Implementation Plan: Notification Service Capability Gaps

**Project:** Listo.Notification Service Enhancements  
**Date Created:** 2025-10-21  
**Estimated Duration:** 3 Weeks  
**Total Effort:** 16-20 hours

---

## Executive Summary

This plan addresses 4 critical capability gaps identified in the Listo.Notification service to fully support Listo.Auth and Listo.Orders requirements:

1. **Device Token Management** - Enable push notifications with FCM device registration
2. **Batch Notification Endpoint** - Efficient driver broadcast for Orders service
3. **Template-Based API Flow** - Leverage centralized template system
4. **Synchronous Delivery** - Support real-time 2FA and critical notifications

---

## Table of Contents

1. [Phase 1: Device Management & Batch Endpoint](#phase-1-week-1)
2. [Phase 2: Template-Based Flow](#phase-2-week-2)
3. [Phase 3: Synchronous Delivery](#phase-3-week-3)
4. [Testing Strategy](#testing-strategy)
5. [Deployment Plan](#deployment-plan)
6. [Rollback Strategy](#rollback-strategy)

---

## Phase 1: Device Management & Batch Endpoint (Week 1)

**Duration:** 12 hours  
**Priority:** 🔴 CRITICAL

### Task 1.1: Device Token Management (8 hours)

#### 1.1.1 Database Schema (1 hour)

**Create Migration:** `20251021_AddDeviceManagement.cs`

```csharp
public partial class AddDeviceManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Devices",
            columns: table => new
            {
                DeviceId = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                DeviceToken = table.Column<string>(maxLength: 512, nullable: false),
                Platform = table.Column<int>(nullable: false), // 1=iOS, 2=Android, 3=Web
                DeviceInfo = table.Column<string>(maxLength: 1000, nullable: true),
                AppVersion = table.Column<string>(maxLength: 50, nullable: true),
                LastSeen = table.Column<DateTime>(nullable: false),
                Active = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Devices", x => x.DeviceId);
                table.ForeignKey(
                    name: "FK_Devices_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Devices_TenantId_UserId_Active",
            table: "Devices",
            columns: new[] { "TenantId", "UserId", "Active" });

        migrationBuilder.CreateIndex(
            name: "IX_Devices_DeviceToken",
            table: "Devices",
            column: "DeviceToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Devices_UserId_Platform",
            table: "Devices",
            columns: new[] { "UserId", "Platform" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Devices");
    }
}
```

**Entity Model:** `src/Listo.Notification.Domain/Entities/DeviceEntity.cs`

```csharp
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents a device registered for push notifications.
/// </summary>
public class DeviceEntity
{
    public Guid DeviceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    
    /// <summary>
    /// FCM device token for push notifications.
    /// </summary>
    public string DeviceToken { get; set; } = string.Empty;
    
    public DevicePlatform Platform { get; set; }
    
    /// <summary>
    /// JSON string containing device metadata (model, OS version, etc.)
    /// </summary>
    public string? DeviceInfo { get; set; }
    
    public string? AppVersion { get; set; }
    
    public DateTime LastSeen { get; set; }
    
    public bool Active { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}
```

**Enum:** `src/Listo.Notification.Domain/Enums/DevicePlatform.cs`

```csharp
namespace Listo.Notification.Domain.Enums;

public enum DevicePlatform
{
    iOS = 1,
    Android = 2,
    Web = 3
}
```

---

#### 1.1.2 DTOs (30 minutes)

**File:** `src/Listo.Notification.Application/DTOs/DeviceDtos.cs`

```csharp
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to register a new device for push notifications.
/// </summary>
public record RegisterDeviceRequest
{
    public required string DeviceToken { get; init; }
    public required DevicePlatform Platform { get; init; }
    public string? DeviceInfo { get; init; }
    public string? AppVersion { get; init; }
}

/// <summary>
/// Request to update an existing device registration.
/// </summary>
public record UpdateDeviceRequest
{
    public string? DeviceToken { get; init; }
    public string? DeviceInfo { get; init; }
    public string? AppVersion { get; init; }
    public bool? Active { get; init; }
}

/// <summary>
/// Response containing device details.
/// </summary>
public record DeviceResponse
{
    public required Guid DeviceId { get; init; }
    public required Guid UserId { get; init; }
    public required DevicePlatform Platform { get; init; }
    public required bool Active { get; init; }
    public required DateTime LastSeen { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? AppVersion { get; init; }
}

/// <summary>
/// Paginated list of devices.
/// </summary>
public record PagedDevicesResponse
{
    public required IEnumerable<DeviceResponse> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

---

#### 1.1.3 Repository Interface & Implementation (1 hour)

**Interface:** `src/Listo.Notification.Application/Interfaces/IDeviceRepository.cs`

```csharp
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Interfaces;

public interface IDeviceRepository
{
    Task<DeviceEntity?> GetByIdAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
    
    Task<DeviceEntity?> GetByTokenAsync(string deviceToken, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<DeviceEntity>> GetUserDevicesAsync(
        Guid tenantId, 
        Guid userId, 
        bool activeOnly = true, 
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<DeviceEntity>> GetActiveDevicesByPlatformAsync(
        Guid tenantId,
        Guid userId,
        DevicePlatform platform,
        CancellationToken cancellationToken = default);
    
    Task<int> GetUserDeviceCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    
    Task<DeviceEntity> CreateAsync(DeviceEntity device, CancellationToken cancellationToken = default);
    
    Task<DeviceEntity> UpdateAsync(DeviceEntity device, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
    
    Task<bool> DeactivateAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
    
    Task<int> DeactivateOldDevicesAsync(Guid tenantId, Guid userId, int keepCount = 5, CancellationToken cancellationToken = default);
}
```

**Implementation:** `src/Listo.Notification.Infrastructure/Repositories/DeviceRepository.cs`

```csharp
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Listo.Notification.Infrastructure.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly NotificationDbContext _context;

    public DeviceRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceEntity?> GetByIdAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<DeviceEntity?> GetByTokenAsync(string deviceToken, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken, cancellationToken);
    }

    public async Task<IEnumerable<DeviceEntity>> GetUserDevicesAsync(
        Guid tenantId, 
        Guid userId, 
        bool activeOnly = true, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Devices
            .Where(d => d.TenantId == tenantId && d.UserId == userId);

        if (activeOnly)
        {
            query = query.Where(d => d.Active);
        }

        return await query
            .OrderByDescending(d => d.LastSeen)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeviceEntity>> GetActiveDevicesByPlatformAsync(
        Guid tenantId,
        Guid userId,
        DevicePlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.TenantId == tenantId 
                && d.UserId == userId 
                && d.Platform == platform 
                && d.Active)
            .OrderByDescending(d => d.LastSeen)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUserDeviceCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .CountAsync(d => d.TenantId == tenantId && d.UserId == userId && d.Active, cancellationToken);
    }

    public async Task<DeviceEntity> CreateAsync(DeviceEntity device, CancellationToken cancellationToken = default)
    {
        _context.Devices.Add(device);
        await _context.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<DeviceEntity> UpdateAsync(DeviceEntity device, CancellationToken cancellationToken = default)
    {
        device.UpdatedAt = DateTime.UtcNow;
        _context.Devices.Update(device);
        await _context.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await GetByIdAsync(tenantId, deviceId, cancellationToken);
        if (device == null) return false;

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await GetByIdAsync(tenantId, deviceId, cancellationToken);
        if (device == null) return false;

        device.Active = false;
        device.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeactivateOldDevicesAsync(
        Guid tenantId, 
        Guid userId, 
        int keepCount = 5, 
        CancellationToken cancellationToken = default)
    {
        var devices = await _context.Devices
            .Where(d => d.TenantId == tenantId && d.UserId == userId && d.Active)
            .OrderByDescending(d => d.LastSeen)
            .ToListAsync(cancellationToken);

        if (devices.Count <= keepCount)
            return 0;

        var devicesToDeactivate = devices.Skip(keepCount).ToList();
        
        foreach (var device in devicesToDeactivate)
        {
            device.Active = false;
            device.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return devicesToDeactivate.Count;
    }
}
```

---

#### 1.1.4 Device Service (1.5 hours)

**Interface:** `src/Listo.Notification.Application/Interfaces/IDeviceService.cs`

```csharp
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Interfaces;

public interface IDeviceService
{
    Task<DeviceResponse> RegisterDeviceAsync(
        Guid tenantId,
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<DeviceResponse?> UpdateDeviceAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        UpdateDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDeviceAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<PagedDevicesResponse> GetUserDevicesAsync(
        Guid tenantId,
        Guid userId,
        int pageNumber = 1,
        int pageSize = 20,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<DeviceResponse?> GetDeviceByIdAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetActiveDeviceTokensAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

**Implementation:** `src/Listo.Notification.Application/Services/DeviceService.cs`

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<DeviceService> _logger;
    private const int MaxDevicesPerUser = 5;

    public DeviceService(
        IDeviceRepository deviceRepository,
        ILogger<DeviceService> logger)
    {
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    public async Task<DeviceResponse> RegisterDeviceAsync(
        Guid tenantId,
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check if device token already exists
        var existingDevice = await _deviceRepository.GetByTokenAsync(request.DeviceToken, cancellationToken);
        
        if (existingDevice != null)
        {
            // Update existing device
            if (existingDevice.UserId != userId)
            {
                _logger.LogWarning(
                    "Device token {DeviceToken} moved from user {OldUserId} to {NewUserId}",
                    request.DeviceToken, existingDevice.UserId, userId);
                
                existingDevice.UserId = userId;
                existingDevice.TenantId = tenantId;
            }

            existingDevice.Platform = request.Platform;
            existingDevice.DeviceInfo = request.DeviceInfo;
            existingDevice.AppVersion = request.AppVersion;
            existingDevice.LastSeen = DateTime.UtcNow;
            existingDevice.Active = true;
            existingDevice.UpdatedAt = DateTime.UtcNow;

            var updated = await _deviceRepository.UpdateAsync(existingDevice, cancellationToken);
            return MapToResponse(updated);
        }

        // Check device count limit
        var deviceCount = await _deviceRepository.GetUserDeviceCountAsync(tenantId, userId, cancellationToken);
        
        if (deviceCount >= MaxDevicesPerUser)
        {
            _logger.LogInformation(
                "User {UserId} has {Count} devices. Deactivating old devices.",
                userId, deviceCount);
            
            await _deviceRepository.DeactivateOldDevicesAsync(
                tenantId, 
                userId, 
                MaxDevicesPerUser - 1, 
                cancellationToken);
        }

        // Create new device
        var device = new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            DeviceToken = request.DeviceToken,
            Platform = request.Platform,
            DeviceInfo = request.DeviceInfo,
            AppVersion = request.AppVersion,
            LastSeen = DateTime.UtcNow,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _deviceRepository.CreateAsync(device, cancellationToken);
        
        _logger.LogInformation(
            "Device {DeviceId} registered for user {UserId} on platform {Platform}",
            created.DeviceId, userId, request.Platform);

        return MapToResponse(created);
    }

    public async Task<DeviceResponse?> UpdateDeviceAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        UpdateDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(tenantId, deviceId, cancellationToken);
        
        if (device == null || device.UserId != userId)
        {
            return null;
        }

        if (request.DeviceToken != null)
            device.DeviceToken = request.DeviceToken;
        
        if (request.DeviceInfo != null)
            device.DeviceInfo = request.DeviceInfo;
        
        if (request.AppVersion != null)
            device.AppVersion = request.AppVersion;
        
        if (request.Active.HasValue)
            device.Active = request.Active.Value;

        device.LastSeen = DateTime.UtcNow;

        var updated = await _deviceRepository.UpdateAsync(device, cancellationToken);
        return MapToResponse(updated);
    }

    public async Task<bool> DeleteDeviceAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(tenantId, deviceId, cancellationToken);
        
        if (device == null || device.UserId != userId)
        {
            return false;
        }

        return await _deviceRepository.DeleteAsync(tenantId, deviceId, cancellationToken);
    }

    public async Task<PagedDevicesResponse> GetUserDevicesAsync(
        Guid tenantId,
        Guid userId,
        int pageNumber = 1,
        int pageSize = 20,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetUserDevicesAsync(
            tenantId, 
            userId, 
            activeOnly, 
            cancellationToken);

        var deviceList = devices.ToList();
        var totalCount = deviceList.Count;
        
        var pagedDevices = deviceList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse)
            .ToList();

        return new PagedDevicesResponse
        {
            Items = pagedDevices,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<DeviceResponse?> GetDeviceByIdAsync(
        Guid tenantId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(tenantId, deviceId, cancellationToken);
        
        if (device == null || device.UserId != userId)
        {
            return null;
        }

        return MapToResponse(device);
    }

    public async Task<IEnumerable<string>> GetActiveDeviceTokensAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetUserDevicesAsync(
            tenantId, 
            userId, 
            activeOnly: true, 
            cancellationToken);

        return devices.Select(d => d.DeviceToken).ToList();
    }

    private static DeviceResponse MapToResponse(DeviceEntity device)
    {
        return new DeviceResponse
        {
            DeviceId = device.DeviceId,
            UserId = device.UserId,
            Platform = device.Platform,
            Active = device.Active,
            LastSeen = device.LastSeen,
            CreatedAt = device.CreatedAt,
            AppVersion = device.AppVersion
        };
    }
}
```

---

#### 1.1.5 Devices Controller (2 hours)

**File:** `src/Listo.Notification.API/Controllers/DevicesController.cs`

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Device management endpoints for push notification registration.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceService deviceService,
        ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new device for push notifications.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetUserContext();

        try
        {
            var device = await _deviceService.RegisterDeviceAsync(
                tenantId,
                userId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetDevice),
                new { deviceId = device.DeviceId },
                device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device for user {UserId}", userId);
            return BadRequest(new { error = "Failed to register device" });
        }
    }

    /// <summary>
    /// Update an existing device registration.
    /// </summary>
    [HttpPut("{deviceId}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> UpdateDevice(
        [FromRoute] Guid deviceId,
        [FromBody] UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetUserContext();

        var device = await _deviceService.UpdateDeviceAsync(
            tenantId,
            userId,
            deviceId,
            request,
            cancellationToken);

        if (device == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        return Ok(device);
    }

    /// <summary>
    /// Delete a device registration.
    /// </summary>
    [HttpDelete("{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteDevice(
        [FromRoute] Guid deviceId,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetUserContext();

        var deleted = await _deviceService.DeleteDeviceAsync(
            tenantId,
            userId,
            deviceId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound(new { error = "Device not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Get all devices for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedDevicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedDevicesResponse>> GetUserDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetUserContext();

        var devices = await _deviceService.GetUserDevicesAsync(
            tenantId,
            userId,
            page,
            pageSize,
            activeOnly,
            cancellationToken);

        return Ok(devices);
    }

    /// <summary>
    /// Get a specific device by ID.
    /// </summary>
    [HttpGet("{deviceId}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> GetDevice(
        [FromRoute] Guid deviceId,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetUserContext();

        var device = await _deviceService.GetDeviceByIdAsync(
            tenantId,
            userId,
            deviceId,
            cancellationToken);

        if (device == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        return Ok(device);
    }

    private (Guid tenantId, Guid userId) GetUserContext()
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value 
            ?? User.FindFirst("http://schemas.listoexpress.com/claims/tenant_id")?.Value;
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(tenantIdClaim) || string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User context not found in JWT");
        }

        return (Guid.Parse(tenantIdClaim), Guid.Parse(userIdClaim));
    }
}
```

---

#### 1.1.6 Update Push Notification Delivery (2 hours)

**Update:** `src/Listo.Notification.Application/Services/NotificationDeliveryService.cs`

Add device token lookup logic:

```csharp
// Add to existing NotificationDeliveryService class

private readonly IDeviceService _deviceService;

// Update constructor to inject IDeviceService

private async Task<DeliveryResult> SendPushNotificationAsync(
    NotificationEntity notification,
    CancellationToken cancellationToken)
{
    try
    {
        // NEW: Lookup device tokens for user
        var deviceTokens = await _deviceService.GetActiveDeviceTokensAsync(
            notification.TenantId,
            notification.UserId,
            cancellationToken);

        var tokens = deviceTokens.ToList();
        
        if (!tokens.Any())
        {
            _logger.LogWarning(
                "No active devices found for user {UserId}. Push notification not sent.",
                notification.UserId);
            
            return DeliveryResult.Failed("No active devices registered");
        }

        // Send to all active devices
        var results = new List<(string token, bool success)>();
        
        foreach (var token in tokens)
        {
            try
            {
                var message = new Message
                {
                    Token = token,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = notification.Subject,
                        Body = notification.Body
                    },
                    Data = ParseMetadata(notification.Metadata)
                };

                var response = await FirebaseMessaging.DefaultInstance.SendAsync(
                    message, 
                    cancellationToken);
                
                results.Add((token, true));
                
                _logger.LogInformation(
                    "Push notification sent successfully. MessageId: {MessageId}, Token: {Token}",
                    response, token);
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                // Token is invalid or device unregistered - deactivate it
                _logger.LogWarning(
                    "Invalid device token {Token}. Marking as inactive. Error: {Error}",
                    token, ex.Message);
                
                await DeactivateDeviceByTokenAsync(token, cancellationToken);
                results.Add((token, false));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to send push notification to token {Token}", 
                    token);
                results.Add((token, false));
            }
        }

        var successCount = results.Count(r => r.success);
        var totalCount = results.Count;

        if (successCount == 0)
        {
            return DeliveryResult.Failed($"Failed to send to all {totalCount} devices");
        }
        
        if (successCount < totalCount)
        {
            return DeliveryResult.PartialSuccess(
                $"Sent to {successCount}/{totalCount} devices");
        }

        return DeliveryResult.Success($"Sent to {successCount} devices");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Push notification delivery failed");
        return DeliveryResult.Failed(ex.Message);
    }
}

private async Task DeactivateDeviceByTokenAsync(string deviceToken, CancellationToken cancellationToken)
{
    try
    {
        var device = await _deviceRepository.GetByTokenAsync(deviceToken, cancellationToken);
        if (device != null)
        {
            await _deviceRepository.DeactivateAsync(
                device.TenantId, 
                device.DeviceId, 
                cancellationToken);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to deactivate device token {Token}", deviceToken);
    }
}
```

---

#### 1.1.7 Validation (30 minutes)

**File:** `src/Listo.Notification.Application/Validators/RegisterDeviceRequestValidator.cs`

```csharp
using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceToken)
            .NotEmpty()
            .WithMessage("Device token is required")
            .MaximumLength(512)
            .WithMessage("Device token cannot exceed 512 characters");

        RuleFor(x => x.Platform)
            .IsInEnum()
            .WithMessage("Invalid device platform");

        RuleFor(x => x.DeviceInfo)
            .MaximumLength(1000)
            .WithMessage("Device info cannot exceed 1000 characters")
            .When(x => x.DeviceInfo != null);

        RuleFor(x => x.AppVersion)
            .MaximumLength(50)
            .WithMessage("App version cannot exceed 50 characters")
            .When(x => x.AppVersion != null);
    }
}
```

---

#### 1.1.8 DI Registration (15 minutes)

**Update:** `src/Listo.Notification.API/Program.cs`

```csharp
// Add to service registration
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IValidator<RegisterDeviceRequest>, RegisterDeviceRequestValidator>();
```

**Update:** `src/Listo.Notification.Infrastructure/Data/NotificationDbContext.cs`

```csharp
public DbSet<DeviceEntity> Devices { get; set; } = null!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Add device entity configuration
    modelBuilder.Entity<DeviceEntity>(entity =>
    {
        entity.HasKey(e => e.DeviceId);
        
        entity.HasIndex(e => new { e.TenantId, e.UserId, e.Active });
        entity.HasIndex(e => e.DeviceToken).IsUnique();
        entity.HasIndex(e => new { e.UserId, e.Platform });

        entity.Property(e => e.DeviceToken).HasMaxLength(512).IsRequired();
        entity.Property(e => e.DeviceInfo).HasMaxLength(1000);
        entity.Property(e => e.AppVersion).HasMaxLength(50);
    });
}
```

---

### Task 1.2: Batch Notification Endpoint (4 hours)

#### 1.2.1 DTOs (30 minutes)

**Update:** `src/Listo.Notification.Application/DTOs/InternalDtos.cs`

```csharp
/// <summary>
/// Request to queue multiple notifications in a batch.
/// </summary>
public record BatchInternalNotificationRequest
{
    public required string ServiceName { get; init; }
    public string? EventType { get; init; }
    public required IEnumerable<InternalNotificationRequest> Notifications { get; init; }
}

/// <summary>
/// Response after queueing batch notifications.
/// </summary>
public record BatchQueueNotificationResponse
{
    public required int TotalRequested { get; init; }
    public required int QueuedCount { get; init; }
    public required int FailedCount { get; init; }
    public required IEnumerable<QueueNotificationResult> Results { get; init; }
    public required DateTime ProcessedAt { get; init; }
}

/// <summary>
/// Individual result in batch response.
/// </summary>
public record QueueNotificationResult
{
    public required int Index { get; init; }
    public required bool Success { get; init; }
    public Guid? QueueId { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

#### 1.2.2 Service Method (1 hour)

**Update:** `src/Listo.Notification.Application/Interfaces/INotificationService.cs`

```csharp
/// <summary>
/// Queue multiple notifications in a batch for async processing.
/// </summary>
Task<BatchQueueNotificationResponse> QueueBatchNotificationsAsync(
    IEnumerable<InternalNotificationRequest> requests,
    string serviceName,
    CancellationToken cancellationToken = default);
```

**Implementation:** `src/Listo.Notification.Application/Services/NotificationService.cs`

```csharp
public async Task<BatchQueueNotificationResponse> QueueBatchNotificationsAsync(
    IEnumerable<InternalNotificationRequest> requests,
    string serviceName,
    CancellationToken cancellationToken = default)
{
    var requestList = requests.ToList();
    var results = new List<QueueNotificationResult>();
    var queuedCount = 0;
    var failedCount = 0;

    // Process in parallel with degree of parallelism = 10
    var semaphore = new SemaphoreSlim(10);
    var tasks = requestList.Select(async (request, index) =>
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var response = await QueueNotificationAsync(
                    request,
                    serviceName,
                    cancellationToken);

                Interlocked.Increment(ref queuedCount);
                
                return new QueueNotificationResult
                {
                    Index = index,
                    Success = true,
                    QueueId = response.QueueId
                };
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failedCount);
                
                _logger.LogError(ex, 
                    "Failed to queue notification at index {Index} in batch", 
                    index);

                return new QueueNotificationResult
                {
                    Index = index,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        finally
        {
            semaphore.Release();
        }
    });

    var allResults = await Task.WhenAll(tasks);

    _logger.LogInformation(
        "Batch queue completed: {Queued}/{Total} queued, {Failed} failed",
        queuedCount, requestList.Count, failedCount);

    return new BatchQueueNotificationResponse
    {
        TotalRequested = requestList.Count,
        QueuedCount = queuedCount,
        FailedCount = failedCount,
        Results = allResults.OrderBy(r => r.Index),
        ProcessedAt = DateTime.UtcNow
    };
}
```

---

#### 1.2.3 Controller Endpoint (1 hour)

**Update:** `src/Listo.Notification.API/Controllers/InternalController.cs`

```csharp
/// <summary>
/// Queue multiple notifications in a batch for async processing.
/// Used by other Listo services to send bulk notifications efficiently.
/// </summary>
/// <param name="request">Batch notification request with service context</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Batch queue operation result</returns>
[HttpPost("notifications/queue/batch")]
[ProducesResponseType(typeof(BatchQueueNotificationResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<BatchQueueNotificationResponse>> QueueBatchNotifications(
    [FromBody] BatchInternalNotificationRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var serviceName = HttpContext.Items["ServiceName"] as string 
            ?? throw new UnauthorizedAccessException("Service name not found in request context");

        var notifications = request.Notifications.ToList();
        
        if (!notifications.Any())
        {
            return BadRequest(new { error = "Notification list cannot be empty" });
        }

        if (notifications.Count > 100)
        {
            return BadRequest(new { error = "Batch size cannot exceed 100 notifications" });
        }

        _logger.LogInformation(
            "Queuing batch of {Count} notifications from service: {ServiceName}",
            notifications.Count, serviceName);

        var response = await _notificationService.QueueBatchNotificationsAsync(
            notifications,
            serviceName,
            cancellationToken);

        if (response.FailedCount > 0)
        {
            _logger.LogWarning(
                "Batch partially failed: {Failed}/{Total} notifications failed to queue",
                response.FailedCount, response.TotalRequested);
        }

        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Batch queue request failed");
        return BadRequest(new { error = ex.Message });
    }
}
```

---

#### 1.2.4 Validation (30 minutes)

**File:** `src/Listo.Notification.Application/Validators/BatchInternalNotificationRequestValidator.cs`

```csharp
using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

public class BatchInternalNotificationRequestValidator : AbstractValidator<BatchInternalNotificationRequest>
{
    public BatchInternalNotificationRequestValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required");

        RuleFor(x => x.Notifications)
            .NotNull()
            .WithMessage("Notifications list is required")
            .Must(n => n != null && n.Any())
            .WithMessage("Notifications list cannot be empty")
            .Must(n => n == null || n.Count() <= 100)
            .WithMessage("Batch size cannot exceed 100 notifications");

        RuleForEach(x => x.Notifications)
            .SetValidator(new InternalNotificationRequestValidator());
    }
}
```

---

#### 1.2.5 Integration Test (1 hour)

**File:** `tests/Listo.Notification.Tests.Integration/BatchNotificationTests.cs`

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Listo.Notification.Tests.Integration;

public class BatchNotificationTests : IntegrationTestBase
{
    [Fact]
    public async Task QueueBatchNotifications_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new BatchInternalNotificationRequest
        {
            ServiceName = "Listo.Orders",
            Notifications = Enumerable.Range(1, 10).Select(i => new InternalNotificationRequest
            {
                ServiceName = "Listo.Orders",
                Channel = NotificationChannel.Push,
                Recipient = $"user-{i}@example.com",
                Subject = $"Test Notification {i}",
                Body = $"This is test notification {i}",
                Priority = Priority.Normal,
                ServiceOrigin = ServiceOrigin.Orders
            }).ToList()
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/internal/notifications/queue/batch",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<BatchQueueNotificationResponse>();
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalRequested);
        Assert.Equal(10, result.QueuedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task QueueBatchNotifications_ExceedsLimit_ReturnsBadRequest()
    {
        // Arrange
        var request = new BatchInternalNotificationRequest
        {
            ServiceName = "Listo.Orders",
            Notifications = Enumerable.Range(1, 101).Select(i => new InternalNotificationRequest
            {
                ServiceName = "Listo.Orders",
                Channel = NotificationChannel.Push,
                Recipient = $"user-{i}@example.com",
                Subject = "Test",
                Body = "Test",
                Priority = Priority.Normal,
                ServiceOrigin = ServiceOrigin.Orders
            }).ToList()
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/internal/notifications/queue/batch",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

---

## Phase 2: Template-Based Flow (Week 2)

**Duration:** 4 hours  
**Priority:** 🟡 MEDIUM

### Task 2.1: Update Internal API DTOs (1 hour)

**Update:** `src/Listo.Notification.Application/DTOs/InternalDtos.cs`

```csharp
/// <summary>
/// Internal service-to-service notification request (requires X-Service-Secret header).
/// Supports both pre-rendered content and template-based rendering.
/// </summary>
public record InternalNotificationRequest : SendNotificationRequest
{
    public required string ServiceName { get; init; }
    public string? EventType { get; init; }
    
    // Option 1: Pre-rendered (backward compatibility)
    public new string? Subject { get; init; }
    public new string? Body { get; init; }
    
    // Option 2: Template-based (preferred)
    public string? TemplateKey { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public string? Locale { get; init; }
}
```

---

### Task 2.2: Update Notification Service Logic (2 hours)

**Update:** `src/Listo.Notification.Application/Services/NotificationService.cs`

```csharp
public async Task<QueueNotificationResponse> QueueNotificationAsync(
    InternalNotificationRequest request,
    string serviceName,
    CancellationToken cancellationToken = default)
{
    string subject;
    string body;

    // Template-based flow
    if (!string.IsNullOrEmpty(request.TemplateKey))
    {
        _logger.LogInformation(
            "Rendering template {TemplateKey} for service {ServiceName}",
            request.TemplateKey, serviceName);

        try
        {
            var rendered = await _templateRenderingService.RenderTemplateAsync(
                request.TenantId ?? Guid.Empty, // TODO: Get from service context
                request.TemplateKey,
                request.Variables ?? new Dictionary<string, object>(),
                request.Locale ?? "en-US",
                cancellationToken);

            subject = rendered.Subject;
            body = rendered.Body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Template {TemplateKey} not found or rendering failed", 
                request.TemplateKey);
            
            // Fall back to pre-rendered if provided
            if (!string.IsNullOrEmpty(request.Body))
            {
                _logger.LogWarning(
                    "Falling back to pre-rendered content for {TemplateKey}",
                    request.TemplateKey);
                
                subject = request.Subject ?? string.Empty;
                body = request.Body;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Template '{request.TemplateKey}' not found and no fallback content provided");
            }
        }
    }
    // Pre-rendered flow (backward compatibility)
    else
    {
        if (string.IsNullOrEmpty(request.Body))
        {
            throw new ArgumentException("Either TemplateKey or Body must be provided");
        }

        _logger.LogInformation(
            "Using pre-rendered content from service {ServiceName}",
            serviceName);

        subject = request.Subject ?? string.Empty;
        body = request.Body;
    }

    // Continue with existing queue logic
    var notification = new NotificationQueueEntity
    {
        // ... existing code
        Subject = subject,
        Body = body,
        TemplateKey = request.TemplateKey,
        TemplateVariables = request.Variables != null 
            ? JsonSerializer.Serialize(request.Variables) 
            : null
    };

    // ... rest of existing implementation
}
```

---

### Task 2.3: Update Validators (30 minutes)

**Update:** `src/Listo.Notification.Application/Validators/InternalNotificationRequestValidator.cs`

```csharp
public class InternalNotificationRequestValidator : AbstractValidator<InternalNotificationRequest>
{
    public InternalNotificationRequestValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required");

        RuleFor(x => x.Channel)
            .IsInEnum()
            .WithMessage("Invalid notification channel");

        RuleFor(x => x.Recipient)
            .NotEmpty()
            .WithMessage("Recipient is required");

        // Validate that either TemplateKey or Body is provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.TemplateKey) || !string.IsNullOrEmpty(x.Body))
            .WithMessage("Either TemplateKey or Body must be provided");

        // If TemplateKey is provided, Variables should be provided
        RuleFor(x => x.Variables)
            .NotNull()
            .WithMessage("Variables are required when using TemplateKey")
            .When(x => !string.IsNullOrEmpty(x.TemplateKey));

        RuleFor(x => x.Locale)
            .MaximumLength(10)
            .WithMessage("Locale cannot exceed 10 characters")
            .When(x => x.Locale != null);
    }
}
```

---

### Task 2.4: Template Seeding Script (30 minutes)

**File:** `src/Listo.Notification.Infrastructure/Data/Seeding/TemplateSeedData.cs`

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Infrastructure.Data.Seeding;

public class TemplateSeedData
{
    private readonly ITemplateRenderingService _templateService;
    private readonly ILogger<TemplateSeedData> _logger;

    public TemplateSeedData(
        ITemplateRenderingService templateService,
        ILogger<TemplateSeedData> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public async Task SeedTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding notification templates for tenant {TenantId}", tenantId);

        var templates = new[]
        {
            // Auth Templates
            new CreateTemplateRequest
            {
                TemplateKey = "email_verification",
                Name = "Email Verification",
                Channel = NotificationChannel.Email,
                SubjectTemplate = "Verify Your Email - ListoExpress",
                BodyTemplate = @"
                    Hi {{userName}},
                    
                    Please verify your email address by clicking the link below:
                    {{verificationLink}}
                    
                    Or use this code: {{verificationCode}}
                    
                    This link expires in {{expiresIn}}.
                    
                    Best regards,
                    ListoExpress Team",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "sms_verification",
                Name = "SMS Verification",
                Channel = NotificationChannel.SMS,
                SubjectTemplate = "",
                BodyTemplate = "Your ListoExpress verification code is: {{verificationCode}}",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "welcome_email",
                Name = "Welcome Email",
                Channel = NotificationChannel.Email,
                SubjectTemplate = "Welcome to ListoExpress!",
                BodyTemplate = @"
                    Hi {{userName}},
                    
                    Welcome to ListoExpress! We're excited to have you on board.
                    
                    Get started: {{loginLink}}
                    
                    Best regards,
                    ListoExpress Team",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "password_reset",
                Name = "Password Reset",
                Channel = NotificationChannel.Email,
                SubjectTemplate = "Reset Your Password - ListoExpress",
                BodyTemplate = @"
                    Hi {{userName}},
                    
                    You requested to reset your password. Click the link below:
                    {{resetLink}}
                    
                    This link expires in {{expiresIn}}.
                    
                    If you didn't request this, please ignore this email.
                    
                    Best regards,
                    ListoExpress Team",
                Locale = "en-US",
                IsActive = true
            },
            
            // Orders Templates
            new CreateTemplateRequest
            {
                TemplateKey = "driver_new_order_available",
                Name = "Driver - New Order Available",
                Channel = NotificationChannel.Push,
                SubjectTemplate = "New Order Available - {{orderNumber}}",
                BodyTemplate = @"
                    Delivery fee: ${{deliveryFee}}
                    Total: ${{totalAmount}}
                    
                    From: {{pickupAddress}}
                    To: {{deliveryAddress}}
                    
                    Estimated time: {{estimatedDeliveryTime}}",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "driver_order_assigned",
                Name = "Driver - Order Assigned",
                Channel = NotificationChannel.Push,
                SubjectTemplate = "Order Assigned - {{orderId}}",
                BodyTemplate = "You've been assigned to order {{orderId}}. View details.",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "driver_assignment_confirmed",
                Name = "Driver - Assignment Confirmed",
                Channel = NotificationChannel.Push,
                SubjectTemplate = "Order Confirmed - {{orderNumber}}",
                BodyTemplate = @"
                    Pickup: {{pickupAddress}}
                    Deliver to: {{deliveryAddress}}
                    By: {{estimatedDeliveryTime}}",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "driver_assignment_cancelled",
                Name = "Driver - Assignment Cancelled",
                Channel = NotificationChannel.Push,
                SubjectTemplate = "Assignment Cancelled - {{orderNumber}}",
                BodyTemplate = "Your assignment to order {{orderNumber}} was cancelled. Reason: {{reason}}",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "admin_assignment_timeout",
                Name = "Admin - Assignment Timeout",
                Channel = NotificationChannel.Email,
                SubjectTemplate = "Order Assignment Timeout - {{orderNumber}}",
                BodyTemplate = @"
                    Driver {{driverId}} didn't respond to order {{orderNumber}} within {{timeoutMinutes}} minutes.
                    
                    Order Status: {{orderStatus}}
                    Total: ${{totalAmount}}
                    Customer: {{customerAddress}}
                    
                    Manage order: {{orderManagementLink}}",
                Locale = "en-US",
                IsActive = true
            },
            new CreateTemplateRequest
            {
                TemplateKey = "admin_driver_unassigned",
                Name = "Admin - Driver Unassigned",
                Channel = NotificationChannel.Email,
                SubjectTemplate = "Driver Unassigned - Order {{orderId}}",
                BodyTemplate = @"
                    Driver {{driverId}} was unassigned from order {{orderId}}.
                    
                    Reason: {{reason}}
                    Time: {{unassignedAt}}
                    
                    Manage order: {{orderManagementLink}}",
                Locale = "en-US",
                IsActive = true
            }
        };

        foreach (var template in templates)
        {
            try
            {
                await _templateService.CreateTemplateAsync(tenantId, template, cancellationToken);
                _logger.LogInformation("Template '{TemplateKey}' seeded successfully", template.TemplateKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed template '{TemplateKey}'", template.TemplateKey);
            }
        }

        _logger.LogInformation("Template seeding completed for tenant {TenantId}", tenantId);
    }
}
```

---

## Phase 3: Synchronous Delivery (Week 3)

**Duration:** 5 hours  
**Priority:** 🟡 MEDIUM

### Task 3.1: Update Request DTO (15 minutes)

**Update:** `src/Listo.Notification.Application/DTOs/InternalDtos.cs`

```csharp
public record InternalNotificationRequest : SendNotificationRequest
{
    // ... existing properties
    
    /// <summary>
    /// If true, notification is sent immediately and delivery result is returned.
    /// If false, notification is queued for async processing via Service Bus.
    /// </summary>
    public bool Synchronous { get; init; } = false;
}
```

---

### Task 3.2: Update Queue Response (15 minutes)

**Update:** `src/Listo.Notification.Application/DTOs/InternalDtos.cs`

```csharp
public record QueueNotificationResponse
{
    public required Guid QueueId { get; init; }
    public required string Status { get; init; }
    public required DateTime QueuedAt { get; init; }
    
    // Synchronous delivery fields
    public DateTime? SentAt { get; init; }
    public string? DeliveryStatus { get; init; }
    public string? DeliveryDetails { get; init; }
}
```

---

### Task 3.3: Implement Synchronous Delivery Service (2 hours)

**Update:** `src/Listo.Notification.Application/Interfaces/INotificationDeliveryService.cs`

```csharp
/// <summary>
/// Send notification immediately (synchronous delivery).
/// </summary>
Task<DeliveryResult> SendNowAsync(
    NotificationEntity notification,
    CancellationToken cancellationToken = default);
```

**Implementation:** `src/Listo.Notification.Application/Services/NotificationDeliveryService.cs`

```csharp
public async Task<DeliveryResult> SendNowAsync(
    NotificationEntity notification,
    CancellationToken cancellationToken = default)
{
    _logger.LogInformation(
        "Sending notification {NotificationId} synchronously via {Channel}",
        notification.NotificationId, notification.Channel);

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout

    try
    {
        DeliveryResult result = notification.Channel switch
        {
            NotificationChannel.SMS => await SendSmsNotificationAsync(notification, cts.Token),
            NotificationChannel.Email => await SendEmailNotificationAsync(notification, cts.Token),
            NotificationChannel.Push => await SendPushNotificationAsync(notification, cts.Token),
            _ => DeliveryResult.Failed($"Synchronous delivery not supported for channel {notification.Channel}")
        };

        // Update notification entity
        notification.Status = result.Success ? NotificationStatus.Sent : NotificationStatus.Failed;
        notification.SentAt = result.Success ? DateTime.UtcNow : null;
        notification.ErrorMessage = result.Success ? null : result.ErrorMessage;

        await _notificationRepository.UpdateAsync(notification, cancellationToken);

        return result;
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning(
            "Synchronous delivery timed out for notification {NotificationId}",
            notification.NotificationId);

        notification.Status = NotificationStatus.Failed;
        notification.ErrorMessage = "Delivery timeout (30s exceeded)";
        await _notificationRepository.UpdateAsync(notification, cancellationToken);

        return DeliveryResult.Failed("Delivery timeout");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, 
            "Synchronous delivery failed for notification {NotificationId}",
            notification.NotificationId);

        notification.Status = NotificationStatus.Failed;
        notification.ErrorMessage = ex.Message;
        await _notificationRepository.UpdateAsync(notification, cancellationToken);

        return DeliveryResult.Failed(ex.Message);
    }
}
```

---

### Task 3.4: Update Internal Controller (1.5 hours)

**Update:** `src/Listo.Notification.API/Controllers/InternalController.cs`

```csharp
[HttpPost("notifications/queue")]
[ProducesResponseType(typeof(QueueNotificationResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status408RequestTimeout)]
public async Task<ActionResult<QueueNotificationResponse>> QueueNotification(
    [FromBody] InternalNotificationRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var serviceName = HttpContext.Items["ServiceName"] as string 
            ?? throw new UnauthorizedAccessException("Service name not found in request context");

        _logger.LogInformation(
            "Queueing notification from service: ServiceName={ServiceName}, Channel={Channel}, Synchronous={Synchronous}",
            serviceName, request.Channel, request.Synchronous);

        // Synchronous delivery path
        if (request.Synchronous)
        {
            // Validate channel supports synchronous delivery
            if (request.Channel == NotificationChannel.InApp)
            {
                return BadRequest(new { error = "Synchronous delivery not supported for In-App channel" });
            }

            // Create notification entity
            var notification = await CreateNotificationEntityAsync(request, serviceName, cancellationToken);
            
            // Send immediately
            var deliveryResult = await _notificationDeliveryService.SendNowAsync(
                notification,
                cancellationToken);

            if (deliveryResult.Success)
            {
                return Ok(new QueueNotificationResponse
                {
                    QueueId = notification.NotificationId,
                    Status = "Sent",
                    QueuedAt = notification.CreatedAt,
                    SentAt = DateTime.UtcNow,
                    DeliveryStatus = "Delivered",
                    DeliveryDetails = deliveryResult.Message
                });
            }
            else
            {
                if (deliveryResult.ErrorMessage?.Contains("timeout") == true)
                {
                    return StatusCode(StatusCodes.Status408RequestTimeout, new QueueNotificationResponse
                    {
                        QueueId = notification.NotificationId,
                        Status = "Failed",
                        QueuedAt = notification.CreatedAt,
                        DeliveryStatus = "Timeout",
                        DeliveryDetails = deliveryResult.ErrorMessage
                    });
                }

                return Ok(new QueueNotificationResponse
                {
                    QueueId = notification.NotificationId,
                    Status = "Failed",
                    QueuedAt = notification.CreatedAt,
                    DeliveryStatus = "Failed",
                    DeliveryDetails = deliveryResult.ErrorMessage
                });
            }
        }

        // Async delivery path (existing code)
        var response = await _notificationService.QueueNotificationAsync(
            request,
            serviceName,
            cancellationToken);

        return Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning(ex, "Invalid queue notification request");
        return BadRequest(new { error = ex.Message });
    }
}

private async Task<NotificationEntity> CreateNotificationEntityAsync(
    InternalNotificationRequest request,
    string serviceName,
    CancellationToken cancellationToken)
{
    // Render template if TemplateKey provided
    string subject, body;
    if (!string.IsNullOrEmpty(request.TemplateKey))
    {
        var rendered = await _templateRenderingService.RenderTemplateAsync(
            Guid.Empty, // TODO: Get tenant from context
            request.TemplateKey,
            request.Variables ?? new Dictionary<string, object>(),
            request.Locale ?? "en-US",
            cancellationToken);
        
        subject = rendered.Subject;
        body = rendered.Body;
    }
    else
    {
        subject = request.Subject ?? string.Empty;
        body = request.Body ?? string.Empty;
    }

    var notification = new NotificationEntity
    {
        NotificationId = Guid.NewGuid(),
        TenantId = Guid.Empty, // TODO: Get from service context
        UserId = Guid.Empty, // TODO: Parse from recipient or context
        Channel = request.Channel,
        Recipient = request.Recipient,
        Subject = subject,
        Body = body,
        Priority = request.Priority,
        ServiceOrigin = request.ServiceOrigin,
        Status = NotificationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    return await _notificationRepository.CreateAsync(notification, cancellationToken);
}
```

---

### Task 3.5: Update Validator (30 minutes)

**Update:** `src/Listo.Notification.Application/Validators/InternalNotificationRequestValidator.cs`

```csharp
public class InternalNotificationRequestValidator : AbstractValidator<InternalNotificationRequest>
{
    public InternalNotificationRequestValidator()
    {
        // ... existing rules

        // Synchronous delivery restrictions
        RuleFor(x => x.Channel)
            .Must(c => c != NotificationChannel.InApp)
            .WithMessage("Synchronous delivery not supported for In-App notifications")
            .When(x => x.Synchronous);

        RuleFor(x => x)
            .Must(x => x.Channel == NotificationChannel.SMS || !x.Synchronous)
            .WithMessage("Synchronous delivery recommended for SMS only (critical use cases)")
            .When(x => x.Synchronous);
    }
}
```

---

### Task 3.6: Integration Test (30 minutes)

**File:** `tests/Listo.Notification.Tests.Integration/SynchronousDeliveryTests.cs`

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Listo.Notification.Tests.Integration;

public class SynchronousDeliveryTests : IntegrationTestBase
{
    [Fact]
    public async Task QueueNotification_Synchronous_SMS_ReturnsImmediateResult()
    {
        // Arrange
        var request = new InternalNotificationRequest
        {
            ServiceName = "Listo.Auth",
            Channel = NotificationChannel.SMS,
            Recipient = "+1234567890",
            Subject = "2FA Code",
            Body = "Your code is: 123456",
            Priority = Priority.High,
            ServiceOrigin = ServiceOrigin.Auth,
            Synchronous = true
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/internal/notifications/queue",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<QueueNotificationResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.SentAt);
        Assert.Equal("Sent", result.Status);
        Assert.NotNull(result.DeliveryStatus);
    }

    [Fact]
    public async Task QueueNotification_Synchronous_InApp_ReturnsBadRequest()
    {
        // Arrange
        var request = new InternalNotificationRequest
        {
            ServiceName = "Listo.Orders",
            Channel = NotificationChannel.InApp,
            Recipient = "user-123",
            Subject = "Test",
            Body = "Test",
            Priority = Priority.Normal,
            ServiceOrigin = ServiceOrigin.Orders,
            Synchronous = true
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/internal/notifications/queue",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

---

## Testing Strategy

### Unit Tests
- **Device Management:** Repository, Service, Validators
- **Batch Endpoint:** Service method, Controller endpoint
- **Template Flow:** Rendering logic, Fallback behavior
- **Synchronous Delivery:** Timeout handling, Channel validation

**Estimated Coverage:** 85%+

### Integration Tests
- Device registration flow end-to-end
- Batch notification with partial failures
- Template-based vs pre-rendered notifications
- Synchronous SMS delivery with mocked Twilio

**Test Environment:** In-memory database + mocked external services

### Load Tests (Optional)
- Batch endpoint: 1000 notifications in 100 batches
- Device lookup performance: 10,000 concurrent requests
- Template rendering: 1000 renders/second

---

## Deployment Plan

### Pre-Deployment Checklist
1. ✅ All migrations tested in development
2. ✅ Unit tests passing (85%+ coverage)
3. ✅ Integration tests passing
4. ✅ Feature flags configured
5. ✅ Monitoring dashboards updated
6. ✅ Rollback plan documented

### Deployment Sequence

#### Step 1: Database Migration (Week 1, Day 1)
```powershell
# Run migration
dotnet ef migrations add AddDeviceManagement --project src/Listo.Notification.Infrastructure
dotnet ef database update --project src/Listo.Notification.API
```

#### Step 2: Deploy Notification Service (Week 1, Day 2)
1. Deploy Phase 1 changes (Device Management + Batch Endpoint)
2. Verify health endpoint
3. Test device registration manually
4. Test batch endpoint with curl/Postman

#### Step 3: Deploy Auth Service (Week 2, Day 1)
1. Update to use shared contracts
2. Update to use new internal API endpoints
3. Deploy with feature flag `UseNewNotificationAPI=false`
4. Gradually enable feature flag per tenant

#### Step 4: Deploy Orders Service (Week 2, Day 2)
1. Update to use shared contracts
2. Update batch notification calls
3. Deploy with feature flag `UseNewNotificationAPI=false`
4. Gradually enable feature flag per tenant

#### Step 5: Template Seeding (Week 2, Day 3)
```csharp
// Run seeding script for each tenant
await templateSeedData.SeedTemplatesAsync(tenantId);
```

#### Step 6: Enable Template-Based Flow (Week 2, Day 4-5)
1. Enable feature flag `UseTemplateBasedFlow=true`
2. Monitor error rates
3. Roll back if template not found errors spike

#### Step 7: Deploy Synchronous Delivery (Week 3, Day 1)
1. Deploy Notification service with sync delivery
2. Enable for SMS channel only
3. Monitor timeout rates

---

## Rollback Strategy

### Quick Rollback (< 5 minutes)
**Feature Flags:**
```json
{
  "UseNewNotificationAPI": false,
  "UseTemplateBasedFlow": false,
  "UseSynchronousDelivery": false
}
```

### Database Rollback (if needed)
```powershell
# Rollback migration
dotnet ef database update PreviousMigration --project src/Listo.Notification.API
```

### Service Rollback
1. Redeploy previous version from artifact
2. Update feature flags
3. Verify health endpoints
4. Monitor error rates for 15 minutes

---

## Monitoring & Alerts

### Metrics to Track
- Device registration rate
- Batch notification throughput
- Template rendering latency
- Synchronous delivery timeout rate
- FCM token invalidation rate

### Alerts
- Device registration failures > 5%
- Batch endpoint latency > 2 seconds
- Template not found errors > 10/minute
- Synchronous delivery timeout rate > 10%

---

## Success Criteria

### Phase 1
- ✅ 1000+ devices registered successfully
- ✅ Batch endpoint handles 100 notifications in < 2 seconds
- ✅ Push notifications deliver to all active devices
- ✅ Zero data loss during device token management

### Phase 2
- ✅ 90% of notifications use template-based flow
- ✅ Template rendering < 100ms average
- ✅ Zero template not found errors (with proper seeding)
- ✅ Backward compatibility maintained for 30 days

### Phase 3
- ✅ SMS synchronous delivery < 5 seconds average
- ✅ Timeout rate < 1%
- ✅ Auth service 2FA flow works seamlessly
- ✅ Zero failures due to sync delivery issues

---

## Timeline Summary

| Week | Phase | Tasks | Hours |
|------|-------|-------|-------|
| Week 1 | Device Management & Batch | Migration, Entities, Controllers, Batch Endpoint | 12 |
| Week 2 | Template-Based Flow | DTO Updates, Service Logic, Validators, Seeding | 4 |
| Week 3 | Synchronous Delivery | DTO Updates, Delivery Service, Controller, Tests | 5 |
| **Total** | | | **21 hours** |

---

## Next Steps

1. **Review this plan** with stakeholders
2. **Create GitHub Issues** for each task
3. **Set up feature flags** in configuration
4. **Prepare test environment** with sample data
5. **Schedule deployment windows** for each phase
6. **Start Week 1 development** on Device Management

---

## Questions or Concerns?

If you have any questions about this implementation plan, please reach out before starting development.

**Plan Author:** AI Assistant  
**Plan Status:** Ready for Review  
**Last Updated:** 2025-10-21
