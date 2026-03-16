using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;
using Microsoft.EntityFrameworkCore;

namespace Latog_Final_project.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IBranchService _branchService;
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffController(
            ApplicationDbContext context,
            IAuditService auditService,
            IBranchService branchService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _auditService = auditService;
            _branchService = branchService;
            _userManager = userManager;
        }

        // ============================
        // 📋 MY REQUESTS
        // ============================
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _context.ResourceRequests
                .Where(r => r.UserId == user.Id && r.IsArchived != true)
                .OrderByDescending(r => r.RequestDate);

            var totalRecords = await query.CountAsync();

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new StaffRequestsViewModel
            {
                Requests = requests,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_StaffRequestsTable", model);
            }

            return View(model);
        }

        // ➕ CREATE REQUEST
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ResourceRequest request)
        {
            if (!ModelState.IsValid)
                return View(request);

            request.Status = "Pending Treasurer Review";
            request.RequestDate = DateTime.Now;
            request.UserId = _userManager.GetUserId(User)!;

            // ✅ MULTI-TENANCY: Assign Branch
            var branchId = _branchService.GetCurrentBranchId();
            if (branchId.HasValue)
            {
                request.BranchId = branchId.Value;
            }

            _context.ResourceRequests.Add(request);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Create",
                "Purchase Requisition",
                "ResourceRequest",
                request.Id.ToString(),
                $"Resource request created: '{request.ItemName}' (Qty: {request.Quantity}, Purpose: {request.Purpose})"
            );

            TempData["Success"] = "Submitted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ✏️ EDIT (ONLY PENDING & OWN)
        public async Task<IActionResult> Edit(int id)
        {
            var request = await _context.ResourceRequests.FindAsync(id);

            if (request == null ||
                request.Status != "Pending" && request.Status != "Pending Treasurer Review" ||
                request.UserId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ResourceRequest request)
        {
            var existing = await _context.ResourceRequests.FindAsync(request.Id);

            if (existing == null ||
                (existing.Status != "Pending" && existing.Status != "Pending Treasurer Review") ||
                existing.UserId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var oldItem = existing.ItemName;
            var oldQty = existing.Quantity;
            var oldPurpose = existing.Purpose;
            var oldCategory = existing.Category;
            var oldAmount = existing.EstimatedAmount;

            existing.ItemName = request.ItemName;
            existing.Quantity = request.Quantity;
            existing.Purpose = request.Purpose;
            existing.Category = request.Category;
            existing.EstimatedAmount = request.EstimatedAmount;

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Update",
                "Purchase Requisition",
                "ResourceRequest",
                existing.Id.ToString(),
                $"Request updated: Item '{oldItem}' → '{request.ItemName}', Qty {oldQty} → {request.Quantity}, Category '{oldCategory}' → '{request.Category}', Est. Amount {oldAmount:C} → {request.EstimatedAmount:C}"
            );

            TempData["Success"] = "Request updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // 🗑 DELETE (ONLY PENDING & OWN)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var request = _context.ResourceRequests.Find(id);

            if (request == null ||
                request.Status != "Pending" ||
                request.UserId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var itemName = request.ItemName;
            var qty = request.Quantity;

            _context.ResourceRequests.Remove(request);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Delete",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Resource request deleted: '{itemName}' (Qty: {qty})"
            );

            return RedirectToAction(nameof(Index));
        }
    }
}
