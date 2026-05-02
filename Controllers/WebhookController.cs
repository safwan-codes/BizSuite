using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BizSuite.Services;
using BizSuite.Data;
using BizSuite.Repositories;
using System;

namespace BizSuite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<WebhookController> _logger;
        private readonly IShippingService _shippingService;
        private readonly IFulfillmentRepository _fulfillmentRepo;
        private readonly DbHelper _db;

        public WebhookController(IConfiguration config, ILogger<WebhookController> logger, IShippingService shippingService, IFulfillmentRepository fulfillmentRepo, DbHelper db)
        {
            _config = config;
            _logger = logger;
            _shippingService = shippingService;
            _fulfillmentRepo = fulfillmentRepo;
            _db = db;
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var endpointSecret = _config["Stripe:WebhookSecret"];

            try
            {
                // In local testing, you must use stripe CLI to forward events:
                // stripe listen --forward-to localhost:yourport/api/webhook/stripe
                
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], endpointSecret);

                // Handle the event
                int logId = _fulfillmentRepo.LogWebhookEvent("Stripe", stripeEvent.Type, json, null);

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    _logger.LogInformation("Payment for {Amount} succeeded.", paymentIntent.Amount);
                    
                    if (paymentIntent.Metadata.ContainsKey("OrderId") && int.TryParse(paymentIntent.Metadata["OrderId"], out int orderId)) 
                    { 
                        _fulfillmentRepo.UpdateWebhookStatus(logId, "Success");
                        _fulfillmentRepo.UpdateOrderFulfillmentStatus(orderId, "Processing");
                    }
                }
                else
                {
                     _fulfillmentRepo.UpdateWebhookStatus(logId, "Skipped");
                }
                
                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Stripe Webhook Failed");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server error handling stripe webhook");
                return StatusCode(500);
            }
        }

        [HttpPost("easypost")]
        public async Task<IActionResult> EasyPostWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            
            try
            {
                // Parse the EasyPost webhook event
                int logId = _fulfillmentRepo.LogWebhookEvent("EasyPost", "tracking_update", json, null);

                // e.g., if tracker becomes 'delivered', update database Status
                // Typically you parse: EasyPost.Models.API.Event epEvent = EasyPost.Client.Event.Retrieve(json); 
                // and extract custom data (OrderId) to pass to generic UpdateOrderFulfillmentStatus().
                
                _fulfillmentRepo.UpdateWebhookStatus(logId, "Success");
                _logger.LogInformation("EasyPost Webhook received.");

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server error handling EasyPost webhook");
                return StatusCode(500);
            }
        }
    }
}
