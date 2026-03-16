using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Latog_Final_project.Areas.Identity.Pages.Account
{
    [Authorize(Roles = "Chairman")]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IAuditService _auditService;
        private readonly IBranchService _branchService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IEmailService emailService,
            IAuditService auditService,
            IBranchService branchService,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
            _branchService = branchService;
            _logger = logger;
        }

        public List<ApplicationUser> CurrentUsers { get; set; } = new();
        public Dictionary<string, string> UserRoles { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Role { get; set; }
            public string Password { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadRequestsAsync();
        }


        // ===============================
        // ✅ CREATE USER MANUALLY (Chairman Only)
        // ===============================
        public async Task<IActionResult> OnPostCreateUserAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Input.Email) || string.IsNullOrWhiteSpace(Input.FullName) || string.IsNullOrWhiteSpace(Input.Password))
            {
                TempData["Error"] = "Please provide Full Name, Email, and Password.";
                return RedirectToPage();
            }

            var branchId = _branchService.GetCurrentBranchId();

            // Validate role (Chairman cannot create SuperAdmins)
            var validRoles = new[] { "Staff", "Treasurer", "Chairman" };
            var role = (string.IsNullOrWhiteSpace(Input.Role) || !validRoles.Contains(Input.Role)) ? "Staff" : Input.Role;

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                TempData["Error"] = $"A user with email '{Input.Email}' already exists.";
                return RedirectToPage();
            }

            // 1. Create user
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName,
                EmailConfirmed = true,
                BranchId = branchId
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToPage();
            }

            // 2. Assign role
            await _userManager.AddToRoleAsync(user, role);

            // 3. Audit log
            await _auditService.LogAsync(
                "Create",
                "Account Management",
                "ApplicationUser",
                user.Id,
                $"Manual account creation by Chairman for '{Input.Email}' with role '{role}'"
            );

            // 4. Send "Set Password" email (Reuse same logic as approval)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/SetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encodedToken, email = Input.Email },
                protocol: Request.Scheme);

            try
            {
                var htmlBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 500px; margin: auto; padding: 20px;'>
                        <div style='background: linear-gradient(135deg, #f4c430, #ffd95a); padding: 20px; border-radius: 12px 12px 0 0; text-align: center;'>
                            <h2 style='margin: 0; color: #333;'>🎉 Welcome to IRP System</h2>
                        </div>
                        <div style='background: #fff; padding: 24px; border: 1px solid #eee; border-radius: 0 0 12px 12px;'>
                            <p>Hello <strong>{Input.FullName}</strong>,</p>
                            <p>An account has been created for you by the Chairman. You have been assigned the role of <strong>{role}</strong>.</p>
                            <p>Click the button below to set your password and activate your account:</p>
                            <div style='text-align: center; margin: 24px 0;'>
                                <a href='{callbackUrl}' style='background: #f4c430; color: #000; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
                                    Set Your Password
                                </a>
                            </div>
                        </div>
                    </div>";

                await _emailService.SendEmailAsync(Input.Email, "Account Created – Set Your Password", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send creation email to {Email}", Input.Email);
                TempData["Warning"] = "Account created, but the invitation email failed to send.";
            }

            TempData["Success"] = $"Account for {Input.Email} has been created successfully!";
            return RedirectToPage();
        }


        private async Task LoadRequestsAsync()
        {
            var branchId = _branchService.GetCurrentBranchId();

            // Fetch Users for current branch
            var usersInBranch = await _userManager.Users
                .Where(u => u.BranchId == branchId)
                .OrderBy(u => u.Email)
                .ToListAsync();
            
            CurrentUsers = new List<ApplicationUser>();
            foreach (var user in usersInBranch)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var primaryRole = roles.FirstOrDefault() ?? "No Role";

                // ⛔ HIDE SUPERADMINS FROM CHAIRMAN
                if (primaryRole == "SuperAdmin") continue;

                CurrentUsers.Add(user);
                UserRoles[user.Id] = primaryRole;
            }
        }

        // ===============================
        // ✅ CHANGE USER ROLE
        // ===============================
        public async Task<IActionResult> OnPostChangeRoleAsync(string userId, string newRole)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(newRole) || newRole == "SuperAdmin")
                return RedirectToPage();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var oldRole = currentRoles.FirstOrDefault() ?? "None";

            // ⛔ BLOCK CHAIRMAN ROLE CHANGE
            if (oldRole == "Chairman")
            {
                TempData["Error"] = "The Chairman role cannot be changed through this panel for security reasons.";
                return RedirectToPage();
            }

            if (oldRole != newRole)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, newRole);

                // Audit Log
                await _auditService.LogAsync(
                    "Role Change",
                    "Account Management",
                    "ApplicationUser",
                    userId,
                    $"Role updated from '{oldRole}' to '{newRole}' for user '{user.Email}'"
                );

                TempData["Success"] = $"Role for {user.Email} updated to {newRole}.";
            }

            return RedirectToPage();
        }

        // ===============================
        // ❌ DELETE USER
        // ===============================
        public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return RedirectToPage();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Prevent self-deletion
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToPage();
            }

            var email = user.Email;
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    "Delete",
                    "Account Management",
                    "ApplicationUser",
                    userId,
                    $"User account '{email}' was deleted by Chairman"
                );
                TempData["Success"] = $"User {email} has been deleted.";
            }
            else
            {
                TempData["Error"] = "Failed to delete user.";
            }

            return RedirectToPage();
        }
    }
}
