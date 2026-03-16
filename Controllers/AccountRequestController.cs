using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Latog_Final_project.Controllers
{
    [Authorize]
    public class AccountRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly IBranchService _branchService;


        public AccountRequestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            IEmailService emailService,
            IBranchService branchService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _emailService = emailService;
            _branchService = branchService;
        }


        // ===============================
        // 📋 VIEW REQUESTS (Chairman Only)
        // ===============================
        [Authorize(Roles = "Chairman")]
        public async Task<IActionResult> Index(string? status, int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var query = _context.AccountRequests.AsQueryable();

            if (!isSuperAdmin && branchId.HasValue)
            {
                query = query.Where(r => r.BranchId == branchId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }


            var totalRecords = await query.CountAsync();

            var requests = await query
                .OrderByDescending(r => r.DateRequested)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new AccountRequestFilterViewModel
            {
                StatusFilter = status,
                Requests = requests,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_AccountRequestTable", model);
            }

            return View(model);
        }

        // ===============================
        //  APPROVE REQUEST (Chairman Only)
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chairman")]
        public async Task<IActionResult> Approve(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var request = await _context.AccountRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null || request.Status != "Pending")
                return BadRequest();

            // Security: If Chairman, ensure they only approve their own branch requests
            if (!isSuperAdmin && branchId.HasValue && request.BranchId != branchId)
            {
                return Forbid();
            }


            // 1. Create the User
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                EmailConfirmed = true,
                BranchId = request.BranchId
            };


            // Temporary Password (User should change it later, or use a reset link)
            var result = await _userManager.CreateAsync(user, "TempPass@123");

            if (result.Succeeded)
            {
                // 2. Assign Role
                await _userManager.AddToRoleAsync(user, request.RequestedRole);

                // 3. Update Request Status
                request.Status = "Approved";
                await _context.SaveChangesAsync();

                // 4. Send Email
                var subject = "Account Approved - IRP System";
                var body = $"<h3>Welcome to IRP System, {request.FullName}!</h3>" +
                           $"<p>Your account request has been approved. You can now login using your email.</p>" +
                           $"<p><b>Temporary Password:</b> TempPass@123</p>" +
                           $"<p><i>Please change your password after your first login.</i></p>";

                try {
                    await _emailService.SendEmailAsync(request.Email, subject, body);
                } catch {
                    // Log error but proceed
                }

                // 5. Audit Log
                await _auditService.LogAsync(
                    "Account Approval",
                    "Account Management",
                    "ApplicationUser",
                    user.Id,
                    $"Account request for '{request.Email}' approved by {User.Identity?.Name}"
                );

                TempData["Success"] = $"Account for {request.Email} created successfully.";
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                TempData["Error"] = $"Error creating account: {errors}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // ❌ REJECT REQUEST (Chairman Only)
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chairman")]
        public async Task<IActionResult> Reject(int id)
        {
            var request = _context.AccountRequests.Find(id);
            if (request == null || request.Status != "Pending")
                return BadRequest();

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            // Audit Log
            await _auditService.LogAsync(
                "Account Rejection",
                "Account Management",
                "AccountRequest",
                id.ToString(),
                $"Account request for '{request.Email}' rejected by {User.Identity?.Name}"
            );

            TempData["Success"] = "Account request rejected.";
            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // 📝 REQUEST ACCOUNT FORM (Public)
        // ===============================
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> RequestAccount()
        {
            ViewBag.Branches = await _context.Branches.OrderBy(b => b.BranchName).ToListAsync();
            return View();
        }


        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestAccount(string fullName, string email, int? branchId)

        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || !branchId.HasValue)
            {
                TempData["Error"] = "Please fill in all fields (including Barangay).";
                ViewBag.Branches = await _context.Branches.OrderBy(b => b.BranchName).ToListAsync();
                return View();
            }


            // Check if email already has a pending request
            var existing = _context.AccountRequests
                .Any(r => r.Email == email && r.Status == "Pending");

            if (existing)
            {
                TempData["Error"] = "A pending request for this email already exists.";
                ViewBag.Branches = await _context.Branches.OrderBy(b => b.BranchName).ToListAsync();
                return View();
            }


            var request = new AccountRequest
            {
                FullName = fullName,
                Email = email,
                RequestedRole = "Staff",
                Status = "Pending",
                DateRequested = DateTime.Now,
                BranchId = branchId
            };


            _context.AccountRequests.Add(request);
            _context.SaveChanges();

            TempData["Success"] = "Your account request has been submitted! You will receive an email once approved.";
            return RedirectToAction("RequestAccount");
        }
    }
}
