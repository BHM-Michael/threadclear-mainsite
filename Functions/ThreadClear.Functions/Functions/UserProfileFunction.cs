using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class UserProfileFunction
    {
        private readonly ILogger<UserProfileFunction> _logger;
        private readonly IUserService _userService;

        public UserProfileFunction(ILogger<UserProfileFunction> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [Function("UpdateProfile")]
        public async Task<HttpResponseData> UpdateProfile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/me/profile")] HttpRequestData req)
        {
            try
            {
                var user = await ValidateUserFromHeaders(req);
                if (user == null)
                {
                    var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorized.WriteAsJsonAsync(new { error = "Authentication required" });
                    return unauthorized;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<UpdateProfileRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Invalid request" });
                    return badRequest;
                }

                if (!string.IsNullOrEmpty(request.DisplayName))
                {
                    user.DisplayName = request.DisplayName;
                }

                await _userService.UpdateUser(user);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, displayName = user.DisplayName });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profile");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to update profile" });
                return error;
            }
        }

        [Function("ChangePassword")]
        public async Task<HttpResponseData> ChangePassword(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/me/password")] HttpRequestData req)
        {
            try
            {
                var user = await ValidateUserFromHeaders(req);
                if (user == null)
                {
                    var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorized.WriteAsJsonAsync(new { error = "Authentication required" });
                    return unauthorized;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<ChangePasswordRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Current and new password required" });
                    return badRequest;
                }

                var validated = await _userService.ValidateLogin(user.Email, request.CurrentPassword);
                if (validated == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Current password is incorrect" });
                    return forbidden;
                }

                await _userService.UpdatePassword(user.Id, request.NewPassword);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change password");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to change password" });
                return error;
            }
        }

        private async Task<ThreadClear.Functions.Models.User?> ValidateUserFromHeaders(HttpRequestData req)
        {
            var authHeader = req.Headers.TryGetValues("Authorization", out var authValues)
                ? authValues.FirstOrDefault() : null;

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
                return null;

            var credentials = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader.Substring(6))).Split(':');

            if (credentials.Length != 2)
                return null;

            return await _userService.ValidateLogin(credentials[0], credentials[1]);
        }
    }

    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}