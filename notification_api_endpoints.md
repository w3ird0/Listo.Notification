# Notification & Communication Service API Endpoints

## Technology Stack
- **Backend**: .NET 9 with ASP.NET Core Web API
- **Database**: Microsoft SQL Server
- **Push Notifications**: Google Firebase Cloud Messaging (FCM)
- **SMS Service**: Twilio (recommended for reliability and features)
- **Email Service**: SendGrid or Azure Communication Services
- **Hosting**: Microsoft Azure
- **Authentication**: JWT Bearer tokens with Azure AD integration

## API Endpoints Structure

### 1. Push Notifications

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/notifications/push` | POST | Send push notification to specific users | ```json<br/>{<br/>  "userIds": ["user123", "user456"],<br/>  "title": "Order Update",<br/>  "body": "Your order has been accepted!",<br/>  "data": {<br/>    "orderId": "ORD-001",<br/>    "type": "ORDER_ACCEPTED",<br/>    "actionUrl": "/orders/ORD-001"<br/>  },<br/>  "priority": "high",<br/>  "timeToLive": 86400<br/>}``` |
| `/api/v1/notifications/push/broadcast` | POST | Send push notification to all users or specific segments | ```json<br/>{<br/>  "audience": "all", // or "drivers", "customers"<br/>  "title": "System Maintenance",<br/>  "body": "Scheduled maintenance tonight at 2 AM",<br/>  "data": {<br/>    "type": "SYSTEM_ANNOUNCEMENT",<br/>    "maintenanceStart": "2024-01-15T02:00:00Z"<br/>  },<br/>  "scheduledTime": "2024-01-15T01:00:00Z"<br/>}``` |
| `/api/v1/notifications/push/templates` | GET | Get available push notification templates | **Response:**<br/>```json<br/>{<br/>  "templates": [<br/>    {<br/>      "id": "order_accepted",<br/>      "title": "Order Accepted",<br/>      "body": "Your order #{orderId} has been accepted!",<br/>      "category": "order_updates"<br/>    }<br/>  ]<br/>}``` |
| `/api/v1/notifications/push/templates` | POST | Create new push notification template | ```json<br/>{<br/>  "templateId": "driver_arrived",<br/>  "title": "Driver Arrived",<br/>  "body": "Your driver {driverName} has arrived!",<br/>  "category": "delivery_updates",<br/>  "variables": ["driverName", "estimatedTime"]<br/>}``` |
| `/api/v1/notifications/push/history` | GET | Get push notification history | **Query Params:** `?userId=user123&page=1&limit=20&startDate=2024-01-01&endDate=2024-01-31`<br/>**Response:**<br/>```json<br/>{<br/>  "notifications": [<br/>    {<br/>      "id": "notif123",<br/>      "userId": "user123",<br/>      "title": "Order Update",<br/>      "body": "Your order has been delivered!",<br/>      "sentAt": "2024-01-15T10:30:00Z",<br/>      "status": "delivered",<br/>      "opened": true<br/>    }<br/>  ],<br/>  "pagination": {<br/>    "page": 1,<br/>    "limit": 20,<br/>    "total": 150<br/>  }<br/>}``` |
| `/api/v1/notifications/push/{notificationId}/status` | GET | Get specific notification delivery status | **Response:**<br/>```json<br/>{<br/>  "notificationId": "notif123",<br/>  "status": "delivered",<br/>  "sentAt": "2024-01-15T10:30:00Z",<br/>  "deliveredAt": "2024-01-15T10:30:15Z",<br/>  "openedAt": "2024-01-15T10:35:00Z",<br/>  "deviceInfo": {<br/>    "platform": "android",<br/>    "version": "12"<br/>  }<br/>}``` |

### 2. Device Management

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/devices/register` | POST | Register device for push notifications | ```json<br/>{<br/>  "userId": "user123",<br/>  "deviceToken": "fcm_token_here",<br/>  "platform": "android", // or "ios"<br/>  "deviceInfo": {<br/>    "model": "Samsung Galaxy S21",<br/>    "osVersion": "12",<br/>    "appVersion": "1.2.3"<br/>  },<br/>  "preferences": {<br/>    "orderUpdates": true,<br/>    "promotions": false,<br/>    "systemAnnouncements": true<br/>  }<br/>}``` |
| `/api/v1/devices/{deviceId}` | PUT | Update device registration | ```json<br/>{<br/>  "deviceToken": "new_fcm_token_here",<br/>  "preferences": {<br/>    "orderUpdates": true,<br/>    "promotions": true,<br/>    "systemAnnouncements": true<br/>  }<br/>}``` |
| `/api/v1/devices/{deviceId}` | DELETE | Unregister device | **No payload required** |
| `/api/v1/devices/user/{userId}` | GET | Get all devices for a user | **Response:**<br/>```json<br/>{<br/>  "devices": [<br/>    {<br/>      "deviceId": "dev123",<br/>      "platform": "android",<br/>      "lastSeen": "2024-01-15T10:30:00Z",<br/>      "active": true<br/>    }<br/>  ]<br/>}``` |

### 3. In-App Messaging

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/messages/conversations` | GET | Get user's conversations | **Query Params:** `?page=1&limit=20`<br/>**Response:**<br/>```json<br/>{<br/>  "conversations": [<br/>    {<br/>      "conversationId": "conv123",<br/>      "participants": [<br/>        {<br/>          "userId": "customer123",<br/>          "userType": "customer",<br/>          "name": "John Doe"<br/>        },<br/>        {<br/>          "userId": "driver456",<br/>          "userType": "driver",<br/>          "name": "Jane Smith"<br/>        }<br/>      ],<br/>      "lastMessage": {<br/>        "content": "I'm on my way!",<br/>        "sentAt": "2024-01-15T10:30:00Z",<br/>        "senderId": "driver456"<br/>      },<br/>      "unreadCount": 2,<br/>      "orderId": "ORD-001"<br/>    }<br/>  ]<br/>}``` |
| `/api/v1/messages/conversations` | POST | Create new conversation | ```json<br/>{<br/>  "participants": ["customer123", "driver456"],<br/>  "orderId": "ORD-001",<br/>  "type": "order_communication",<br/>  "initialMessage": {<br/>    "content": "Hi! I'm your driver for today's delivery.",<br/>    "messageType": "text"<br/>  }<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/messages` | GET | Get messages in a conversation | **Query Params:** `?page=1&limit=50&before=2024-01-15T10:30:00Z`<br/>**Response:**<br/>```json<br/>{<br/>  "messages": [<br/>    {<br/>      "messageId": "msg123",<br/>      "senderId": "driver456",<br/>      "content": "I'm on my way!",<br/>      "messageType": "text",<br/>      "sentAt": "2024-01-15T10:30:00Z",<br/>      "readBy": [<br/>        {<br/>          "userId": "customer123",<br/>          "readAt": "2024-01-15T10:31:00Z"<br/>        }<br/>      ],<br/>      "edited": false,<br/>      "metadata": {}<br/>    }<br/>  ]<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/messages` | POST | Send message in conversation | ```json<br/>{<br/>  "content": "Thank you! I'll be waiting outside.",<br/>  "messageType": "text", // or "image", "location", "file"<br/>  "replyToMessageId": "msg122",<br/>  "metadata": {<br/>    "priority": "normal"<br/>  }<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/messages/{messageId}/read` | PUT | Mark message as read | ```json<br/>{<br/>  "readAt": "2024-01-15T10:31:00Z"<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/typing` | POST | Send typing indicator | ```json<br/>{<br/>  "isTyping": true<br/>}``` |
| `/api/v1/messages/upload` | POST | Upload file/image for messaging | **Form Data:**<br/>- `file`: [binary file]<br/>- `conversationId`: "conv123"<br/>- `messageType`: "image"<br/><br/>**Response:**<br/>```json<br/>{<br/>  "fileId": "file123",<br/>  "url": "https://storage.azure.com/files/file123.jpg",<br/>  "thumbnailUrl": "https://storage.azure.com/thumbs/file123_thumb.jpg",<br/>  "fileSize": 1024576,<br/>  "mimeType": "image/jpeg"<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/participants` | POST | Add participant to conversation | ```json<br/>{<br/>  "userId": "support789",<br/>  "userType": "support_agent"<br/>}``` |
| `/api/v1/messages/conversations/{conversationId}/close` | PUT | Close/archive conversation | ```json<br/>{<br/>  "reason": "order_completed",<br/>  "summary": "Order delivered successfully"<br/>}``` |

### 4. SMS Gateway

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/sms/send` | POST | Send SMS message | ```json<br/>{<br/>  "phoneNumber": "+1234567890",<br/>  "message": "Your order ORD-001 has been confirmed!",<br/>  "templateId": "order_confirmation",<br/>  "variables": {<br/>    "orderId": "ORD-001",<br/>    "customerName": "John Doe"<br/>  },<br/>  "priority": "high",<br/>  "scheduledTime": "2024-01-15T10:30:00Z"<br/>}``` |
| `/api/v1/sms/bulk` | POST | Send bulk SMS messages | ```json<br/>{<br/>  "recipients": [<br/>    {<br/>      "phoneNumber": "+1234567890",<br/>      "variables": {<br/>        "customerName": "John Doe",<br/>        "orderId": "ORD-001"<br/>      }<br/>    },<br/>    {<br/>      "phoneNumber": "+1234567891",<br/>      "variables": {<br/>        "customerName": "Jane Smith",<br/>        "orderId": "ORD-002"<br/>      }<br/>    }<br/>  ],<br/>  "templateId": "order_confirmation",<br/>  "scheduledTime": "2024-01-15T10:30:00Z"<br/>}``` |
| `/api/v1/sms/templates` | GET | Get SMS templates | **Response:**<br/>```json<br/>{<br/>  "templates": [<br/>    {<br/>      "templateId": "order_confirmation",<br/>      "name": "Order Confirmation",<br/>      "message": "Hi {customerName}, your order {orderId} has been confirmed!",<br/>      "variables": ["customerName", "orderId"],<br/>      "category": "transactional"<br/>    }<br/>  ]<br/>}``` |
| `/api/v1/sms/templates` | POST | Create SMS template | ```json<br/>{<br/>  "templateId": "password_reset",<br/>  "name": "Password Reset",<br/>  "message": "Your password reset code is: {resetCode}. Valid for 10 minutes.",<br/>  "variables": ["resetCode"],<br/>  "category": "security",<br/>  "expiryMinutes": 10<br/>}``` |
| `/api/v1/sms/status/{messageId}` | GET | Get SMS delivery status | **Response:**<br/>```json<br/>{<br/>  "messageId": "sms123",<br/>  "phoneNumber": "+1234567890",<br/>  "status": "delivered", // sent, delivered, failed, undelivered<br/>  "sentAt": "2024-01-15T10:30:00Z",<br/>  "deliveredAt": "2024-01-15T10:30:15Z",<br/>  "cost": 0.0075,<br/>  "errorMessage": null<br/>}``` |
| `/api/v1/sms/history` | GET | Get SMS history | **Query Params:** `?phoneNumber=+1234567890&page=1&limit=20&startDate=2024-01-01`<br/>**Response:**<br/>```json<br/>{<br/>  "messages": [<br/>    {<br/>      "messageId": "sms123",<br/>      "phoneNumber": "+1234567890",<br/>      "message": "Your order has been confirmed!",<br/>      "status": "delivered",<br/>      "sentAt": "2024-01-15T10:30:00Z"<br/>    }<br/>  ],<br/>  "pagination": {<br/>    "page": 1,<br/>    "limit": 20,<br/>    "total": 500<br/>  }<br/>}``` |

### 5. Email Gateway

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/email/send` | POST | Send email | ```json<br/>{<br/>  "to": ["john.doe@example.com"],<br/>  "cc": ["manager@example.com"],<br/>  "bcc": ["audit@example.com"],<br/>  "subject": "Order Confirmation - ORD-001",<br/>  "templateId": "order_confirmation_email",<br/>  "variables": {<br/>    "customerName": "John Doe",<br/>    "orderId": "ORD-001",<br/>    "orderTotal": "$25.99",<br/>    "deliveryAddress": "123 Main St, City"<br/>  },<br/>  "attachments": [<br/>    {<br/>      "filename": "invoice.pdf",<br/>      "content": "base64_encoded_content",<br/>      "contentType": "application/pdf"<br/>    }<br/>  ],<br/>  "priority": "high",<br/>  "scheduledTime": "2024-01-15T10:30:00Z"<br/>}``` |
| `/api/v1/email/bulk` | POST | Send bulk emails | ```json<br/>{<br/>  "recipients": [<br/>    {<br/>      "email": "john.doe@example.com",<br/>      "variables": {<br/>        "customerName": "John Doe",<br/>        "orderId": "ORD-001"<br/>      }<br/>    }<br/>  ],<br/>  "templateId": "weekly_newsletter",<br/>  "subject": "Weekly Newsletter - {date}",<br/>  "globalVariables": {<br/>    "date": "January 15, 2024",<br/>    "companyName": "FoodDelivery Co."<br/>  }<br/>}``` |
| `/api/v1/email/templates` | GET | Get email templates | **Response:**<br/>```json<br/>{<br/>  "templates": [<br/>    {<br/>      "templateId": "order_confirmation_email",<br/>      "name": "Order Confirmation Email",<br/>      "subject": "Order Confirmation - {orderId}",<br/>      "variables": ["customerName", "orderId", "orderTotal"],<br/>      "category": "transactional",<br/>      "htmlContent": "<html>...</html>"<br/>    }<br/>  ]<br/>}``` |
| `/api/v1/email/templates` | POST | Create email template | ```json<br/>{<br/>  "templateId": "welcome_email",<br/>  "name": "Welcome Email",<br/>  "subject": "Welcome to {companyName}, {customerName}!",<br/>  "htmlContent": "<html><body>Welcome {customerName}!</body></html>",<br/>  "textContent": "Welcome {customerName}!",<br/>  "variables": ["customerName", "companyName"],<br/>  "category": "onboarding"<br/>}``` |
| `/api/v1/email/status/{messageId}` | GET | Get email delivery status | **Response:**<br/>```json<br/>{<br/>  "messageId": "email123",<br/>  "email": "john.doe@example.com",<br/>  "status": "delivered", // sent, delivered, opened, clicked, bounced, spam<br/>  "sentAt": "2024-01-15T10:30:00Z",<br/>  "deliveredAt": "2024-01-15T10:30:15Z",<br/>  "openedAt": "2024-01-15T10:35:00Z",<br/>  "clickedAt": "2024-01-15T10:36:00Z",<br/>  "bounceReason": null<br/>}``` |

### 6. Notification Preferences

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/preferences/user/{userId}` | GET | Get user notification preferences | **Response:**<br/>```json<br/>{<br/>  "userId": "user123",<br/>  "pushNotifications": {<br/>    "orderUpdates": true,<br/>    "promotions": false,<br/>    "systemAnnouncements": true<br/>  },<br/>  "emailNotifications": {<br/>    "orderConfirmations": true,<br/>    "newsletters": false,<br/>    "accountUpdates": true<br/>  },<br/>  "smsNotifications": {<br/>    "orderUpdates": true,<br/>    "securityAlerts": true,<br/>    "promotions": false<br/>  },<br/>  "quietHours": {<br/>    "enabled": true,<br/>    "startTime": "22:00",<br/>    "endTime": "08:00",<br/>    "timezone": "America/New_York"<br/>  }<br/>}``` |
| `/api/v1/preferences/user/{userId}` | PUT | Update user notification preferences | ```json<br/>{<br/>  "pushNotifications": {<br/>    "orderUpdates": true,<br/>    "promotions": true,<br/>    "systemAnnouncements": true<br/>  },<br/>  "emailNotifications": {<br/>    "orderConfirmations": true,<br/>    "newsletters": true,<br/>    "accountUpdates": true<br/>  },<br/>  "smsNotifications": {<br/>    "orderUpdates": true,<br/>    "securityAlerts": true,<br/>    "promotions": false<br/>  },<br/>  "quietHours": {<br/>    "enabled": true,<br/>    "startTime": "23:00",<br/>    "endTime": "07:00",<br/>    "timezone": "America/New_York"<br/>  }<br/>}``` |

### 7. Analytics & Reporting

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/analytics/notifications/stats` | GET | Get notification analytics | **Query Params:** `?startDate=2024-01-01&endDate=2024-01-31&type=push`<br/>**Response:**<br/>```json<br/>{<br/>  "period": {<br/>    "startDate": "2024-01-01",<br/>    "endDate": "2024-01-31"<br/>  },<br/>  "pushNotifications": {<br/>    "sent": 15000,<br/>    "delivered": 14500,<br/>    "opened": 8500,<br/>    "deliveryRate": 96.7,<br/>    "openRate": 58.6<br/>  },<br/>  "emails": {<br/>    "sent": 5000,<br/>    "delivered": 4900,<br/>    "opened": 2450,<br/>    "clicked": 490,<br/>    "deliveryRate": 98.0,<br/>    "openRate": 50.0,<br/>    "clickRate": 20.0<br/>  },<br/>  "sms": {<br/>    "sent": 2000,<br/>    "delivered": 1980,<br/>    "deliveryRate": 99.0,<br/>    "totalCost": 15.75<br/>  }<br/>}``` |
| `/api/v1/analytics/conversations/stats` | GET | Get messaging analytics | **Response:**<br/>```json<br/>{<br/>  "totalConversations": 1200,<br/>  "activeConversations": 150,<br/>  "averageResponseTime": "2m 30s",<br/>  "messageVolume": {<br/>    "daily": 500,<br/>    "weekly": 3500,<br/>    "monthly": 15000<br/>  },<br/>  "participantTypes": {<br/>    "customer": 45,<br/>    "driver": 40,<br/>    "support": 15<br/>  }<br/>}``` |

### 8. System Health & Monitoring

| Endpoint | HTTP Method | Description | Sample Payload |
|----------|-------------|-------------|----------------|
| `/api/v1/health` | GET | Health check endpoint | **Response:**<br/>```json<br/>{<br/>  "status": "healthy",<br/>  "timestamp": "2024-01-15T10:30:00Z",<br/>  "services": {<br/>    "database": "healthy",<br/>    "firebase": "healthy",<br/>    "sms": "healthy",<br/>    "email": "healthy",<br/>    "redis": "healthy"<br/>  },<br/>  "version": "1.2.3"<br/>}``` |
| `/api/v1/health/detailed` | GET | Detailed health check | **Response:**<br/>```json<br/>{<br/>  "status": "healthy",<br/>  "services": {<br/>    "database": {<br/>      "status": "healthy",<br/>      "responseTime": "15ms",<br/>      "connections": 25<br/>    },<br/>    "firebase": {<br/>      "status": "healthy",<br/>      "lastSuccessfulPush": "2024-01-15T10:29:00Z"<br/>    },<br/>    "sms": {<br/>      "status": "healthy",<br/>      "provider": "twilio",<br/>      "remainingBalance": 150.75<br/>    }<br/>  }<br/>}``` |

## Security Considerations

### Authentication & Authorization
- All endpoints require JWT Bearer token authentication
- Role-based access control (Customer, Driver, Support, Admin)
- API rate limiting: 100 requests/minute per user, 1000/minute for system accounts
- Request/Response encryption using HTTPS

### Data Protection
- Personal data encryption at rest and in transit
- GDPR compliant data handling
- Message content encryption for sensitive communications
- PII masking in logs and analytics

### Input Validation
- Request payload validation using Data Annotations
- SQL injection prevention through parameterized queries
- XSS protection for message content
- File upload validation and scanning

## Error Response Format

All endpoints return consistent error responses:
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid phone number format",
    "details": {
      "field": "phoneNumber",
      "value": "invalid_number",
      "constraint": "E.164 format required"
    },
    "timestamp": "2024-01-15T10:30:00Z",
    "traceId": "trace-12345"
  }
}
```

## Pagination Standard

All list endpoints support pagination:
```json
{
  "data": [...],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150,
    "hasNext": true,
    "hasPrevious": false
  }
}
```