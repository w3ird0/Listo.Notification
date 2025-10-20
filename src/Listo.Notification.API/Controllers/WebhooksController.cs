// TODO: Implement webhook handlers in Infrastructure layer before enabling this controller
/*
using Listo.Notification.Application.DTOs;
using Listo.Notification.Infrastructure.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Handles webhook callbacks from notification providers (Twilio, SendGrid, FCM).
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous] // Webhooks use signature validation instead
public class WebhooksController : ControllerBase
{
    private readonly TwilioWebhookHandler _twilioHandler;
    private readonly SendGridWebhookHandler _sendGridHandler;
    private readonly FcmWebhookHandler _fcmHandler;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        TwilioWebhookHandler twilioHandler,
        SendGridWebhookHandler sendGridHandler,
        FcmWebhookHandler fcmHandler,
        ILogger<WebhooksController> logger)
    {
        _twilioHandler = twilioHandler;
        _sendGridHandler = sendGridHandler;
        _fcmHandler = fcmHandler;
        _logger = logger;
    }

    /// <summary>
    /// Twilio SMS status callback.
    /// </summary>
    [HttpPost("twilio/status")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TwilioStatus([FromForm] TwilioStatusWebhook webhook)
    {
        try
        {
            _logger.LogInformation("Received Twilio webhook: MessageSid={MessageSid}, Status={Status}", 
                webhook.MessageSid, webhook.MessageStatus);

            await _twilioHandler.HandleAsync(webhook, HttpContext.Request);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Twilio webhook signature validation failed");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Twilio webhook");
            return Ok(); // Return 200 to prevent retries
        }
    }

    /// <summary>
    /// SendGrid email events webhook.
    /// </summary>
    [HttpPost("sendgrid/events")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendGridEvents([FromBody] SendGridEventWebhook[] events)
    {
        try
        {
            _logger.LogInformation("Received SendGrid webhook: EventCount={EventCount}", events.Length);

            await _sendGridHandler.HandleAsync(events, HttpContext.Request);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "SendGrid webhook signature validation failed");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SendGrid webhook");
            return Ok(); // Return 200 to prevent retries
        }
    }

    /// <summary>
    /// FCM push notification delivery status.
    /// </summary>
    [HttpPost("fcm/delivery-status")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> FcmDeliveryStatus([FromBody] FcmDeliveryWebhook webhook)
    {
        try
        {
            _logger.LogInformation("Received FCM webhook: MessageId={MessageId}, Status={Status}", 
                webhook.MessageId, webhook.Status);

            await _fcmHandler.HandleAsync(webhook);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing FCM webhook");
            return Ok(); // Return 200 to prevent retries
        }
    }
}
*/
