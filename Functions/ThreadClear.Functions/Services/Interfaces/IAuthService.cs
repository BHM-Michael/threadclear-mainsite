using System.Security.Claims;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Services.Interfaces
{
    /// <summary>
    /// Authentication and authorization service
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Validate API key or token
        /// </summary>
        Task<bool> ValidateApiKey(string apiKey);

        /// <summary>
        /// Validate JWT token
        /// </summary>
        Task<ClaimsPrincipal?> ValidateToken(string token);

        /// <summary>
        /// Check if user has access to resource
        /// </summary>
        Task<bool> HasAccess(string userId, string resourceId);

        /// <summary>
        /// Get user information from token/key
        /// </summary>
        Task<UserInfo?> GetUserInfo(string token);

        /// <summary>
        /// Rate limiting check
        /// </summary>
        Task<bool> CheckRateLimit(string userId);
    }

    public class UserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SubscriptionTier { get; set; } = "Free"; // Free, Pro, Enterprise
        public List<string> Roles { get; set; } = new();
    }
}
