using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace ThreadClear.Functions.Services.Implementations
{
    public class StripeService
    {
        private readonly ILogger<StripeService> _logger;
        private readonly string _connectionString;

        public StripeService(string stripeSecretKey, string connectionString, ILogger<StripeService> logger)
        {
            StripeConfiguration.ApiKey = stripeSecretKey;
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<string> CreateCheckoutSession(Guid userId, string userEmail, string priceId, string successUrl, string cancelUrl)
        {
            var options = new SessionCreateOptions
            {
                CustomerEmail = userEmail,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                },
                Mode = "subscription",
                SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("Created checkout session {SessionId} for user {UserId}", session.Id, userId);

            return session.Url;
        }

        public async Task<bool> HandleWebhook(string json, string stripeSignature, string webhookSecret)
        {
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        var session = stripeEvent.Data.Object as Session;
                        await HandleCheckoutCompleted(session);
                        break;

                    case "customer.subscription.deleted":
                        var subscription = stripeEvent.Data.Object as Subscription;
                        await HandleSubscriptionCanceled(subscription);
                        break;

                    case "invoice.payment_failed":
                        var invoice = stripeEvent.Data.Object as Invoice;
                        await HandlePaymentFailed(invoice);
                        break;
                }

                return true;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                return false;
            }
        }

        private async Task HandleCheckoutCompleted(Session session)
        {
            if (session?.Metadata == null || !session.Metadata.ContainsKey("userId"))
            {
                _logger.LogWarning("Checkout session missing userId metadata");
                return;
            }

            var userId = Guid.Parse(session.Metadata["userId"]);
            var subscriptionId = session.SubscriptionId;

            // Get the subscription to find the price/tier
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId);
            var priceId = subscription.Items.Data[0].Price.Id;

            // Look up tier from price
            var tier = await GetTierFromPriceId(priceId);

            // Update user's plan
            await UpdateUserPlan(userId, tier, session.CustomerId, subscriptionId);

            _logger.LogInformation("User {UserId} upgraded to {Tier}", userId, tier);
        }

        private async Task HandleSubscriptionCanceled(Subscription subscription)
        {
            var customerId = subscription.CustomerId;

            // Find user by Stripe customer ID and downgrade to free
            await DowngradeUserByCustomerId(customerId);

            _logger.LogInformation("Subscription canceled for customer {CustomerId}", customerId);
        }

        private async Task HandlePaymentFailed(Invoice invoice)
        {
            _logger.LogWarning("Payment failed for customer {CustomerId}, invoice {InvoiceId}",
                invoice.CustomerId, invoice.Id);
            // Optionally: send email, flag account, etc.
        }

        private async Task<string> GetTierFromPriceId(string priceId)
        {
            var sql = "SELECT TierName FROM TierLimits WHERE StripePriceId = @PriceId";

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PriceId", priceId);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? "pro";
        }

        private async Task UpdateUserPlan(Guid userId, string tier, string stripeCustomerId, string subscriptionId)
        {
            var sql = @"UPDATE Users 
                        SET [Plan] = @Plan, 
                            StripeCustomerId = @StripeCustomerId, 
                            StripeSubscriptionId = @SubscriptionId 
                        WHERE Id = @UserId";

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Plan", tier);
            command.Parameters.AddWithValue("@StripeCustomerId", stripeCustomerId);
            command.Parameters.AddWithValue("@SubscriptionId", subscriptionId);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }

        private async Task DowngradeUserByCustomerId(string stripeCustomerId)
        {
            var sql = @"UPDATE Users 
                        SET [Plan] = 'free', 
                            StripeSubscriptionId = NULL 
                        WHERE StripeCustomerId = @StripeCustomerId";

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@StripeCustomerId", stripeCustomerId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<string?> GetUserSubscriptionId(Guid userId)
        {
            var sql = "SELECT StripeSubscriptionId FROM Users WHERE Id = @UserId";

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }

        public async Task UpdateUserPlan(Guid userId, string tier, string? subscriptionId)
        {
            var sql = @"UPDATE Users 
                SET [Plan] = @Plan, 
                    StripeSubscriptionId = @SubscriptionId 
                WHERE Id = @UserId";

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Plan", tier);
            command.Parameters.AddWithValue("@SubscriptionId", (object?)subscriptionId ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }
    }
}