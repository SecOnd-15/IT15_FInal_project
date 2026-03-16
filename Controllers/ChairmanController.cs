using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;

namespace Latog_Final_project.Controllers
{
    [Authorize(Roles = "Chairman")]
    public class ChairmanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IBranchService _branchService;

        public ChairmanController(ApplicationDbContext context, IAuditService auditService, IBranchService branchService)
        {
            _context = context;
            _auditService = auditService;
            _branchService = branchService;
        }


        // ===============================
        // ✅ DASHBOARD (CARD STYLE)
        // ===============================
        public IActionResult Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            var branchId = _branchService.GetCurrentBranchId();
            
            var requestQuery = _context.ResourceRequests.Where(r => r.BranchId == branchId);
            var invoiceQuery = _context.Invoices.Where(i => i.BranchId == branchId);

            if (startDate.HasValue)
            {
                requestQuery = requestQuery.Where(r => r.RequestDate >= startDate.Value);
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                requestQuery = requestQuery.Where(r => r.RequestDate <= endDate.Value);
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate <= endDate.Value);
            }

            var allRequests = requestQuery.ToList();
            var allInvoices = invoiceQuery.ToList();


            var model = new Latog_Final_project.Models.ProcurementReportViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRequests = allRequests.Count,
                ApprovedRequests = allRequests.Count(r => r.Status == "Approved" || r.Status == "ToReceive" || r.Status == "Partially Received" || r.Status == "Completed"),
                RejectedRequests = allRequests.Count(r => r.Status == "Rejected"),
                PendingRequests = allRequests.Count(r => r.Status == "Pending" || r.Status == "ForChairman"),
                TotalSpending = allInvoices.Where(i => i.PaymentStatus == "Paid").Sum(i => i.TotalAmount),
                PaidInvoices = allInvoices.Count(i => i.PaymentStatus == "Paid"),
                UnpaidInvoices = allInvoices.Count(i => i.PaymentStatus == "Unpaid"),

                // 💎 BI DATA CALCULATIONS
                SpendingByCategory = allRequests
                    .Where(r => !string.IsNullOrEmpty(r.Category))
                    .GroupBy(r => r.Category)
                    .Select(g => new ChartDataPoint
                    {
                        Label = g.Key,
                        Value = g.Sum(r => r.TotalAmount ?? 0)
                    })
                    .OrderByDescending(d => d.Value)
                    .ToList(),

                MonthlySpendingTrend = (startDate.HasValue && endDate.HasValue) ?
                    allInvoices
                        .GroupBy(i => i.CreatedDate.Date)
                        .Select(g => new ChartDataPoint
                        {
                            Label = g.Key.ToString("MMM dd"),
                            Value = g.Sum(i => i.TotalAmount)
                        })
                        .OrderBy(d => DateTime.ParseExact(d.Label, "MMM dd", null))
                        .ToList()
                    :
                    allInvoices
                        .GroupBy(i => new { i.CreatedDate.Year, i.CreatedDate.Month })
                        .Select(g => new ChartDataPoint
                        {
                            Label = $"{System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month)} {g.Key.Year}",
                            Value = g.Sum(i => i.TotalAmount)
                        })
                        .OrderBy(d => d.Label) // Note: This might need better sorting for multi-year but fine for current year
                        .Take(12)
                        .ToList(),

                RequestStatusDistribution = new List<ChartDataPoint>
                {
                    new ChartDataPoint { Label = "Approved", Value = allRequests.Count(r => r.Status == "Approved" || r.Status == "Completed") },
                    new ChartDataPoint { Label = "Pending", Value = allRequests.Count(r => r.Status == "Pending" || r.Status == "ForChairman") },
                    new ChartDataPoint { Label = "Rejected", Value = allRequests.Count(r => r.Status == "Rejected") }
                },

                RecentApprovals = allRequests
                    .Where(r => r.Status == "Approved" || r.Status == "Completed")
                    .OrderByDescending(r => r.DecisionDate)
                    .Take(5)
                    .ToList()
            };

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(model);
            }

            return View(model);
        }

        // ===============================
        // ✅ VIEW REQUESTS FOR APPROVAL
        // ===============================
        public IActionResult ForApproval(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Include(r => r.ForwardedByUser)
                .Where(r => r.BranchId == branchId && r.Status == "ForChairman");


            var totalRecords = query.Count();

            var requests = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = requests,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Chairman",
                ActionName = "ForApproval"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ForApprovalTable", model);
            }

            return View(model);
        }

        // ===============================
        // ✅ APPROVE REQUEST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);

            if (request == null || request.Status != "ForChairman")

                return BadRequest();

            request.Status = "Approved";
            request.DecisionDate = DateTime.Now;

            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Approve",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' (Qty: {request.Quantity}) approved by Chairman"
            );

            return RedirectToAction(nameof(ForApproval));
        }

        // ===============================
        // ❌ REJECT REQUEST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);

            if (request == null || request.Status != "ForChairman")

                return BadRequest();

            request.Status = "Rejected";
            request.DecisionDate = DateTime.Now;

            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Reject",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' (Qty: {request.Quantity}) rejected by Chairman"
            );

            return RedirectToAction(nameof(ForApproval));
        }

        // ===============================
        // 📋 VIEW REJECTED ONLY
        // ===============================
        public IActionResult RejectedCompleted(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && (r.Status == "Rejected" || r.Status == "Completed") && !r.IsArchived);


            var totalRecords = query.Count();

            var requests = query
                .OrderByDescending(r => r.DecisionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = requests,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Chairman",
                ActionName = "RejectedCompleted"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_RejectedCompletedTable", model);
            }

            return View(model);
        }

        // ===============================
        // 📊 PROCUREMENT REPORTS
        // ===============================
        public IActionResult Reports()
        {
            var branchId = _branchService.GetCurrentBranchId();
            var allRequests = _context.ResourceRequests
                .Where(r => r.BranchId == branchId).ToList();
            var allInvoices = _context.Invoices
                .Where(i => i.BranchId == branchId).ToList();


            var model = new Latog_Final_project.Models.ProcurementReportViewModel
            {
                TotalRequests = allRequests.Count,
                ApprovedRequests = allRequests.Count(r => r.Status == "Approved" || r.Status == "ToReceive" || r.Status == "Partially Received" || r.Status == "Completed"),
                RejectedRequests = allRequests.Count(r => r.Status == "Rejected"),
                PendingRequests = allRequests.Count(r => r.Status == "Pending" || r.Status == "ForChairman"),
                TotalSpending = allInvoices.Where(i => i.PaymentStatus == "Paid").Sum(i => i.TotalAmount),
                PaidInvoices = allInvoices.Count(i => i.PaymentStatus == "Paid"),
                UnpaidInvoices = allInvoices.Count(i => i.PaymentStatus == "Unpaid")
            };

            return View(model);
        }

        // ===============================
        // 📦 ARCHIVE
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);

            if (request == null)

                return NotFound();

            if (request.Status != "Completed" && request.Status != "Rejected")
                return BadRequest("Only Completed or Rejected requests can be archived.");

            request.IsArchived = true;
            request.ArchivedDate = DateTime.Now;
            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Archive",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' archived by Chairman"
            );

            TempData["Success"] = "Request archived successfully.";
            return RedirectToAction(nameof(RejectedCompleted));
        }

        // 📦 UNARCHIVE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);

            if (request == null)

                return NotFound();

            request.IsArchived = false;
            request.ArchivedDate = null;
            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Unarchive",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' restored from archive by Chairman"
            );

            TempData["Success"] = "Request restored from archive.";
            return RedirectToAction(nameof(ArchivedList));
        }

        // 📦 VIEW ARCHIVED LIST
        public IActionResult ArchivedList(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && r.IsArchived);


            var totalRecords = query.Count();

            var archived = query
                .OrderByDescending(r => r.ArchivedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = archived,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Chairman",
                ActionName = "ArchivedList"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ArchivedListTable", model);
            }

            return View(model);
        }

        // 💰 VIEW BUDGETS (READ-ONLY)
        public async Task<IActionResult> Budgets()
        {
            var branchId = _branchService.GetCurrentBranchId();
            var budgets = await _context.Budgets
                .Where(b => b.BranchId == branchId)
                .ToListAsync();
            return View(budgets);
        }

    }
}
