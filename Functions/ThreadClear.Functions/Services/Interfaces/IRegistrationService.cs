using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;

namespace ThreadClear.Functions.Services.Interfaces
{
    public interface IRegistrationService
    {
        Task<RegistrationResult> RegisterUser(UserRegistration registration);
        Task<RegistrationResult> AcceptInvite(string inviteToken, string password);
        Task<bool> VerifyEmail(string verificationToken);
        Task<bool> RequestPasswordReset(string email);
        Task<bool> ResetPassword(string resetToken, string newPassword);
    }
}
