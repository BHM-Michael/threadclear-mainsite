using System;

namespace ThreadClear.Functions.Models
{
    public class UserGraphToken
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public string? GraphUserId { get; set; }
        public string? GraphUserEmail { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}