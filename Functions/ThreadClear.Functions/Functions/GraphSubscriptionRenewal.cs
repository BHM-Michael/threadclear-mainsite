using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class GraphSubscriptionRenewal
    {
        private readonly IGraphService _graphService;
        private readonly IGraphTokenRepository _graphTokenRepo;
        private readonly IUserService _userService;
        private readonly ILogger<GraphSubscriptionRenewal> _logger;

        public GraphSubscriptionRenewal(
            IGraphService graphService,
            IGraphTokenRepository graphTokenRepo,
            IUserService userService,
            ILogger<GraphSubscriptionRenewal> logger)
        {
            _graphService = graphService;
            _graphTokenRepo = graphTokenRepo;
            _userService = userService;
            _logger = logger;
        }

        // Runs every 12 hours
        [Function("GraphSubscriptionRenewal")]
        public async Task Run([TimerTrigger("0 0 */12 * * *")] TimerInfo timer)
        {
            _logger.LogInformation("GraphSubscriptionRenewal fired at {Time}", DateTime.UtcNow);

            var users = await _userService.GetAllUsers();

            foreach (var user in users)
            {
                try
                {
                    var tokenRecord = await _graphTokenRepo.GetByUserIdAsync(user.Id);
                    if (tokenRecord == null) continue;

                    var subscriptionId = await _graphTokenRepo.GetSubscriptionIdAsync(user.Id);
                    if (string.IsNullOrEmpty(subscriptionId)) continue;

                    var accessToken = await _graphService.RefreshAccessTokenAsync(tokenRecord.RefreshToken);
                    await _graphService.RenewSubscriptionAsync(accessToken, subscriptionId);

                    _logger.LogInformation("Renewed Graph subscription for user {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to renew subscription for user {UserId}", user.Id);
                }
            }
        }
    }
}