using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;


namespace Latog_Final_project.Controllers
{
    [Authorize(Roles = "Treasurer,Chairman")]
    public class TreasurerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IBudgetGuardService _budgetGuard;
        private readonly IBranchService _branchService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly InvoicePdfService _pdfService;

        public TreasurerController(
            ApplicationDbContext context, 
            IAuditService auditService,
            IBudgetGuardService budgetGuard,
            IBranchService branchService,
            UserManager<ApplicationUser> userManager,
            InvoicePdfService pdfService)
        {
            _context = context;
            _auditService = auditService;
            _budgetGuard = budgetGuard;
            _branchService = branchService;
            _userManager = userManager;
            _pdfService = pdfService;
        }


        // 1️⃣ DASHBOARD INDEX
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string period = "Monthly")
        {
            var branchId = _branchService.GetCurrentBranchId();
            if (branchId == null) return Unauthorized();

            var invoiceQuery = _context.Invoices
                .Include(i => i.ResourceRequest)
                .Where(i => i.BranchId == branchId);

            var requestQuery = _context.ResourceRequests
                .Where(r => r.BranchId == branchId && !r.IsArchived);

            if (startDate.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate >= startDate.Value);
                requestQuery = requestQuery.Where(r => r.RequestDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate <= endDate.Value);
                requestQuery = requestQuery.Where(r => r.RequestDate <= endDate.Value);
            }

            var invoices = await invoiceQuery.ToListAsync();
            var resourceRequests = await requestQuery.ToListAsync();
            
            var viewModel = new TreasurerDashboardViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalSpending = invoices.Sum(i => i.TotalAmount),
                PaidAmount = invoices.Where(i => i.PaymentStatus == "Paid").Sum(i => i.TotalAmount),
                UnpaidAmount = invoices.Where(i => i.PaymentStatus != "Paid").Sum(i => i.TotalAmount),
                TotalInvoices = invoices.Count,
                PendingRequests = await _context.ResourceRequests.CountAsync(r => r.BranchId == branchId && (r.Status == "Pending" || r.Status == "Approved") && !r.IsArchived),
                OverdueInvoices = invoices.Count(i => i.PaymentStatus != "Paid" && i.DueDate.HasValue && i.DueDate.Value < DateTime.Now),
                FilterPeriod = period,

                PendingPayments = invoices.Where(i => i.PaymentStatus != "Paid")
                    .OrderByDescending(i => i.CreatedDate)
                    .Take(5)
                    .ToList()
            };


            // Prepare Spending Chart Data
            viewModel.SpendingChartData = GetChartDataPoints(invoices, period, startDate, endDate);

            // Prepare Invoice Status Data
            viewModel.InvoiceStatusData = new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Paid", Value = invoices.Count(i => i.PaymentStatus == "Paid") },
                new ChartDataPoint { Label = "Partially Paid", Value = invoices.Count(i => i.PaymentStatus == "Partially Paid") },
                new ChartDataPoint { Label = "Unpaid", Value = invoices.Count(i => i.PaymentStatus == "Unpaid") }
            };

            // 💎 NEW PREMIUM DATA CALCULATION
            
            // 1. Category Distribution (Spending Insights)
            viewModel.CategoryDistribution = resourceRequests
                .Where(r => !string.IsNullOrEmpty(r.Category))
                .GroupBy(r => r.Category)
                .Select(g => new ChartDataPoint { 
                    Label = g.Key, 
                    Value = g.Sum(r => r.TotalAmount ?? 0) 
                })
                .OrderByDescending(d => d.Value)
                .ToList();

            // 2. Item Volume (Sales Volume)
            viewModel.ItemVolumeData = resourceRequests
                .GroupBy(r => r.ItemName)
                .Select(g => new ChartDataPoint { 
                    Label = g.Key, 
                    Value = g.Sum(r => r.Quantity) 
                })
                .OrderByDescending(d => d.Value)
                .Take(10)
                .ToList();

            // 3. Peak Hours (Throughput)
            viewModel.PeakHoursData = resourceRequests
                .GroupBy(r => r.RequestDate.Hour)
                .Select(g => new ChartDataPoint { 
                    Label = new DateTime(1, 1, 1, g.Key, 0, 0).ToString("hh tt"), 
                    Value = g.Count() 
                })
                .OrderBy(d => d.Label)
                .ToList();

            // 4. Top Products
            var topItems = resourceRequests
                .GroupBy(r => r.ItemName)
                .Select(g => new TopPerformingProduct
                {
                    ProductName = g.Key,
                    TotalSold = g.Sum(r => r.Quantity),
                    RevenueGenerated = g.Sum(r => r.TotalAmount ?? 0)
                })
                .OrderByDescending(p => p.TotalSold)
                .Take(5)
                .ToList();

            // Calculate progress relative to the highest volume item
            int maxVolume = topItems.Any() ? topItems.First().TotalSold : 1;
            foreach (var item in topItems)
            {
                item.Progress = (item.TotalSold * 100) / maxVolume;
            }
            viewModel.TopProducts = topItems;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetSpendingTrendData(string period, DateTime? startDate = null, DateTime? endDate = null)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var query = _context.Invoices.Where(i => i.BranchId == branchId);

            if (startDate.HasValue) query = query.Where(i => i.CreatedDate >= startDate.Value);
            if (endDate.HasValue) query = query.Where(i => i.CreatedDate <= endDate.Value);

            var invoices = await query.ToListAsync();
            var trendData = GetChartDataPoints(invoices, period, startDate, endDate);
            
            var statusData = new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Paid", Value = invoices.Count(i => i.PaymentStatus == "Paid") },
                new ChartDataPoint { Label = "Partially Paid", Value = invoices.Count(i => i.PaymentStatus == "Partially Paid") },
                new ChartDataPoint { Label = "Unpaid", Value = invoices.Count(i => i.PaymentStatus == "Unpaid") }
            };

            return Json(new { 
                labels = trendData.Select(d => d.Label).ToList(),
                values = trendData.Select(d => d.Value).ToList(),
                statusLabels = statusData.Select(d => d.Label).ToList(),
                statusValues = statusData.Select(d => d.Value).ToList()
            });
        }

        private List<ChartDataPoint> GetChartDataPoints(List<Invoice> invoices, string period, DateTime? startDate = null, DateTime? endDate = null)
        {
            var now = DateTime.Now;

            // Handle Custom Date Range First
            if (startDate.HasValue && endDate.HasValue)
            {
                return invoices
                    .GroupBy(i => i.CreatedDate.Date)
                    .Select(g => new ChartDataPoint
                    {
                        Label = g.Key.ToString("MMM dd"),
                        Value = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(d => DateTime.ParseExact(d.Label, "MMM dd", null))
                    .ToList();
            }

            if (period == "Weekly")
            {
                var fourWeeksAgo = now.AddDays(-28);
                return invoices
                    .Where(i => i.CreatedDate >= fourWeeksAgo)
                    .GroupBy(i => ISOWeek.GetWeekOfYear(i.CreatedDate))
                    .Select(g => new ChartDataPoint
                    {
                        Label = "Week " + g.Key,
                        Value = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(d => d.Label)
                    .ToList();
            }
            else if (period == "Yearly")
            {
                var fiveYearsAgo = now.AddYears(-5);
                return invoices
                    .Where(i => i.CreatedDate >= fiveYearsAgo.Date)
                    .GroupBy(i => i.CreatedDate.Year)
                    .Select(g => new ChartDataPoint
                    {
                        Label = g.Key.ToString(),
                        Value = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(d => d.Label)
                    .ToList();
            }
            else // Monthly (Default)
            {
                var sixMonthsAgo = now.AddMonths(-6);
                return invoices
                    .Where(i => i.CreatedDate >= sixMonthsAgo)
                    .GroupBy(i => new { i.CreatedDate.Year, i.CreatedDate.Month })
                    .Select(g => new ChartDataPoint
                    {
                        Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Value = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(d => DateTime.ParseExact(d.Label, "MMM yyyy", null))
                    .ToList();
            }
        }
        

        // 1.5️⃣ PENDING RESOURCE REQUESTS (FROM STAFF)
        public async Task<IActionResult> PendingRequests(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();

            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && (r.Status == "Pending" || r.Status == "Pending Treasurer Review") && !r.IsArchived);


            var totalRecords = await query.CountAsync();

            var pending = await query
                .OrderByDescending(r => r.RequestDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new ResourceRequestViewModel
            {
                Requests = pending,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Treasurer",
                ActionName = "PendingRequests"
            };


            ViewBag.Categories = await _context.Budgets
                .Where(b => b.BranchId == branchId)
                .OrderBy(b => b.CategoryName)
                .ToListAsync();


            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_PendingRequestsTable", model);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForwardToChairman(int id, string Category)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);
            
            if (request == null || (request.Status != "Pending" && request.Status != "Pending Treasurer Review"))
                return BadRequest();

            if (string.IsNullOrEmpty(Category))
            {
                TempData["Error"] = "Please select a budget category before forwarding.";
                return RedirectToAction(nameof(PendingRequests));
            }


          
            decimal amountToCheck = request.EstimatedAmount ?? 0;
            var budgetResult = await _budgetGuard.CheckBudgetAsync(Category, amountToCheck, branchId.Value);

            if (!budgetResult.IsWithinBudget)
            {
                TempData["Error"] = "Insufficient Funds";
                return RedirectToAction(nameof(PendingRequests));
            }

            request.Status = "ForChairman";
            request.Category = Category;
            request.ForwardedByUserId = _userManager.GetUserId(User);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Forward",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' (Qty: {request.Quantity}) assigned to '{Category}' and forwarded to Chairman"
            );

            return RedirectToAction(nameof(PendingRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectResourceRequest(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId);
            
            if (request == null || (request.Status != "Pending" && request.Status != "Pending Treasurer Review"))
                return BadRequest();

            request.Status = "Rejected";
            request.DecisionDate = DateTime.Now;
            request.ForwardedByUserId = _userManager.GetUserId(User);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Reject",
                "Purchase Requisition",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' REJECTED manually by Treasurer"
            );

            TempData["Success"] = "Request rejected successfully.";
            return RedirectToAction(nameof(PendingRequests));
        }

        // 3️⃣ APPROVED REQUESTS (WAITING FOR PO)
        public IActionResult Approved(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && r.Status == "Approved" && !r.HasPurchaseOrder && !r.IsArchived);

            var totalRecords = query.Count();

            var approved = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = approved,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Treasurer",
                ActionName = "Approved"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ApprovedTable", model);
            }

            return View(model);
        }

        // 4️⃣ SHOW CREATE PO FORM
        [HttpGet]
        public async Task<IActionResult> CreatePO(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();


            var request = await _context.ResourceRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId && r.Status == "Approved");

            if (request == null)
                return NotFound();

            if (request.IsArchived)
                ViewBag.ActiveMenu = "Archive";
            else
                ViewBag.ActiveMenu = "POReceipt";

            ViewBag.Categories = await _context.Budgets
                .Where(b => b.BranchId == branchId)
                .OrderBy(b => b.CategoryName)
                .ToListAsync();
            return View(request);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePO(int id, string Supplier, decimal TotalAmount, string Category, DateTime? DueDate, string? Notes)
        {
            var branchId = _branchService.GetCurrentBranchId();

            var request = _context.ResourceRequests
                .FirstOrDefault(r => r.Id == id && r.BranchId == branchId && r.Status == "Approved");

            if (request == null)
                return BadRequest();





            // ✅ SAVE P.O DETAILS
            request.HasPurchaseOrder = true;
            request.Supplier = Supplier;
            request.TotalAmount = TotalAmount;
            
            // If the request already has a category from the Chairman phase, 
            // use that instead of the form input (enforce locking).
            if (!string.IsNullOrEmpty(request.Category))
            {
                Category = request.Category;
            }
            request.Category = Category; 

            // 🔥 AFTER P.O → WAITING DELIVERY
            request.Status = "ToReceive";

            // 💰 DEDUCT FROM BUDGET
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.BranchId == branchId && b.CategoryName == Category);

            if (budget != null)
            {
                decimal availableFunds = budget.MonthlyLimit - budget.CurrentSpending;
                if (TotalAmount > availableFunds)
                {
                    TempData["Error"] = "Insufficient Funds";
                    ViewBag.Categories = await _context.Budgets
                        .Where(b => b.BranchId == branchId)
                        .OrderBy(b => b.CategoryName).ToListAsync();

                    return View(request);
                }

                budget.CurrentSpending += TotalAmount;
                _context.Update(budget);

                // ✅ BUDGET AUDIT LOG
                await _auditService.LogAsync(
                    "Deduction",
                    "Budget Management",
                    "Budget",
                    budget.Id.ToString(),
                    $"Deducted {TotalAmount:C} from '{Category}' due to PO creation for '{request.ItemName}'"
                );
            }

            // 💰 AUTO-CREATE INVOICE
            // Generate invoice number: INV-YYYYMMDD-NNN
            var today = DateTime.Now;
            var todayPrefix = $"INV-{today:yyyyMMdd}";
            var todayCount = await _context.Invoices
                .CountAsync(i => i.InvoiceNumber.StartsWith(todayPrefix));
            var invoiceNumber = $"{todayPrefix}-{(todayCount + 1):D3}";

            var invoice = new Invoice
            {
                ResourceRequestId = id,
                Supplier = Supplier,
                TotalAmount = TotalAmount,
                PaymentStatus = "Unpaid",
                CreatedDate = today,
                DueDate = DueDate ?? today.AddDays(30),
                InvoiceNumber = invoiceNumber,
                Notes = Notes,
                BranchId = branchId.Value
            };

            _context.Invoices.Add(invoice);

            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Create",
                "Purchase Orders",
                "PurchaseOrder",
                id.ToString(),
                $"Purchase Order created for '{request.ItemName}' — Supplier: {Supplier}, Amount: ₱{TotalAmount:N2}. Budget '{Category}' updated. Invoice {invoiceNumber} auto-generated."
            );

            return RedirectToAction(nameof(PurchaseOrders));
        }

        // 📄 VIEW PO RECEIPTS
        public IActionResult POReceipt(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && r.HasPurchaseOrder && !r.IsArchived);


            var totalRecords = query.Count();

            var poList = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = poList,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Treasurer",
                ActionName = "POReceipt"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_POReceiptTable", model);
            }

            return View(model);
        }

        public IActionResult ViewReceipt(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.ResourceRequests
                .FirstOrDefault(r => r.Id == id && r.BranchId == branchId);

            if (item == null) return NotFound();

            return View(item);
        }

        // ✏️ EDIT PO
        [HttpGet]
        public IActionResult EditPO(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.ResourceRequests
                .FirstOrDefault(r => r.Id == id && r.BranchId == branchId);
            
            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPO(int id, string Supplier, decimal TotalAmount)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.ResourceRequests
                .FirstOrDefault(r => r.Id == id && r.BranchId == branchId);
            
            if (item == null) return NotFound();

            var oldSupplier = item.Supplier;
            var oldAmount = item.TotalAmount;

            item.Supplier = Supplier;
            item.TotalAmount = TotalAmount;

            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Update",
                "Purchase Orders",
                "PurchaseOrder",
                id.ToString(),
                $"PO updated for '{item.ItemName}' — Supplier: '{oldSupplier}' → '{Supplier}', Amount: ₱{oldAmount:N2} → ₱{TotalAmount:N2}"
            );

            return RedirectToAction("POReceipt");
        }

        // 🔥 RECEIVE ITEMS + AUTO INVENTORY STOCK-IN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveItems(int id, int receivedQty)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.ResourceRequests
                .FirstOrDefault(r => r.Id == id && r.BranchId == branchId);

            if (item == null || !item.HasPurchaseOrder)
                return BadRequest();

            // ❗ OPTIONAL: Prevent over receiving
            int currentReceived = item.ReceivedQuantity ?? 0;
            if (currentReceived + receivedQty > item.Quantity)
            {
                return BadRequest("Received quantity exceeds ordered quantity.");
            }

            // ✅ UPDATE RECEIVED
            item.ReceivedQuantity = currentReceived + receivedQty;

            // 🔥 AUTO INVENTORY UPDATE
            var inventoryItem = _context.Inventories
                .FirstOrDefault(i => i.BranchId == branchId && i.ItemName == item.ItemName);


            int previousStock = 0;

            if (inventoryItem != null)
            {
                previousStock = inventoryItem.Quantity;
                inventoryItem.Quantity += receivedQty;
            }
            else
            {
                inventoryItem = new Inventory
                {
                    ItemName = item.ItemName,
                    Quantity = receivedQty,
                    BranchId = branchId.Value
                };
                _context.Inventories.Add(inventoryItem);

            }

            // 📝 LOG STOCK-IN TRANSACTION
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ItemName = item.ItemName,
                TransactionType = "Stock-In",
                Quantity = receivedQty,
                PreviousStock = previousStock,
                NewStock = previousStock + receivedQty,
                Reason = $"PO Received — {item.ItemName} (Supplier: {item.Supplier ?? "N/A"})",
                PerformedBy = User.Identity?.Name ?? "System",
                TransactionDate = DateTime.Now,
                BranchId = branchId.Value
            });


            // ✅ STATUS LOGIC
            if (item.ReceivedQuantity < item.Quantity)
            {
                item.Status = "Partially Received";
            }
            else
            {
                item.Status = "Completed";
            }

            item.DecisionDate = DateTime.Now;

            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Receive",
                "Inventory",
                "ResourceRequest",
                id.ToString(),
                $"Received {receivedQty} units of '{item.ItemName}' — Stock: {previousStock} → {previousStock + receivedQty} (Supplier: {item.Supplier ?? "N/A"})"
            );

            return RedirectToAction("POReceipt");
        }

        // 6️⃣ MARK AS COMPLETED (MANUAL)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var request = _context.ResourceRequests.Find(id);
            if (request == null || !request.HasPurchaseOrder)
                return BadRequest();

            request.Status = "Completed";
            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Complete",
                "Purchase Orders",
                "ResourceRequest",
                id.ToString(),
                $"Purchase Order for '{request.ItemName}' marked as Completed by Treasurer"
            );

            return RedirectToAction(nameof(PurchaseOrders));
        }

        // 7️⃣ VIEW PURCHASE ORDERS
        public IActionResult PurchaseOrders(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && r.HasPurchaseOrder && !r.IsArchived);


            var totalRecords = query.Count();

            var purchaseOrders = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new ResourceRequestViewModel
            {
                Requests = purchaseOrders,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Treasurer",
                ActionName = "PurchaseOrders"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_PurchaseOrdersTable", model);
            }

            return View(model);
        }

        // ===============================
        // 💰 INVOICES
        // ===============================
        public IActionResult Invoices(int page = 1, string search = null)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();


            var query = _context.Invoices
                .Include(i => i.ResourceRequest)
                .Where(i => i.BranchId == branchId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s)) ||
                    (i.ResourceRequest != null && i.ResourceRequest.ItemName.ToLower().Contains(s)) ||
                    (i.Supplier != null && i.Supplier.ToLower().Contains(s)) ||
                    i.PaymentStatus.ToLower().Contains(s)
                );
            }

            var totalRecords = query.Count();

            var invoices = query
                .OrderByDescending(i => i.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new InvoiceViewModel
            {
                Invoices = invoices,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            // Overdue count for the banner
            ViewBag.OverdueCount = _context.Invoices
                .Count(i => i.BranchId == branchId && i.PaymentStatus != "Paid" && i.DueDate.HasValue && i.DueDate.Value < DateTime.Now);

            ViewBag.CurrentSearch = search;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_InvoicesTable", model);
            }

            return View(model);
        }

        // 💰 MARK AS PAID (SUPPORTS PARTIAL PAYMENTS)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id, decimal amountPaid)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var invoice = _context.Invoices
                .FirstOrDefault(i => i.Id == id && i.BranchId == branchId);
            
            if (invoice == null)
                return NotFound();

            if (invoice.PaymentStatus == "Paid")
                return RedirectToAction(nameof(Invoices));

            // Add to existing amount paid
            invoice.AmountPaid += amountPaid;

            if (invoice.AmountPaid >= invoice.TotalAmount)
            {
                invoice.AmountPaid = invoice.TotalAmount; // Cap at total
                invoice.PaymentStatus = "Paid";
                invoice.PaidDate = DateTime.Now;
            }
            else
            {
                invoice.PaymentStatus = "Partially Paid";
            }

            _context.SaveChanges();

            var balance = invoice.TotalAmount - invoice.AmountPaid;

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Update",
                "Invoice & Payment",
                "Invoice",
                id.ToString(),
                $"Payment of ₱{amountPaid:N2} recorded for Invoice {invoice.InvoiceNumber} ('{invoice.Supplier}'). Total Paid: ₱{invoice.AmountPaid:N2}, Balance: ₱{balance:N2}. Status: {invoice.PaymentStatus}"
            );

            TempData["Success"] = invoice.PaymentStatus == "Paid"
                ? "Invoice fully paid!"
                : $"Partial payment of ₱{amountPaid:N2} recorded. Balance: ₱{balance:N2}";
            return RedirectToAction(nameof(Invoices));
        }

        // 📎 UPLOAD ATTACHMENT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int id, IFormFile attachment)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId);
            
            if (invoice == null) return NotFound();

            if (attachment == null || attachment.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Invoices));
            }

            // Validate file size (5MB max)
            if (attachment.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "File size must be less than 5MB.";
                return RedirectToAction(nameof(Invoices));
            }

            // Validate file type
            var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
            {
                TempData["Error"] = "Only PDF, JPG, and PNG files are allowed.";
                return RedirectToAction(nameof(Invoices));
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "invoices");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"invoice_{invoice.Id}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await attachment.CopyToAsync(stream);
            }

            invoice.AttachmentPath = $"/uploads/invoices/{fileName}";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "Upload",
                "Invoice & Payment",
                "Invoice",
                id.ToString(),
                $"Attachment uploaded for Invoice {invoice.InvoiceNumber}: {attachment.FileName}"
            );

            TempData["Success"] = "Attachment uploaded successfully.";
            return RedirectToAction(nameof(Invoices));
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoicePdf(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var invoice = await _context.Invoices
                .Include(i => i.ResourceRequest)
                .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId);

            if (invoice == null) return NotFound();

            var pdfBytes = _pdfService.GeneratePdf(invoice);
            var fileName = string.IsNullOrEmpty(invoice.InvoiceNumber) 
                ? $"Invoice_#{invoice.Id}.pdf" 
                : $"{invoice.InvoiceNumber}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
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
                "Purchase Orders",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' archived by Treasurer"
            );

            TempData["Success"] = "Request archived successfully.";
            return RedirectToAction(nameof(POReceipt));
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
                "Purchase Orders",
                "ResourceRequest",
                id.ToString(),
                $"Request '{request.ItemName}' restored from archive by Treasurer"
            );

            TempData["Success"] = "Request restored from archive.";
            return RedirectToAction(nameof(ArchivedList));
        }

        // 📦 VIEW ARCHIVED LIST
        public IActionResult ArchivedList(int page = 1, string filter = "all")
        {
            var branchId = _branchService.GetCurrentBranchId();

            var query = _context.ResourceRequests
                .Include(r => r.User)
                .Where(r => r.BranchId == branchId && r.IsArchived);


            // Apply filter
            if (filter == "po")
                query = query.Where(r => r.HasPurchaseOrder && r.ReceivedQuantity == null);
            else if (filter == "receipt")
                query = query.Where(r => r.HasPurchaseOrder && r.ReceivedQuantity != null);

            int pageSize = 10;
            var totalRecords = query.Count();


            var archived = query
                .OrderByDescending(r => r.ArchivedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentFilter = filter;

            var model = new ResourceRequestViewModel
            {
                Requests = archived,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                ControllerName = "Treasurer",
                ActionName = "ArchivedList"
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_TreasurerArchivedListTable", model);
            }

            return View(model);
        }

        // 1️⃣1️⃣ BUDGET MANAGEMENT
        public async Task<IActionResult> Budgets()
        {
            var branchId = _branchService.GetCurrentBranchId();
            var budgets = await _context.Budgets
                .Where(b => b.BranchId == branchId)
                .ToListAsync();
            return View(budgets);
        }


        [HttpGet]
        [Authorize(Roles = "Treasurer")]
        public IActionResult AddBudget()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Treasurer")]
        public async Task<IActionResult> AddBudget(Budget budget)
        {
            if (!ModelState.IsValid) return View(budget);

            var branchId = _branchService.GetCurrentBranchId();
            if (branchId.HasValue)
            {
                budget.BranchId = branchId.Value;
            }

            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();


            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Create",
                "Budget Management",
                "Budget",
                budget.Id.ToString(),
                $"New budget category created: '{budget.CategoryName}' with limit {budget.MonthlyLimit:C}"
            );

            TempData["Success"] = $"New budget category '{budget.CategoryName}' added.";
            return RedirectToAction(nameof(Budgets));
        }

        [HttpGet]
        [Authorize(Roles = "Treasurer")]
        public async Task<IActionResult> EditBudget(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.Id == id && b.BranchId == branchId);

            if (budget == null) return NotFound();
            return View(budget);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Treasurer")]
        public async Task<IActionResult> EditBudget(Budget budget)
        {
            if (!ModelState.IsValid) return View(budget);

            var branchId = _branchService.GetCurrentBranchId();
            var existing = await _context.Budgets
                .FirstOrDefaultAsync(b => b.Id == budget.Id && b.BranchId == branchId);

            if (existing == null) return NotFound();


            decimal oldLimit = existing.MonthlyLimit;
            existing.MonthlyLimit = budget.MonthlyLimit;

            _context.Update(existing);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Update",
                "Budget Management",
                "Budget",
                existing.Id.ToString(),
                $"Budget limit for '{existing.CategoryName}' updated from {oldLimit:C} to {existing.MonthlyLimit:C}"
            );

            TempData["Success"] = $"Budget for {existing.CategoryName} updated successfully.";
            return RedirectToAction(nameof(Budgets));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Treasurer")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.Id == id && b.BranchId == branchId);

            if (budget == null) return NotFound();


            var categoryName = budget.CategoryName;
            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Delete",
                "Budget Management",
                "Budget",
                id.ToString(),
                $"Budget category deleted: '{categoryName}'"
            );

            TempData["Success"] = $"Budget category '{categoryName}' deleted successfully.";
            return RedirectToAction(nameof(Budgets));
        }
    }
}
