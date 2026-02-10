using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ThreadClear.Functions.Functions
{
    public class UserApproval
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        public UserApproval(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<UserApproval>();
            _connectionString = configuration["SqlConnectionString"]
                ?? configuration.GetConnectionString("SqlConnectionString")
                ?? "";
        }

        [Function("ApproveUser")]
        public async Task<HttpResponseData> ApproveUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "approvals/user/{userId}")] HttpRequestData req,
            string userId)
        {
            if (req.Method == "OPTIONS")
                return req.CreateResponse(HttpStatusCode.OK);

            try
            {
                // Authenticate admin
                var admin = await GetAuthenticatedUser(req);
                if (admin == null)
                    return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Authentication required");

                // Check if admin or super admin
                if (admin.Role != "admin" && !admin.IsSuperAdmin)
                    return await CreateErrorResponse(req, HttpStatusCode.Forbidden, "Admin access required");

                if (!Guid.TryParse(userId, out var userGuid))
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid user ID");

                // If not super admin, verify user is in admin's org
                if (!admin.IsSuperAdmin)
                {
                    var canApprove = await CanAdminApproveUser(admin.Id, userGuid);
                    if (!canApprove)
                        return await CreateErrorResponse(req, HttpStatusCode.Forbidden, "Cannot approve users outside your organization");
                }

                // Approve the user
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "UPDATE Users SET IsActive = 1 WHERE Id = @UserId";
                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@UserId", userGuid);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "User not found");

                _logger.LogInformation("User {UserId} approved by {AdminId}", userId, admin.Id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "User approved" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving user {UserId}", userId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to approve user");
            }
        }

        private async Task<bool> CanAdminApproveUser(Guid adminId, Guid userId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if both admin and user share at least one organization
            var sql = @"
                SELECT COUNT(*) FROM OrganizationMemberships am
                INNER JOIN OrganizationMemberships um ON am.OrganizationId = um.OrganizationId
                WHERE am.UserId = @AdminId AND um.UserId = @UserId";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@AdminId", adminId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task<AdminUser?> GetAuthenticatedUser(HttpRequestData req)
        {
            if (!req.Headers.TryGetValues("X-User-Email", out var emailValues) ||
                !req.Headers.TryGetValues("X-User-Password", out var passwordValues))
                return null;

            var email = emailValues.FirstOrDefault();
            var password = passwordValues.FirstOrDefault();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return null;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Id, Email, PasswordHash, Role, IsSuperAdmin FROM Users WHERE Email = @Email AND IsActive = 1";
            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Email", email);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var storedHash = reader.GetString(2);
            if (!BCrypt.Net.BCrypt.Verify(password, storedHash))
                return null;

            return new AdminUser
            {
                Id = reader.GetGuid(0),
                Email = reader.GetString(1),
                Role = reader.GetString(3),
                IsSuperAdmin = reader.GetBoolean(4)
            };
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
        {
            var response = req.CreateResponse(status);
            await response.WriteAsJsonAsync(new { success = false, error = message });
            return response;
        }

        private class AdminUser
        {
            public Guid Id { get; set; }
            public string Email { get; set; } = "";
            public string Role { get; set; } = "";
            public bool IsSuperAdmin { get; set; }
        }
    }
}