using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IUserService
    {
        // User retrieval
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserById(Guid id);
        Task<User?> ValidateLogin(string email, string password);
        
        // User creation
        Task<User> CreateUser(CreateUserRequest request, Guid? createdBy = null);
        Task<User> CreateAdminUser(string email, string password);
        Task<User> CreateUserDirect(User user);
        
        // User management
        Task<List<User>> GetAllUsers();
        Task<User> UpdateUser(User user);
        Task UpdateUserPermissions(Guid userId, UserPermissions permissions);
        Task<bool> DeleteUser(Guid userId);
        
        // Feature pricing
        Task<List<FeaturePricing>> GetFeaturePricing();
        Task UpdateFeaturePricing(string featureName, decimal price, Guid updatedBy);

        // Token management
        Task<string> CreateUserToken(Guid userId, string? deviceInfo = null, int expirationDays = 30);
        Task<User?> ValidateToken(string token);
        Task RevokeToken(string token);
        Task RevokeAllUserTokens(Guid userId);

        // Registration flow
        Task<User?> GetUserByVerificationToken(string token);
        Task<User?> GetUserByResetToken(string token);
    }
}
