using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Services.Implementations
{
    /// <summary>
    /// Authentication and authorization service
    /// Handles API keys, JWT tokens, and rate limiting
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly string _jwtSecret;
        private readonly Dictionary<string, UserInfo> _apiKeyStore; // In production: use database
        private readonly Dictionary<string, (int count, DateTime resetTime)> _rateLimits;

        public AuthService(string? jwtSecret = null)
        {
            _jwtSecret = jwtSecret ?? "your-secret-key-min-32-characters-long-for-production";
            _apiKeyStore = new Dictionary<string, UserInfo>();
            _rateLimits = new Dictionary<string, (int, DateTime)>();

            // Initialize with some test API keys (in production: load from database)
            InitializeTestApiKeys();
        }

        public async Task<bool> ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            return await Task.FromResult(_apiKeyStore.ContainsKey(apiKey));
        }

        public async Task<ClaimsPrincipal?> ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                return await Task.FromResult(principal);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> HasAccess(string userId, string resourceId)
        {
            // In production: check database for user permissions
            // For now: simple check
            return await Task.FromResult(!string.IsNullOrWhiteSpace(userId));
        }

        public async Task<UserInfo?> GetUserInfo(string apiKeyOrToken)
        {
            // Try as API key first
            if (_apiKeyStore.TryGetValue(apiKeyOrToken, out var userInfo))
            {
                return await Task.FromResult(userInfo);
            }

            // Try as JWT token
            var principal = await ValidateToken(apiKeyOrToken);
            if (principal != null)
            {
                return await Task.FromResult(new UserInfo
                {
                    UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                    Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                    SubscriptionTier = principal.FindFirst("SubscriptionTier")?.Value ?? "Free",
                    Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
                });
            }

            return null;
        }

        public async Task<bool> CheckRateLimit(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var now = DateTime.UtcNow;
            var resetTime = now.AddHours(1); // 1 hour window

            if (_rateLimits.TryGetValue(userId, out var limit))
            {
                // Check if rate limit window has expired
                if (now > limit.resetTime)
                {
                    // Reset the counter
                    _rateLimits[userId] = (1, resetTime);
                    return await Task.FromResult(true);
                }

                // Check if under limit
                var maxRequests = GetMaxRequestsForUser(userId);
                if (limit.count < maxRequests)
                {
                    _rateLimits[userId] = (limit.count + 1, limit.resetTime);
                    return await Task.FromResult(true);
                }

                // Over limit
                return await Task.FromResult(false);
            }

            // First request
            _rateLimits[userId] = (1, resetTime);
            return await Task.FromResult(true);
        }

        #region Helper Methods

        private void InitializeTestApiKeys()
        {
            // Test API keys (in production: load from secure storage)
            _apiKeyStore["tc-test-key-12345"] = new UserInfo
            {
                UserId = "user-1",
                Email = "test@example.com",
                SubscriptionTier = "Pro",
                Roles = new List<string> { "User" }
            };

            _apiKeyStore["tc-admin-key-67890"] = new UserInfo
            {
                UserId = "admin-1",
                Email = "admin@threadclear.com",
                SubscriptionTier = "Enterprise",
                Roles = new List<string> { "User", "Admin" }
            };
        }

        private int GetMaxRequestsForUser(string userId)
        {
            // Get user's subscription tier and return appropriate limit
            var userInfo = _apiKeyStore.Values.FirstOrDefault(u => u.UserId == userId);

            return userInfo?.SubscriptionTier switch
            {
                "Enterprise" => 10000,
                "Pro" => 1000,
                "Free" => 100,
                _ => 100
            };
        }

        public string GenerateJwtToken(UserInfo userInfo, int expirationHours = 24)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userInfo.UserId),
                new Claim(ClaimTypes.Email, userInfo.Email),
                new Claim("SubscriptionTier", userInfo.SubscriptionTier)
            };

            foreach (var role in userInfo.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(expirationHours),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        #endregion
    }
}
