using Microsoft.Azure.Functions.Worker.Http;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Helpers
{
    public static class AuthHelper
    {
        /// <summary>
        /// Authenticates user from request headers (Bearer token or email/password)
        /// </summary>
        public static async Task<User?> AuthenticateRequest(HttpRequestData req, IUserService userService)
        {
            // Try Bearer token first
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length);
                    var user = await userService.ValidateToken(token);
                    if (user != null)
                    {
                        return user;
                    }
                }
            }

            // Fall back to email/password headers
            var email = req.Headers.TryGetValues("X-User-Email", out var emailValues) ? emailValues.FirstOrDefault() : null;
            var password = req.Headers.TryGetValues("X-User-Password", out var passValues) ? passValues.FirstOrDefault() : null;

            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                return await userService.ValidateLogin(email, password);
            }

            return null;
        }
    }
}
