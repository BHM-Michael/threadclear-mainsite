using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class Registration
    {
        private readonly IRegistrationService _registrationService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserService _userService;
        private readonly ILogger<Registration> _logger;

        public Registration(
            IRegistrationService registrationService,
            IOrganizationRepository organizationRepository,
            IUserService userService,
            ILogger<Registration> logger)
        {
            _registrationService = registrationService;
            _organizationRepository = organizationRepository;
            _userService = userService;
            _logger = logger;
        }

        [Function("ValidateInvite")]
        public async Task<HttpResponseData> ValidateInvite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/validate-invite/{token}")] HttpRequestData req,
            string token)
        {
            try
            {
                var membership = await _organizationRepository.GetByInviteToken(token);
                
                if (membership == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.OK);
                    await notFound.WriteAsJsonAsync(new
                    {
                        success = true,
                        valid = false,
                        message = "Invalid or expired invitation"
                    });
                    return notFound;
                }

                // Get the organization name
                var org = await _organizationRepository.GetById(membership.OrganizationId);

                // Get the user email if exists
                string? email = null;
                if (membership.UserId != Guid.Empty)
                {
                    var user = await _userService.GetUserById(membership.UserId);
                    email = user?.Email;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    valid = true,
                    email = email,
                    organizationName = org?.Name,
                    role = membership.Role
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invite token");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, valid = false, message = "Error validating invitation" });
                return error;
            }
        }

        [Function("Register")]
        public async Task<HttpResponseData> Register(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<UserRegistration>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid request" });
                    return badRequest;
                }

                // Require invite token
                if (string.IsNullOrEmpty(request.InviteToken))
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteAsJsonAsync(new { success = false, error = "Registration requires an invitation" });
                    return forbidden;
                }

                // Validate invite token exists
                var membership = await _organizationRepository.GetByInviteToken(request.InviteToken);
                if (membership == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Invalid or expired invitation" });
                    return badRequest;
                }

                // Validate password
                if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Password must be at least 8 characters" });
                    return badRequest;
                }

                var result = await _registrationService.RegisterUser(request);

                if (!result.Success)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = result.Error });
                    return badRequest;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    token = result.Token,
                    user = new
                    {
                        id = result.User?.Id,
                        email = result.User?.Email,
                        firstName = result.User?.FirstName,
                        lastName = result.User?.LastName,
                        role = result.User?.Role
                    },
                    organization = result.Organization != null ? new
                    {
                        id = result.Organization.Id,
                        name = result.Organization.Name,
                        slug = result.Organization.Slug
                    } : null
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Registration failed" });
                return error;
            }
        }

        [Function("AcceptInvite")]
        public async Task<HttpResponseData> AcceptInvite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/accept-invite")] HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<AcceptInviteRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Password))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "Token and password are required" });
                    return badRequest;
                }

                var result = await _registrationService.AcceptInvite(request.Token, request.Password);

                if (!result.Success)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = result.Error });
                    return badRequest;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    token = result.Token,
                    user = result.User,
                    organization = result.Organization
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accept invite error");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { success = false, error = "Failed to accept invitation" });
                return error;
            }
        }

        [Function("VerifyEmail")]
        public async Task<HttpResponseData> VerifyEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/verify-email/{token}")] HttpRequestData req,
            string token)
        {
            var success = await _registrationService.VerifyEmail(token);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success,
                message = success ? "Email verified successfully" : "Invalid or expired verification token"
            });
            return response;
        }

        [Function("RequestPasswordReset")]
        public async Task<HttpResponseData> RequestPasswordReset(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/request-reset")] HttpRequestData req)
        {
            var body = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<PasswordResetRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrEmpty(request.Email))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Email is required" });
                return badRequest;
            }

            // Always return success to prevent email enumeration
            await _registrationService.RequestPasswordReset(request.Email);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "If an account exists with this email, a reset link has been sent"
            });
            return response;
        }

        [Function("ResetPassword")]
        public async Task<HttpResponseData> ResetPassword(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/reset-password")] HttpRequestData req)
        {
            var body = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<ResetPasswordRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Password))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Token and password are required" });
                return badRequest;
            }

            if (request.Password.Length < 8)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { success = false, error = "Password must be at least 8 characters" });
                return badRequest;
            }

            var success = await _registrationService.ResetPassword(request.Token, request.Password);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success,
                message = success ? "Password reset successfully" : "Invalid or expired reset token"
            });
            return response;
        }
    }

    // Request DTOs
    public class AcceptInviteRequest
    {
        public string Token { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class PasswordResetRequest
    {
        public string Email { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
