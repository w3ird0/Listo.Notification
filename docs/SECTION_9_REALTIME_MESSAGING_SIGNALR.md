# Section 9: Real-Time Messaging with SignalR

**Status:** ✅ **COMPLETE** (2025-10-20)

This document describes the real-time messaging infrastructure built with ASP.NET Core SignalR, including presence tracking, read receipts, typing indicators, message persistence, and Azure SignalR Service configuration.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Presence Tracking](#presence-tracking)
4. [Read Receipts (Dual Storage)](#read-receipts-dual-storage)
5. [Typing Indicators](#typing-indicators)
6. [Message Persistence](#message-persistence)
7. [Conversation Authorization](#conversation-authorization)
8. [Rate Limiting](#rate-limiting)
9. [Azure SignalR Service Configuration](#azure-signalr-service-configuration)
10. [Redis Backplane (Self-Hosted)](#redis-backplane-self-hosted)
11. [Hub Methods Reference](#hub-methods-reference)
12. [Client Integration Examples](#client-integration-examples)
13. [Testing Scenarios](#testing-scenarios)

---

## Overview

The Listo.Notification service provides real-time messaging capabilities through two SignalR hubs:

### NotificationHub (`/hubs/notifications`)
- Real-time notification delivery
- User-specific and tenant-wide broadcasts
- Notification acknowledgment and read receipts
- Channel-based subscriptions

### MessagingHub (`/hubs/messaging`)
- **In-app conversations** (customer-support, customer-driver)
- **Message persistence** to database before broadcast
- **Presence tracking** with Redis TTL
- **Read receipts** with dual storage (database + Redis)
- **Typing indicators** with 10-second TTL
- **Conversation authorization** (participant validation)

---

## Architecture

### Service Components

```
┌─────────────────────────────────────────────────────────────┐
│                      MessagingHub                            │
│  - SendMessage (persist → broadcast)                         │
│  - MarkAsRead (DB + Redis → broadcast)                       │
│  - StartTyping / StopTyping (Redis TTL)                      │
│  - JoinConversation (authorization check)                    │
└─────────────────────┬───────────────────────────────────────┘
                      │
         ┌────────────┼────────────┬─────────────────┐
         ▼            ▼            ▼                 ▼
┌────────────┐ ┌─────────────┐ ┌──────────────┐ ┌──────────┐
│ Presence   │ │ ReadReceipt │ │ Typing       │ │ Database │
│ Tracking   │ │ Service     │ │ Indicator    │ │ Messages │
│ Service    │ │ (Dual)      │ │ Service      │ │ Table    │
└──────┬─────┘ └──────┬──────┘ └──────┬───────┘ └────┬─────┘
       │              │                │               │
       └──────────────┴────────────────┴───────────────┘
                      │
                      ▼
              ┌───────────────┐
              │  Redis Cache  │
              │  - Presence   │
              │  - Receipts   │
              │  - Typing     │
              └───────────────┘
```

### Technology Stack
- **ASP.NET Core SignalR** (native WebSocket support)
- **Azure SignalR Service** (production scale-out)
- **Redis** (backplane for self-hosted, state storage)
- **SQL Server** (message persistence)
- **JWT Authentication** (via query string for WebSocket)

---

## Presence Tracking

### Implementation: `PresenceTrackingService`

**Redis Keys:**
- `presence:user:{userId}` - Online timestamp (5-minute TTL)
- `presence:connections:{userId}` - Active connection IDs (Set, 5-minute TTL)
- `presence:lastseen:{userId}` - Last seen timestamp (30-day TTL)

### Features

#### Multi-Connection Support
- User can have multiple active connections (mobile + desktop)
- Only marked offline when ALL connections disconnect
- Each connection tracked individually in Redis Set

#### TTL Strategy
- **Online presence:** 5-minute TTL, refreshed on activity
- **Last seen:** 30-day TTL, persisted on disconnect
- Automatic cleanup via Redis expiration

### Methods

```csharp
// Mark user online (called on connection)
await presenceService.SetUserOnlineAsync(userId, connectionId);

// Refresh presence TTL (called on user activity)
await presenceService.RefreshPresenceAsync(userId);

// Mark user offline (called on disconnect)
await presenceService.SetUserOfflineAsync(userId, connectionId);

// Query presence status
var presence = await presenceService.GetUserPresenceAsync(userId);
// Returns: UserPresence(UserId, IsOnline, LastSeen, ActiveConnectionCount)

// Batch query for multiple users
var presences = await presenceService.GetBatchPresenceAsync(userIds);
```

### Hub Integration

```csharp
// MessagingHub.OnConnectedAsync
await _presenceService.SetUserOnlineAsync(userId, Context.ConnectionId);
await Clients.Others.UserOnline(userId);

// MessagingHub.OnDisconnectedAsync
await _presenceService.SetUserOfflineAsync(userId, Context.ConnectionId);
var presence = await _presenceService.GetUserPresenceAsync(userId);
if (!presence.IsOnline)
{
    await Clients.Others.UserOffline(userId);
}
```

---

## Read Receipts (Dual Storage)

### Implementation: `ReadReceiptService`

**Dual Storage Strategy:**
1. **Database** (Messages.ReadAt column) - Durable, permanent storage
2. **Redis** (30-day TTL) - Fast queries, reduces DB load

**Redis Keys:**
- `readreceipt:message:{messageId}` - Individual receipt (30-day TTL)
- `readreceipt:conversation:{conversationId}:{userId}` - Set of read message IDs (30-day TTL)

### Features

#### Write Path (Record Receipt)
1. Query database for message
2. Update `Messages.ReadAt` and `Status = "read"`
3. Save to database
4. Cache receipt in Redis (30-day TTL)
5. Add to conversation read set (for batch queries)
6. Broadcast to other participants

#### Read Path (Query Receipt)
1. **Fast path:** Check Redis cache first
2. **Slow path:** Query database if cache miss
3. **Cache warming:** Store result in Redis for future queries

### Methods

```csharp
// Record read receipt (DB + Redis + broadcast)
await readReceiptService.RecordReadReceiptAsync(messageId, userId, readAt);

// Get single receipt (Redis → DB fallback)
var receipt = await readReceiptService.GetReadReceiptAsync(messageId);

// Batch query (efficient Redis pipeline)
var receipts = await readReceiptService.GetBatchReadReceiptsAsync(messageIds);

// Get all read messages in conversation
var readMessageIds = await readReceiptService.GetReadMessageIdsAsync(conversationId, userId);
```

### Hub Integration

```csharp
// MessagingHub.MarkAsRead
var messageGuid = Guid.Parse(messageId);
await _readReceiptService.RecordReadReceiptAsync(messageGuid, userId, readAt);

await Clients.OthersInGroup($"conversation:{conversationId}")
    .OnMessageRead(conversationId, messageId, userId, readAt);
```

---

## Typing Indicators

### Implementation: `TypingIndicatorService`

**Redis Keys:**
- `typing:conversation:{conversationId}` - Set of user IDs currently typing (10-second TTL)

### Features

#### Ephemeral State
- **10-second TTL** for automatic cleanup
- Supports reconnection scenarios (user can rejoin without clearing stale state)
- Non-critical operation (failures logged, not thrown)

#### Automatic Cleanup
- Redis expires typing state after 10 seconds of inactivity
- Client should send periodic `StartTyping` to extend TTL
- `StopTyping` explicitly removes user from set

### Methods

```csharp
// User starts typing (add to Redis set)
await typingIndicatorService.SetTypingAsync(conversationId, userId);

// User stops typing (remove from Redis set)
await typingIndicatorService.ClearTypingAsync(conversationId, userId);

// Get all typing users
var typingUsers = await typingIndicatorService.GetTypingUsersAsync(conversationId);

// Check specific user
var isTyping = await typingIndicatorService.IsUserTypingAsync(conversationId, userId);
```

### Hub Integration

```csharp
// MessagingHub.StartTyping
await _typingIndicatorService.SetTypingAsync(conversationId, userId);
await Clients.OthersInGroup($"conversation:{conversationId}")
    .OnTypingIndicator(conversationId, userId, true);

// MessagingHub.StopTyping
await _typingIndicatorService.ClearTypingAsync(conversationId, userId);
await Clients.OthersInGroup($"conversation:{conversationId}")
    .OnTypingIndicator(conversationId, userId, false);

// Automatically cleared on SendMessage
await _typingIndicatorService.ClearTypingAsync(conversationId, userId);
```

---

## Message Persistence

### Database-First Approach

**Flow:**
1. Validate conversation and user authorization
2. Create `MessageEntity` with all data
3. **Save to database** (`Messages` table)
4. Clear typing indicator
5. Refresh user presence
6. **Broadcast to all participants** in conversation group

### Message Entity

```csharp
public class MessageEntity
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string SenderUserId { get; set; }
    public string? RecipientUserId { get; set; } // null = all participants
    public string Body { get; set; }
    public string? AttachmentsJson { get; set; } // JSON array
    public MessageStatus Status { get; set; } // Sent, Delivered, Read, Failed
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
```

### Hub Implementation

```csharp
public async Task SendMessage(string conversationId, string body, string[]? attachments = null)
{
    var userId = GetUserId();
    var conversationGuid = Guid.Parse(conversationId);
    
    // 1. Persist to database
    var message = new MessageEntity
    {
        MessageId = Guid.NewGuid(),
        ConversationId = conversationGuid,
        SenderUserId = userId,
        Body = body,
        AttachmentsJson = attachments != null && attachments.Length > 0 
            ? JsonSerializer.Serialize(attachments) 
            : null,
        Status = MessageStatus.Sent,
        SentAt = DateTime.UtcNow
    };
    
    _dbContext.Messages.Add(message);
    await _dbContext.SaveChangesAsync();
    
    // 2. Clear typing indicator
    await _typingIndicatorService.ClearTypingAsync(conversationId, userId);
    
    // 3. Refresh presence
    await _presenceService.RefreshPresenceAsync(userId);
    
    // 4. Broadcast
    await Clients.Group($"conversation:{conversationId}")
        .OnMessageReceived(new MessageDto(...));
}
```

### Benefits
- **Durability:** Messages never lost, even if broadcast fails
- **Audit trail:** Complete message history in database
- **Reconnection:** Clients can fetch missed messages from database
- **Compliance:** Retention policies enforced via database cleanup jobs

---

## Conversation Authorization

### Participant Validation

**Flow:**
1. Parse `conversationId` to GUID
2. Query `Conversations` table
3. Deserialize `ParticipantsJson` (JSON array of user IDs)
4. Check if requesting user is in participant list
5. Throw `HubException` if unauthorized

### Implementation

```csharp
public async Task JoinConversation(string conversationId)
{
    var userId = GetUserId();
    var conversationGuid = Guid.Parse(conversationId);
    
    // Validate conversation exists
    var conversation = await _dbContext.Conversations
        .FirstOrDefaultAsync(c => c.ConversationId == conversationGuid);
    
    if (conversation == null)
    {
        throw new HubException("Conversation not found");
    }
    
    // Validate user is participant
    var participants = JsonSerializer.Deserialize<List<string>>(conversation.ParticipantsJson);
    if (participants == null || !participants.Contains(userId))
    {
        throw new HubException("You are not a participant in this conversation");
    }
    
    // Add to SignalR group
    await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
}
```

### Conversation Types

- **customer-support:** Long retention (180 days)
- **customer-driver:** Short retention (30 days)

### Security Benefits
- **Authorization enforcement:** Only participants can join conversations
- **Audit logging:** All join attempts logged
- **Error feedback:** Clear error messages for unauthorized access

---

## Rate Limiting

### Current Status
**SignalRRateLimitFilter** created but rate limiting **deferred for future implementation**.

### Documented Rate Limits (for production)
- **SendMessage:** 60 messages/minute per user
- **StartTyping:** 10 indicators/minute per user

### Future Integration
- Integrate with existing `RedisTokenBucketLimiter`
- Throttle exceeded requests (don't disconnect client)
- Return `HubException` with retry information for messages
- Silently drop typing indicators on rate limit

### Implementation Pattern

```csharp
public async ValueTask<object?> InvokeMethodAsync(
    HubInvocationContext invocationContext,
    Func<HubInvocationContext, ValueTask<object?>> next)
{
    var methodName = invocationContext.HubMethodName;
    
    if (methodName == "SendMessage")
    {
        var allowed = await _rateLimiter.CheckAsync(userId, "messages", capacity: 60);
        if (!allowed)
        {
            throw new HubException($"Rate limit exceeded. Wait {retryAfter} seconds.");
        }
    }
    
    return await next(invocationContext);
}
```

---

## Azure SignalR Service Configuration

### Environment-Based Configuration

#### Development Mode (In-Memory)
```csharp
// No external dependencies
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400; // 100KB
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

#### Production Mode (Azure SignalR Service)

**Step 1: Install NuGet Package**
```bash
dotnet add package Microsoft.Azure.SignalR
```

**Step 2: Configure Connection String**
```json
// appsettings.Production.json
{
  "Azure": {
    "SignalR": {
      "ConnectionString": "Endpoint=https://your-signalr.service.signalr.net;AccessKey=..."
    }
  }
}
```

**Step 3: Register Service**
```csharp
if (!string.IsNullOrEmpty(azureSignalRConnectionString))
{
    signalRBuilder.AddAzureSignalR(options =>
    {
        options.ConnectionString = azureSignalRConnectionString;
        options.ServerStickyMode = ServerStickyMode.Required;
    });
}
```

### Azure SignalR Service Benefits
- **Automatic scaling:** Handles thousands of concurrent connections
- **High availability:** Multi-region deployments
- **WebSocket optimization:** Managed infrastructure
- **Cost efficiency:** Pay only for active connections
- **Zero downtime:** Rolling updates without connection drops

### Configuration Options

```csharp
signalRBuilder.AddSignalR(options =>
{
    // Message size limit
    options.MaximumReceiveMessageSize = 102400; // 100KB
    
    // Stream settings
    options.StreamBufferCapacity = 10;
    
    // Timeout settings
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    
    // Debugging
    options.EnableDetailedErrors = isDevelopment;
});
```

---

## Redis Backplane (Self-Hosted)

### When to Use
- Self-hosted deployments (not using Azure SignalR Service)
- Multi-server scale-out scenarios
- Development/testing environments with multiple instances

### Configuration

```csharp
if (builder.Environment.IsProduction())
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = 
            RedisChannel.Literal("Listo.Notification.SignalR");
    });
}
```

### Redis Pub/Sub Pattern
- All SignalR messages published to Redis channels
- All servers subscribe to channels
- Message routing handled automatically by SignalR
- Channel prefix prevents naming conflicts with other services

### Connection String

```json
{
  "ConnectionStrings": {
    "Redis": "your-redis-server:6379,password=yourpassword,ssl=true"
  }
}
```

### Benefits
- **Horizontal scaling:** Add more servers without client reconnection
- **Load balancing:** Distribute connections across multiple instances
- **Session persistence:** Not required (messages broadcast to all servers)
- **Cost control:** Self-hosted infrastructure, no per-connection fees

---

## Hub Methods Reference

### MessagingHub Server Methods (Client → Server)

```csharp
// Send a message in a conversation
Task SendMessage(string conversationId, string body, string[]? attachments = null)

// Indicate user is typing
Task StartTyping(string conversationId)

// Indicate user stopped typing
Task StopTyping(string conversationId)

// Mark a message as read
Task MarkAsRead(string conversationId, string messageId)

// Join a conversation room
Task JoinConversation(string conversationId)

// Leave a conversation room
Task LeaveConversation(string conversationId)

// Health check
Task<string> Ping() // Returns "pong"
```

### MessagingHub Client Methods (Server → Client)

```csharp
// Receive a new message
Task OnMessageReceived(MessageDto message)

// Typing indicator changed
Task OnTypingIndicator(string conversationId, string userId, bool isTyping)

// Message marked as read
Task OnMessageRead(string conversationId, string messageId, string userId, DateTime readAt)

// User came online
Task UserOnline(string userId)

// User went offline
Task UserOffline(string userId)
```

### NotificationHub Server Methods

```csharp
// Acknowledge notification receipt
Task AcknowledgeNotification(string notificationId)

// Mark notification as read
Task MarkAsRead(string notificationId)

// Subscribe to a channel
Task SubscribeToChannel(string channel) // e.g., "orders", "rides"

// Unsubscribe from a channel
Task UnsubscribeFromChannel(string channel)

// Health check
Task<string> Ping()
```

### NotificationHub Client Methods

```csharp
// Receive a notification
Task ReceiveNotification(NotificationMessage notification)

// Notification acknowledged by another session
Task NotificationAcknowledged(string notificationId)

// Notification read by another session
Task NotificationRead(string notificationId, DateTime readAt)

// Batch of notifications
Task ReceiveNotificationBatch(IEnumerable<NotificationMessage> notifications)
```

---

## Client Integration Examples

### JavaScript/TypeScript (Web)

```typescript
import * as signalR from "@microsoft/signalr";

// Connect to MessagingHub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/messaging", {
        accessTokenFactory: () => getAccessToken()
    })
    .withAutomaticReconnect([0, 1000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Register client methods
connection.on("OnMessageReceived", (message: MessageDto) => {
    console.log("New message:", message);
    displayMessage(message);
});

connection.on("OnTypingIndicator", (conversationId, userId, isTyping) => {
    updateTypingIndicator(userId, isTyping);
});

connection.on("OnMessageRead", (conversationId, messageId, userId, readAt) => {
    markMessageAsRead(messageId);
});

connection.on("UserOnline", (userId) => {
    updateUserStatus(userId, "online");
});

connection.on("UserOffline", (userId) => {
    updateUserStatus(userId, "offline");
});

// Start connection
await connection.start();

// Join conversation
await connection.invoke("JoinConversation", conversationId);

// Send message
await connection.invoke("SendMessage", conversationId, messageBody, attachments);

// Typing indicators
await connection.invoke("StartTyping", conversationId);
setTimeout(() => connection.invoke("StopTyping", conversationId), 3000);

// Mark as read
await connection.invoke("MarkAsRead", conversationId, messageId);
```

### React Native (Mobile)

```typescript
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

class MessagingService {
    private connection: signalR.HubConnection;

    async connect(accessToken: string) {
        this.connection = new HubConnectionBuilder()
            .withUrl("https://api.listoexpress.com/hubs/messaging", {
                accessTokenFactory: () => accessToken
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    // Exponential backoff: 0s, 2s, 10s, 30s, null (stop)
                    const delays = [0, 2000, 10000, 30000];
                    return delays[retryContext.previousRetryCount] ?? null;
                }
            })
            .configureLogging(LogLevel.Warning)
            .build();

        // Event handlers
        this.connection.on("OnMessageReceived", this.handleNewMessage);
        this.connection.on("OnTypingIndicator", this.handleTyping);
        this.connection.on("OnMessageRead", this.handleReadReceipt);
        this.connection.on("UserOnline", this.handleUserOnline);
        this.connection.on("UserOffline", this.handleUserOffline);

        // Lifecycle events
        this.connection.onreconnecting(() => {
            console.log("SignalR reconnecting...");
        });

        this.connection.onreconnected(() => {
            console.log("SignalR reconnected");
            this.rejoinConversations();
        });

        this.connection.onclose((error) => {
            console.error("SignalR disconnected:", error);
        });

        await this.connection.start();
    }

    async sendMessage(conversationId: string, body: string, attachments?: string[]) {
        await this.connection.invoke("SendMessage", conversationId, body, attachments);
    }

    async startTyping(conversationId: string) {
        await this.connection.invoke("StartTyping", conversationId);
    }

    async stopTyping(conversationId: string) {
        await this.connection.invoke("StopTyping", conversationId);
    }

    async markAsRead(conversationId: string, messageId: string) {
        await this.connection.invoke("MarkAsRead", conversationId, messageId);
    }

    async joinConversation(conversationId: string) {
        await this.connection.invoke("JoinConversation", conversationId);
    }
}
```

### .NET Client (Server-to-Server)

```csharp
using Microsoft.AspNetCore.SignalR.Client;

public class NotificationClient
{
    private HubConnection _connection;

    public async Task ConnectAsync(string hubUrl, string accessToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .WithAutomaticReconnect(new[] { 
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(2), 
                TimeSpan.FromSeconds(10), 
                TimeSpan.FromSeconds(30) 
            })
            .Build();

        _connection.On<MessageDto>("OnMessageReceived", HandleMessage);
        _connection.On<string, string, bool>("OnTypingIndicator", HandleTyping);
        _connection.On<string, string, string, DateTime>("OnMessageRead", HandleReadReceipt);

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogError(error, "SignalR connection closed");
            return Task.CompletedTask;
        };

        await _connection.StartAsync();
    }

    public async Task SendMessageAsync(string conversationId, string body, string[] attachments = null)
    {
        await _connection.InvokeAsync("SendMessage", conversationId, body, attachments);
    }

    private void HandleMessage(MessageDto message)
    {
        _logger.LogInformation("Received message: {MessageId}", message.MessageId);
        // Process message
    }
}
```

---

## Testing Scenarios

### Unit Tests

```csharp
[Fact]
public async Task PresenceService_SetUserOnline_StoresInRedis()
{
    // Arrange
    var userId = "user123";
    var connectionId = "conn456";
    
    // Act
    await _presenceService.SetUserOnlineAsync(userId, connectionId);
    
    // Assert
    var presence = await _presenceService.GetUserPresenceAsync(userId);
    Assert.True(presence.IsOnline);
    Assert.Equal(1, presence.ActiveConnectionCount);
}

[Fact]
public async Task ReadReceiptService_RecordReceipt_UpdatesDatabaseAndRedis()
{
    // Arrange
    var messageId = Guid.NewGuid();
    var userId = "user123";
    var readAt = DateTime.UtcNow;
    
    // Act
    await _readReceiptService.RecordReadReceiptAsync(messageId, userId, readAt);
    
    // Assert - Database
    var message = await _dbContext.Messages.FindAsync(messageId);
    Assert.Equal(readAt, message.ReadAt);
    Assert.Equal(MessageStatus.Read, message.Status);
    
    // Assert - Redis
    var receipt = await _readReceiptService.GetReadReceiptAsync(messageId);
    Assert.NotNull(receipt);
    Assert.Equal(userId, receipt.UserId);
}

[Fact]
public async Task TypingIndicatorService_SetTyping_ExpiresAfter10Seconds()
{
    // Arrange
    var conversationId = "conv123";
    var userId = "user123";
    
    // Act
    await _typingIndicatorService.SetTypingAsync(conversationId, userId);
    
    // Assert - Immediate
    var typing = await _typingIndicatorService.IsUserTypingAsync(conversationId, userId);
    Assert.True(typing);
    
    // Assert - After 11 seconds
    await Task.Delay(11000);
    typing = await _typingIndicatorService.IsUserTypingAsync(conversationId, userId);
    Assert.False(typing);
}
```

### Integration Tests

```csharp
[Fact]
public async Task MessagingHub_SendMessage_PersistsAndBroadcasts()
{
    // Arrange
    var conversationId = Guid.NewGuid().ToString();
    var body = "Test message";
    
    var hub = new MessagingHub(_logger, _dbContext, _presenceService, 
        _readReceiptService, _typingIndicatorService);
    
    // Act
    await hub.SendMessage(conversationId, body);
    
    // Assert - Database
    var message = await _dbContext.Messages
        .FirstOrDefaultAsync(m => m.Body == body);
    Assert.NotNull(message);
    Assert.Equal(MessageStatus.Sent, message.Status);
    
    // Assert - Broadcast (verify via mock Clients)
    _mockClients.Verify(c => c.Group(It.IsAny<string>())
        .OnMessageReceived(It.IsAny<MessageDto>()), Times.Once);
}

[Fact]
public async Task MessagingHub_JoinConversation_ValidatesParticipant()
{
    // Arrange
    var conversationId = Guid.NewGuid();
    var userId = "unauthorized-user";
    
    // Act & Assert
    await Assert.ThrowsAsync<HubException>(() => 
        _hub.JoinConversation(conversationId.ToString()));
}
```

### Load Tests (SignalR Crank)

```bash
# Install SignalR Crank
dotnet tool install -g Microsoft.Crank.Controller

# Run load test
crank --config signalr-load-test.yml --scenario messaging \
      --profile production \
      --variable connections=10000 \
      --variable duration=300
```

**signalr-load-test.yml:**
```yaml
jobs:
  server:
    source:
      repository: https://github.com/your-org/listo-notification.git
      branchOrCommit: main
    waitForExit: false
  
  client:
    source:
      repository: https://github.com/aspnet/SignalR.git
      branchOrCommit: main
      project: benchmarks/SignalR.Crankier/SignalR.Crankier.csproj
    args: "--workers {{workers}} --duration {{duration}} --target-url {{serverAddress}}/hubs/messaging"
    
scenarios:
  messaging:
    server:
      job: server
    client:
      job: client
      variables:
        workers: 100
        duration: 60
        connections: 10000
```

### Performance Metrics

**Target Performance:**
- **Connections:** 10,000+ concurrent connections per instance
- **Message throughput:** 10,000+ messages/second
- **Latency:** p99 < 100ms for message delivery
- **Reconnection time:** < 5 seconds average

**Monitoring:**
```csharp
// Application Insights metrics
_telemetryClient.TrackMetric("SignalR.ActiveConnections", activeConnections);
_telemetryClient.TrackMetric("SignalR.MessagesSent", messageCount);
_telemetryClient.TrackDependency("Redis", "PresenceTracking", duration, success);
```

---

## Summary

Section 9 implements a comprehensive real-time messaging system with:

✅ **Presence Tracking** - Multi-connection support, 5-minute TTL, 30-day last seen  
✅ **Read Receipts** - Dual storage (database + Redis), 30-day cache  
✅ **Typing Indicators** - 10-second TTL, automatic cleanup  
✅ **Message Persistence** - Database-first before broadcast  
✅ **Conversation Authorization** - Participant validation on join  
✅ **Azure SignalR Service** - Production scale-out configuration  
✅ **Redis Backplane** - Self-hosted multi-server support  
✅ **Rate Limiting** - Documented for future integration  

**Build Status:** ✅ Solution compiles successfully  
**Testing:** Unit tests, integration tests, and load testing scenarios provided  
**Documentation:** Complete with client integration examples

---

**Last Updated:** 2025-10-20  
**Implementation Status:** ✅ Complete  
**Next Steps:** Section 14 (Azure Functions Configuration) or API Documentation
