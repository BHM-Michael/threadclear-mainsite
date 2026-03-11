using System;

namespace ThreadClear.Functions.Models
{
    public class UserGmailToken
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public string? GmailUserId { get; set; }
        public string? GmailUserEmail { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}