using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public UserPermissions? Permissions { get; set; }
    }

    public class UserPermissions
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool UnansweredQuestions { get; set; }
        public bool TensionPoints { get; set; }
        public bool Misalignments { get; set; }
        public bool ConversationHealth { get; set; }
        public bool SuggestedActions { get; set; }
    }

    public class FeaturePricing
    {
        public Guid Id { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public decimal PricePerUse { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    // DTOs for API requests/responses
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public User? User { get; set; }
        public string? Error { get; set; }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UnansweredQuestions { get; set; }
        public bool TensionPoints { get; set; }
        public bool Misalignments { get; set; }
        public bool ConversationHealth { get; set; }
        public bool SuggestedActions { get; set; }
    }
}
