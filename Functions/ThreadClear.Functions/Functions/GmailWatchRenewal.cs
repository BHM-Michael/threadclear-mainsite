using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class GmailWatchRenewal
    {
        private readonly IGmailService _gmailService;
        private readonly IGmailTokenRepository _gmailTokenRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<GmailWatchRenewal> _logger;

        public GmailWatchRenewal(
            IGmailService gmailService,
            IGmailTokenRepository gmailTokenRepo,
            IConfiguration config,
            ILogger<GmailWatchRenewal> logger)
        {
            _gmailService = gmailService;
            _gmailTokenRepo = gmailTokenRepo;
            _config = config;
            _logger = logger;
        }

        [Function("GmailWatchRenewal")]
        public async Task Run([TimerTrigger("0 0 */12 * * *")] TimerInfo timer)
        {
            _logger.LogInformation("GmailWatchRenewal fired at {Time}", DateTime.UtcNow);

            var userIds = await _gmailTokenRepo.GetUsersWithExpiringWatchesAsync();
            if (userIds.Count == 0)
            {
                _logger.LogInformation("No Gmail watches need renewal");
                return;
            }

            var pubSubTopic = _config["GooglePubSubTopic"]!;
            var clientId = _config["GoogleClientId"]!;
            var clientSecret = _config["GoogleClientSecret"]!;

            foreach (var userId in userIds)
            {
                try
                {
                    var tokenRecord = await _gmailTokenRepo.GetByUserIdAsync(userId);
                    if (tokenRecord == null) continue;

                    var accessToken = await _gmailService.RefreshAccessTokenAsync(
                        tokenRecord.RefreshToken, clientId, clientSecret);

                    var watchResult = await _gmailService.SetupWatchAsync(accessToken, pubSubTopic);

                    await _gmailTokenRepo.UpsertWatchAsync(userId, watchResult.HistoryId,
                        watchResult.Expiration);

                    _logger.LogInformation("Renewed Gmail watch for user {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to renew Gmail watch for user {UserId}", userId);
                }
            }
        }
    }
}