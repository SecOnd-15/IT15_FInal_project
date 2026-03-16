using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using System.Threading.Tasks;

namespace Latog_Final_project.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = default!;

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            // Block login if the user's barangay is inactive (SuperAdmins are exempt)
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("SuperAdmin") && user.BranchId.HasValue)
            {
                var branch = await _context.Branches.FindAsync(user.BranchId.Value);
                if (branch != null && !branch.IsActive)
                {
                    ModelState.AddModelError("", "Your barangay account is currently deactivated. Please contact the administrator.");
                    return Page();
                }
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                false);

            if (result.Succeeded)
            {

                if (roles.Contains("SuperAdmin"))
                    return RedirectToAction("Dashboard", "SuperAdmin");

                if (roles.Contains("Chairman"))
                    return RedirectToAction("Index", "Chairman");

                if (roles.Contains("Treasurer"))
                    return RedirectToAction("Index", "Treasurer");

                if (roles.Contains("Staff"))
                    return RedirectToAction("Index", "Staff");

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return Page();
        }
    }
}
