using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;

namespace ThreadClear.Functions.Functions
{
    public class GmailConnectFunction
    {
        private readonly ILogger<GmailConnectFunction> _logger;
        private readonly IConfiguration _configuration;

        public GmailConnectFunction(ILogger<GmailConnectFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function("GmailConnect")]
        public HttpResponseData Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gmail/connect")] HttpRequestData req)
        {
            var clientId = _configuration["GoogleClientId"];
            var redirectUri = _configuration["GoogleRedirectUri"];

            // Get user ID from query string (frontend passes this)
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var userId = query["userId"] ?? "";
            var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId));

            var scopes = HttpUtility.UrlEncode("https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/userinfo.email");

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={clientId}" +
                $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={scopes}" +
                $"&access_type=offline" +
                $"&prompt=consent" +
                $"&state={state}";

            _logger.LogInformation("Redirecting user {UserId} to Google OAuth", userId);

            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", authUrl);
            return response;
        }
    }
}