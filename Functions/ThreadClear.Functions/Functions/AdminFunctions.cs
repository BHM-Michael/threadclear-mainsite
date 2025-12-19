using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Functions
{
    public class AdminFunctions
    {
        private readonly ILogger<AdminFunctions> _logger;
        private readonly IUserService _userService;

        public AdminFunctions(ILogger<AdminFunctions> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        private async Task<User?> ValidateAdminFromHeaders(HttpRequestData req)
        {
            var adminEmail = req.Headers.TryGetValues("X-Admin-Email", out var emailValues)
                ? emailValues.FirstOrDefault() : null;
            var adminPassword = req.Headers.TryGetValues("X-Admin-Password", out var passValues)
                ? passValues.FirstOrDefault() : null;

            if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
                return null;

            var admin = await _userService.ValidateLogin(adminEmail, adminPassword);
            if (admin == null || admin.Role != "admin")
                return null;

            return admin;
        }

        [Function("AdminCreateUser")]
        public async Task<HttpResponseData> CreateUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/users")] HttpRequestData req)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<CreateUserRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Email and password required" });
                    return badRequest;
                }

                var existingUser = await _userService.GetUserByEmail(request.Email);
                if (existingUser != null)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "User already exists" });
                    return conflict;
                }

                var user = await _userService.CreateUser(request, admin.Id);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new { success = true, user = user });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create user error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Create user failed", details = ex.Message });
                return error;
            }
        }

        [Function("AdminGetUsers")]
        public async Task<HttpResponseData> GetUsers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/users")] HttpRequestData req)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var users = await _userService.GetAllUsers();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, users = users });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get users error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to get users" });
                return error;
            }
        }

        [Function("AdminUpdatePermissions")]
        public async Task<HttpResponseData> UpdateUserPermissions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/permissions/{userId}")] HttpRequestData req,
            string userId)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var permissions = JsonSerializer.Deserialize<UserPermissions>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (permissions == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Permissions required" });
                    return badRequest;
                }

                await _userService.UpdateUserPermissions(Guid.Parse(userId), permissions);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update permissions error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Update failed" });
                return error;
            }
        }

        [Function("AdminDeleteUser")]
        public async Task<HttpResponseData> DeleteUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "manage/user/{userId}")] HttpRequestData req,
            string userId)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var deleted = await _userService.DeleteUser(Guid.Parse(userId));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = deleted });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete user error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Delete failed" });
                return error;
            }
        }

        [Function("AdminGetPricing")]
        public async Task<HttpResponseData> GetFeaturePricing(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/pricing")] HttpRequestData req)
        {
            try
            {
                var pricing = await _userService.GetFeaturePricing();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, pricing = pricing });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get pricing error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Failed to get pricing" });
                return error;
            }
        }

        [Function("AdminUpdatePricing")]
        public async Task<HttpResponseData> UpdateFeaturePricing(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/pricing/{featureName}")] HttpRequestData req,
            string featureName)
        {
            try
            {
                var admin = await ValidateAdminFromHeaders(req);
                if (admin == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin access required" });
                    return forbidden;
                }

                var body = await req.ReadAsStringAsync();
                var priceData = JsonSerializer.Deserialize<Dictionary<string, decimal>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (priceData == null || !priceData.ContainsKey("price"))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Price required" });
                    return badRequest;
                }

                await _userService.UpdateFeaturePricing(featureName, priceData["price"], admin.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update pricing error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Update failed" });
                return error;
            }
        }
    }
}