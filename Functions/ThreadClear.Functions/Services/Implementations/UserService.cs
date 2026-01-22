using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;
using BCrypt.Net;

namespace ThreadClear.Functions.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly string _connectionString;
        private readonly ILogger<UserService> _logger;

        public UserService(string connectionString, ILogger<UserService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        #region User Retrieval

        public async Task<User?> GetUserByEmail(string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, coalesce(u.DisplayName, '') DisplayName, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM Users u
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                WHERE u.Email = @Email";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Email", email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReader(reader);
            }
            return null;
        }

        public async Task<User?> GetUserById(Guid id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, coalesce(u.DisplayName, '') DisplayName, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM Users u
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                WHERE u.Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReader(reader);
            }
            return null;
        }

        public async Task<User?> ValidateLogin(string email, string password)
        {
            var user = await GetUserByEmail(email);
            if (user == null || !user.IsActive)
                return null;

            // Add this check
            if (string.IsNullOrEmpty(user.PasswordHash))
                return null;

            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                user.PasswordHash = ""; // Don't return hash
                return user;
            }
            return null;
        }

        public async Task<List<User>> GetAllUsers()
        {
            var users = new List<User>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, coalesce(u.DisplayName, '') DisplayName, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM Users u
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                ORDER BY u.CreatedAt DESC";

            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var user = MapUserFromReader(reader);
                user.PasswordHash = ""; // Don't return hash
                users.Add(user);
            }
            return users;
        }

        #endregion

        #region User Creation

        public async Task<User> CreateAdminUser(string email, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var userId = Guid.NewGuid();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            using var transaction = connection.BeginTransaction();
            try
            {
                var userSql = @"
                    INSERT INTO Users (Id, Email, PasswordHash, Role, IsActive, CreatedAt)
                    VALUES (@Id, @Email, @PasswordHash, 'admin', 1, GETUTCDATE())";

                using (var cmd = new SqlCommand(userSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    await cmd.ExecuteNonQueryAsync();
                }

                var permSql = @"
                    INSERT INTO UserPermissions (Id, UserId, UnansweredQuestions, TensionPoints, Misalignments, ConversationHealth, SuggestedActions)
                    VALUES (NEWID(), @UserId, 1, 1, 1, 1, 1)";

                using (var cmd = new SqlCommand(permSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                _logger.LogInformation("Created admin user: {Email}", email);

                return new User
                {
                    Id = userId,
                    Email = email,
                    Role = "admin",
                    IsActive = true,
                    Permissions = new UserPermissions
                    {
                        UnansweredQuestions = true,
                        TensionPoints = true,
                        Misalignments = true,
                        ConversationHealth = true,
                        SuggestedActions = true
                    }
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<User> CreateUser(CreateUserRequest request, Guid? createdBy = null)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var userId = Guid.NewGuid();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            using var transaction = connection.BeginTransaction();
            try
            {
                var userSql = @"
                    INSERT INTO Users (Id, Email, DisplayName, PasswordHash, Role, IsActive, CreatedAt, CreatedBy, [Plan])
                    VALUES (@Id, @Email, @DisplayName, @PasswordHash, 'user', 1, GETUTCDATE(), @CreatedBy, 'free')";

                using (var cmd = new SqlCommand(userSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.Parameters.AddWithValue("@Email", request.Email);
                    cmd.Parameters.AddWithValue("@DisplayName", request.DisplayName);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy.HasValue ? createdBy.Value : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                var permSql = @"
                    INSERT INTO UserPermissions (Id, UserId, UnansweredQuestions, TensionPoints, Misalignments, ConversationHealth, SuggestedActions)
                    VALUES (NEWID(), @UserId, @UQ, @TP, @MA, @CH, @SA)";

                using (var cmd = new SqlCommand(permSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@UQ", request.UnansweredQuestions);
                    cmd.Parameters.AddWithValue("@TP", request.TensionPoints);
                    cmd.Parameters.AddWithValue("@MA", request.Misalignments);
                    cmd.Parameters.AddWithValue("@CH", request.ConversationHealth);
                    cmd.Parameters.AddWithValue("@SA", request.SuggestedActions);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                _logger.LogInformation("Created user: {Email}", request.Email);

                return new User
                {
                    Id = userId,
                    Email = request.Email,
                    Role = "user",
                    IsActive = true,
                    Permissions = new UserPermissions
                    {
                        UserId = userId,
                        UnansweredQuestions = request.UnansweredQuestions,
                        TensionPoints = request.TensionPoints,
                        Misalignments = request.Misalignments,
                        ConversationHealth = request.ConversationHealth,
                        SuggestedActions = request.SuggestedActions
                    }
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<User> CreateUserDirect(User user)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO Users (Id, Email, PasswordHash, Role, IsActive, CreatedAt, CreatedBy,
                                   DisplayName, FirstName, LastName, LastLoginAt, EmailVerifiedAt,
                                   EmailVerificationToken, PasswordResetToken, PasswordResetExpires, Preferences)
                VALUES (@Id, @Email, @PasswordHash, @Role, @IsActive, @CreatedAt, @CreatedBy,
                        @DisplayName, @FirstName, @LastName, @LastLoginAt, @EmailVerifiedAt,
                        @EmailVerificationToken, @PasswordResetToken, @PasswordResetExpires, @Preferences)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", user.Id);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@Role", user.Role);
            cmd.Parameters.AddWithValue("@IsActive", user.IsActive);
            cmd.Parameters.AddWithValue("@CreatedAt", user.CreatedAt);
            cmd.Parameters.AddWithValue("@CreatedBy", (object?)user.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstName", (object?)user.FirstName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName", (object?)user.LastName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastLoginAt", (object?)user.LastLoginAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailVerifiedAt", (object?)user.EmailVerifiedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailVerificationToken", (object?)user.EmailVerificationToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PasswordResetToken", (object?)user.PasswordResetToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PasswordResetExpires", (object?)user.PasswordResetExpires ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Preferences", (object?)user.PreferencesJson ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created user {UserId} - {Email}", user.Id, user.Email);

            return user;
        }

        #endregion

        #region User Management

        public async Task<User> UpdateUser(User user)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE Users SET
                    Email = @Email,
                    PasswordHash = @PasswordHash,
                    Role = @Role,
                    IsActive = @IsActive,
                    DisplayName = @DisplayName,
                    FirstName = @FirstName,
                    LastName = @LastName,
                    LastLoginAt = @LastLoginAt,
                    EmailVerifiedAt = @EmailVerifiedAt,
                    EmailVerificationToken = @EmailVerificationToken,
                    PasswordResetToken = @PasswordResetToken,
                    PasswordResetExpires = @PasswordResetExpires,
                    Preferences = @Preferences
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", user.Id);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@Role", user.Role);
            cmd.Parameters.AddWithValue("@IsActive", user.IsActive);
            cmd.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstName", (object?)user.FirstName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName", (object?)user.LastName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastLoginAt", (object?)user.LastLoginAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailVerifiedAt", (object?)user.EmailVerifiedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailVerificationToken", (object?)user.EmailVerificationToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PasswordResetToken", (object?)user.PasswordResetToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PasswordResetExpires", (object?)user.PasswordResetExpires ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Preferences", (object?)user.PreferencesJson ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated user {UserId}", user.Id);

            return user;
        }

        public async Task UpdateUserPermissions(Guid userId, UserPermissions permissions)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE UserPermissions 
                SET UnansweredQuestions = @UQ, TensionPoints = @TP, Misalignments = @MA, 
                    ConversationHealth = @CH, SuggestedActions = @SA
                WHERE UserId = @UserId";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@UQ", permissions.UnansweredQuestions);
            cmd.Parameters.AddWithValue("@TP", permissions.TensionPoints);
            cmd.Parameters.AddWithValue("@MA", permissions.Misalignments);
            cmd.Parameters.AddWithValue("@CH", permissions.ConversationHealth);
            cmd.Parameters.AddWithValue("@SA", permissions.SuggestedActions);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> DeleteUser(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var permSql = "DELETE FROM UserPermissions WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(permSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                var userSql = "DELETE FROM Users WHERE Id = @UserId";
                using (var cmd = new SqlCommand(userSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    var rows = await cmd.ExecuteNonQueryAsync();
                    transaction.Commit();
                    return rows > 0;
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdatePassword(Guid userId, string newPassword)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            var sql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @UserId";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Password updated for user {UserId}", userId);
        }

        #endregion

        #region Feature Pricing

        public async Task<List<FeaturePricing>> GetFeaturePricing()
        {
            var pricing = new List<FeaturePricing>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Id, FeatureName, PricePerUse, IsActive, UpdatedAt, UpdatedBy FROM FeaturePricing";
            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                pricing.Add(new FeaturePricing
                {
                    Id = reader.GetGuid(0),
                    FeatureName = reader.GetString(1),
                    PricePerUse = reader.GetDecimal(2),
                    IsActive = reader.GetBoolean(3),
                    UpdatedAt = reader.GetDateTime(4),
                    UpdatedBy = reader.IsDBNull(5) ? null : reader.GetGuid(5)
                });
            }
            return pricing;
        }

        public async Task UpdateFeaturePricing(string featureName, decimal price, Guid updatedBy)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE FeaturePricing 
                SET PricePerUse = @Price, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE FeatureName = @FeatureName";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@FeatureName", featureName);
            cmd.Parameters.AddWithValue("@Price", price);
            cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Token Management

        public async Task<string> CreateUserToken(Guid userId, string? deviceInfo = null, int expirationDays = 30)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                        Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var expiresAt = DateTime.UtcNow.AddDays(expirationDays);

            var sql = @"
                INSERT INTO UserTokens (UserId, Token, DeviceInfo, ExpiresAt)
                VALUES (@UserId, @Token, @DeviceInfo, @ExpiresAt)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@DeviceInfo", (object?)deviceInfo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Created token for user {UserId}, expires {ExpiresAt}", userId, expiresAt);

            return token;
        }

        public async Task<User?> ValidateToken(string token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, coalesce(u.DisplayName, '') DisplayName, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM UserTokens t
                INNER JOIN Users u ON t.UserId = u.Id
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                WHERE t.Token = @Token 
                  AND t.IsRevoked = 0 
                  AND t.ExpiresAt > GETUTCDATE()
                  AND u.IsActive = 1";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var user = MapUserFromReader(reader);
                user.PasswordHash = ""; // Don't return hash
                return user;
            }
            return null;
        }

        public async Task RevokeToken(string token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE UserTokens SET IsRevoked = 1 WHERE Token = @Token";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Token", token);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RevokeAllUserTokens(Guid userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE UserTokens SET IsRevoked = 1 WHERE UserId = @UserId";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Registration Flow

        public async Task<User?> GetUserByVerificationToken(string token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       u.DisplayName, u.FirstName, u.LastName, u.LastLoginAt, u.EmailVerifiedAt,
                       u.EmailVerificationToken, u.PasswordResetToken, u.PasswordResetExpires, u.Preferences,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM Users u
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                WHERE u.EmailVerificationToken = @Token";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReaderExtended(reader);
            }
            return null;
        }

        public async Task<User?> GetUserByResetToken(string token)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT u.Id, u.Email, u.PasswordHash, u.Role, u.IsActive, u.CreatedAt, u.CreatedBy,
                       u.DisplayName, u.FirstName, u.LastName, u.LastLoginAt, u.EmailVerifiedAt,
                       u.EmailVerificationToken, u.PasswordResetToken, u.PasswordResetExpires, u.Preferences,
                       p.Id as PermId, p.UnansweredQuestions, p.TensionPoints, p.Misalignments, 
                       p.ConversationHealth, p.SuggestedActions
                FROM Users u
                LEFT JOIN UserPermissions p ON u.Id = p.UserId
                WHERE u.PasswordResetToken = @Token AND u.PasswordResetExpires > @Now";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReaderExtended(reader);
            }
            return null;
        }

        #endregion

        #region Mappers

        private User MapUserFromReader(SqlDataReader reader)
        {
            var user = new User
            {
                Id = reader.GetGuid(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                PasswordHash = reader.IsDBNull(3) ? "" : reader.GetString(3),  // FIX: null check
                Role = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.GetDateTime(6),
                CreatedBy = reader.IsDBNull(7) ? null : reader.GetGuid(7)  // FIX: was GetGuid(6)
            };

            if (!reader.IsDBNull(8))  // FIX: was index 7, should be 8 (PermId)
            {
                user.Permissions = new UserPermissions
                {
                    Id = reader.GetGuid(8),           // FIX: was index 7
                    UserId = user.Id,
                    UnansweredQuestions = reader.GetBoolean(9),   // FIX: was 8
                    TensionPoints = reader.GetBoolean(10),         // FIX: was 9
                    Misalignments = reader.GetBoolean(11),         // FIX: was 10
                    ConversationHealth = reader.GetBoolean(12),    // FIX: was 11
                    SuggestedActions = reader.GetBoolean(13)       // FIX: was 12
                };
            }

            return user;
        }

        private User MapUserFromReaderExtended(SqlDataReader reader)
        {
            var user = new User
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? null : reader.GetGuid(reader.GetOrdinal("CreatedBy"))
            };

            // Extended fields
            if (!reader.IsDBNull(reader.GetOrdinal("DisplayName")))
                user.DisplayName = reader.GetString(reader.GetOrdinal("DisplayName"));
            if (!reader.IsDBNull(reader.GetOrdinal("FirstName")))
                user.FirstName = reader.GetString(reader.GetOrdinal("FirstName"));
            if (!reader.IsDBNull(reader.GetOrdinal("LastName")))
                user.LastName = reader.GetString(reader.GetOrdinal("LastName"));
            if (!reader.IsDBNull(reader.GetOrdinal("LastLoginAt")))
                user.LastLoginAt = reader.GetDateTime(reader.GetOrdinal("LastLoginAt"));
            if (!reader.IsDBNull(reader.GetOrdinal("EmailVerifiedAt")))
                user.EmailVerifiedAt = reader.GetDateTime(reader.GetOrdinal("EmailVerifiedAt"));
            if (!reader.IsDBNull(reader.GetOrdinal("EmailVerificationToken")))
                user.EmailVerificationToken = reader.GetString(reader.GetOrdinal("EmailVerificationToken"));
            if (!reader.IsDBNull(reader.GetOrdinal("PasswordResetToken")))
                user.PasswordResetToken = reader.GetString(reader.GetOrdinal("PasswordResetToken"));
            if (!reader.IsDBNull(reader.GetOrdinal("PasswordResetExpires")))
                user.PasswordResetExpires = reader.GetDateTime(reader.GetOrdinal("PasswordResetExpires"));
            if (!reader.IsDBNull(reader.GetOrdinal("Preferences")))
                user.PreferencesJson = reader.GetString(reader.GetOrdinal("Preferences"));

            // Permissions
            if (!reader.IsDBNull(reader.GetOrdinal("PermId")))
            {
                user.Permissions = new UserPermissions
                {
                    Id = reader.GetGuid(reader.GetOrdinal("PermId")),
                    UserId = user.Id,
                    UnansweredQuestions = reader.GetBoolean(reader.GetOrdinal("UnansweredQuestions")),
                    TensionPoints = reader.GetBoolean(reader.GetOrdinal("TensionPoints")),
                    Misalignments = reader.GetBoolean(reader.GetOrdinal("Misalignments")),
                    ConversationHealth = reader.GetBoolean(reader.GetOrdinal("ConversationHealth")),
                    SuggestedActions = reader.GetBoolean(reader.GetOrdinal("SuggestedActions"))
                };
            }

            return user;
        }

        #endregion
    }
}
