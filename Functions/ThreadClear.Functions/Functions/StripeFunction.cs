using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Functions
{
    public class StripeFunction
    {
        private readonly ILogger _logger;
        private readonly StripeService _stripeService;
        private readonly string _webhookSecret;

        public StripeFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StripeFunction>();

            var stripeSecretKey = Environment.GetEnvironmentVariable("StripeSecretKey")
                ?? throw new InvalidOperationException("StripeSecretKey not configured");
            var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
                ?? throw new InvalidOperationException("SqlConnectionString not configured");
            _webhookSecret = Environment.GetEnvironmentVariable("StripeWebhookSecret") ?? "";

            _stripeService = new StripeService(stripeSecretKey, connectionString, loggerFactory.CreateLogger<StripeService>());
        }

        [Function("CreateCheckoutSession")]
        public async Task<HttpResponseData> CreateCheckoutSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/checkout")] HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<CheckoutRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || request.UserId == Guid.Empty || string.IsNullOrEmpty(request.PriceId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "UserId and PriceId required" });
                    return badRequest;
                }

                var checkoutUrl = await _stripeService.CreateCheckoutSession(
                    request.UserId,
                    request.Email,
                    request.PriceId,
                    request.SuccessUrl ?? "https://app.threadclear.com/settings?upgraded=true",
                    request.CancelUrl ?? "https://app.threadclear.com/settings?canceled=true"
                );

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, url = checkoutUrl });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout session");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to create checkout session" });
                return error;
            }
        }

        [Function("StripeWebhook")]
        public async Task<HttpResponseData> Webhook(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/webhook")] HttpRequestData req)
        {
            var json = await req.ReadAsStringAsync();
            var signature = req.Headers.GetValues("Stripe-Signature").FirstOrDefault() ?? "";

            var success = await _stripeService.HandleWebhook(json, signature, _webhookSecret);

            var response = req.CreateResponse(success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            return response;
        }
    }

    public class CheckoutRequest
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = "";
        public string PriceId { get; set; } = "";
        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
}