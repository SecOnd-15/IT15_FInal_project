using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Latog_Final_project.Models;
using Latog_Final_project.Data;
using Latog_Final_project.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Latog_Final_project.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public HomeController(
            SignInManager<ApplicationUser> signInManager, 
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _auditService = auditService;
        }

        // PUBLIC HOME PAGE
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        // =============================================
        // ✅ DASHBOARD STATISTICS (CENTRALIZED)
        // =============================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault();

            object stats;

            if (mainRole == "SuperAdmin")
            {
                stats = new
                {
                    pendingRequests = await _context.ResourceRequests.CountAsync(r => r.Status == "Pending"),
                    approvedRequests = await _context.ResourceRequests.CountAsync(r => r.Status == "Approved"),
                    rejectedRequests = await _context.ResourceRequests.CountAsync(r => r.Status == "Rejected"),
                    totalUsers = await _context.Users.CountAsync(),
                    totalAuditLogs = await _context.AuditLogs.CountAsync()
                };
            }
            else if (mainRole == "Chairman")
            {
                stats = new
                {
                    totalRequests = await _context.ResourceRequests.CountAsync(r => r.BranchId == user.BranchId),
                    approvedRequests = await _context.ResourceRequests.CountAsync(r => r.BranchId == user.BranchId && (r.Status == "Approved" || r.Status == "ToReceive" || r.Status == "Partially Received" || r.Status == "Completed")),
                    rejectedRequests = await _context.ResourceRequests.CountAsync(r => r.BranchId == user.BranchId && r.Status == "Rejected"),
                    pendingApprovals = await _context.ResourceRequests.CountAsync(r => r.BranchId == user.BranchId && (r.Status == "Pending" || r.Status == "ForChairman")),
                    totalSpending = await _context.Invoices.Where(i => i.BranchId == user.BranchId && i.PaymentStatus == "Paid").SumAsync(i => i.TotalAmount),
                    paidInvoices = await _context.Invoices.CountAsync(i => i.BranchId == user.BranchId && i.PaymentStatus == "Paid"),
                    unpaidInvoices = await _context.Invoices.CountAsync(i => i.BranchId == user.BranchId && i.PaymentStatus == "Unpaid")
                };
            }
            else if (mainRole == "Treasurer")
            {
                stats = new
                {
                    pendingRequests = await _context.ResourceRequests.CountAsync(r => r.BranchId == user.BranchId && (r.Status == "Pending" || r.Status == "Approved") && !r.IsArchived),
                    unpaidInvoices = await _context.Invoices.CountAsync(i => i.BranchId == user.BranchId && i.PaymentStatus == "Unpaid"),
                    paidInvoices = await _context.Invoices.CountAsync(i => i.BranchId == user.BranchId && i.PaymentStatus == "Paid"),
                    unpaidAmount = await _context.Invoices.Where(i => i.BranchId == user.BranchId && i.PaymentStatus == "Unpaid").SumAsync(i => i.TotalAmount),
                    paidAmount = await _context.Invoices.Where(i => i.BranchId == user.BranchId && i.PaymentStatus == "Paid").SumAsync(i => i.TotalAmount),
                    totalRevenue = await _context.Invoices.Where(i => i.BranchId == user.BranchId).SumAsync(i => i.TotalAmount)
                };
            }
            else if (mainRole == "Staff")
            {
                stats = new
                {
                    myPendingRequests = await _context.ResourceRequests.CountAsync(r => r.UserId == user.Id && r.Status == "Pending"),
                    myTotalRequests = await _context.ResourceRequests.CountAsync(r => r.UserId == user.Id)
                };
            }
            else
            {
                stats = new { };
            }

            return Json(stats);
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        // ✅ LOGOUT METHOD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // ✅ AUDIT LOG (before sign out, so user is still authenticated)
            await _auditService.LogAsync(
                "Logout",
                "Authentication",
                "ApplicationUser",
                "",
                $"User '{User.Identity?.Name}' logged out"
            );

            await _signInManager.SignOutAsync();

            // 🔥 ALWAYS go to Login
            return Redirect("/Identity/Account/Login");
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
