using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;

namespace Latog_Final_project.Controllers
{
    [Authorize]
    public class ResourceRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public ResourceRequestsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // ================================
        // ALL ROLES: VIEW REQUESTS
        // ================================
        public IActionResult Index()
        {
            // Staff, Chairman, Treasurer can all view requests
            var requests = _context.ResourceRequests.ToList();
            return View(requests);
        }

        // ================================
        // STAFF: CREATE REQUEST
        // ================================
        [Authorize(Roles = "Staff")]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ResourceRequest request)
        {
            if (ModelState.IsValid)
            {
                request.Status = "Pending";
                request.RequestDate = DateTime.Now;

                _context.ResourceRequests.Add(request);
                _context.SaveChanges();

                // ✅ AUDIT LOG
                await _auditService.LogAsync(
                    "Create",
                    "Purchase Requisition",
                    "ResourceRequest",
                    request.Id.ToString(),
                    $"Resource request created: '{request.ItemName}' (Qty: {request.Quantity})"
                );

                return RedirectToAction(nameof(Index));
            }

            return View(request);
        }

        // ================================
        // CHAIRMAN: APPROVE REQUEST
        // ================================
        [Authorize(Roles = "Chairman")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var request = _context.ResourceRequests.Find(id);
            if (request == null) return NotFound();

            // Chairman decision
            request.Status = "Approved";
            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Approve",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' approved by Chairman"
            );

            return RedirectToAction(nameof(Index));
        }

        // ================================
        // CHAIRMAN: REJECT REQUEST
        // ================================
        [Authorize(Roles = "Chairman")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var request = _context.ResourceRequests.Find(id);
            if (request == null) return NotFound();

            request.Status = "Rejected";
            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Reject",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' rejected by Chairman"
            );

            return RedirectToAction(nameof(Index));
        }

        // ================================
        // TREASURER: UPDATE INVENTORY
        // ================================
        [Authorize(Roles = "Treasurer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInventory(int id)
        {
            var request = _context.ResourceRequests.Find(id);
            if (request == null) return NotFound();

            // Treasurer can ONLY process approved requests
            if (request.Status != "Approved")
            {
                return Forbid();
            }

            // TEMP / DOCUMENTATION-SAFE LOGIC
            request.Status = "Completed";

            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Update",
                "Inventory",
                "ResourceRequest",
                id.ToString(),
                $"Inventory updated and request '{request.ItemName}' marked as Completed by Treasurer"
            );

            return RedirectToAction(nameof(Index));
        }
    }
}
