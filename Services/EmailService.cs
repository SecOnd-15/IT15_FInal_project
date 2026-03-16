using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Latog_Final_project.Models;

namespace Latog_Final_project.Services
{
    // ================================
    // INTERFACE
    // ================================
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }

    // ================================
    // IMPLEMENTATION (Disabled)
    // ================================
    public class EmailService : IEmailService, IEmailSender, Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            // Email notifications have been disabled by system administrator.
            await Task.CompletedTask;
        }

        // ================================
        // IEmailSender<ApplicationUser> Implementation
        // ================================
        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
            => SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
            => SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
            => SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }
}
