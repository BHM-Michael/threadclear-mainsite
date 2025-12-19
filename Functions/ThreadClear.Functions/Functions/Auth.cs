using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Functions
{
    public class Auth
    {
        private readonly ILogger<Auth> _logger;
        private readonly IUserService _userService;

        public Auth(ILogger<Auth> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [Function("Login")]
        public async Task<HttpResponseData> Login(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequestData req)
        {
            _logger.LogInformation("Login attempt");

            try
            {
                var body = await req.ReadAsStringAsync();
                var loginRequest = JsonSerializer.Deserialize<LoginRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loginRequest == null || string.IsNullOrEmpty(loginRequest.Email) || string.IsNullOrEmpty(loginRequest.Password))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new LoginResponse { Success = false, Error = "Email and password required" });
                    return badRequest;
                }

                var user = await _userService.ValidateLogin(loginRequest.Email, loginRequest.Password);

                if (user == null)
                {
                    var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorized.WriteAsJsonAsync(new LoginResponse { Success = false, Error = "Invalid email or password" });
                    return unauthorized;
                }

                var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new LoginResponse
                {
                    Success = true,
                    Token = token,
                    User = user
                });

                _logger.LogInformation("User logged in: {Email}", user.Email);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new LoginResponse { Success = false, Error = "Login failed" });
                return error;
            }
        }

        [Function("SetupAdmin")]
        public async Task<HttpResponseData> SetupAdmin(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth/setup-admin")] HttpRequestData req)
        {
            _logger.LogInformation("Admin setup attempt");

            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<LoginRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Email and password required" });
                    return badRequest;
                }

                var existingUsers = await _userService.GetAllUsers();
                if (existingUsers.Any(u => u.Role == "admin"))
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { error = "Admin already exists" });
                    return forbidden;
                }

                var admin = await _userService.CreateAdminUser(request.Email, request.Password);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, user = admin });

                _logger.LogInformation("Admin user created: {Email}", admin.Email);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin setup error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = "Setup failed", details = ex.Message });
                return error;
            }
        }
    }
}