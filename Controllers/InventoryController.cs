using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;

namespace Latog_Final_project.Controllers
{
    [Authorize(Roles = "Treasurer")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IBranchService _branchService;

        public InventoryController(ApplicationDbContext context, IAuditService auditService, IBranchService branchService)
        {
            _context = context;
            _auditService = auditService;
            _branchService = branchService;
        }


        // ===============================
        // 📦 INVENTORY LIST
        // ===============================
        public IActionResult Index(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();
            var query = _context.Inventories
                .Where(i => i.BranchId == branchId)
                .OrderBy(i => i.ItemName);

            var totalRecords = query.Count();

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new InventoryViewModel
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_InventoryTable", model);
            }

            return View(model);
        }

        // ===============================
        // 📤 STOCK-OUT — FORM
        // ===============================
        [HttpGet]
        public IActionResult StockOut(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.Inventories
                .FirstOrDefault(i => i.Id == id && i.BranchId == branchId);
            if (item == null) return NotFound();


            return View(item);
        }

        // 📤 STOCK-OUT — PROCESS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(int id, int quantity, string reason)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.Inventories
                .FirstOrDefault(i => i.Id == id && i.BranchId == branchId);
            if (item == null) return NotFound();


            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                return RedirectToAction(nameof(StockOut), new { id });
            }

            if (quantity > item.Quantity)
            {
                TempData["Error"] = "Stock-Out quantity exceeds available stock.";
                return RedirectToAction(nameof(StockOut), new { id });
            }

            int previousStock = item.Quantity;
            item.Quantity -= quantity;

            // 📝 LOG TRANSACTION
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ItemName = item.ItemName,
                TransactionType = "Stock-Out",
                Quantity = quantity,
                PreviousStock = previousStock,
                NewStock = item.Quantity,
                Reason = reason,
                PerformedBy = User.Identity?.Name ?? "System",
                TransactionDate = DateTime.Now,
                BranchId = branchId.Value
            });


            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Stock-Out",
                "Inventory",
                "Inventory",
                id.ToString(),
                $"Stock-Out of {quantity} '{item.ItemName}' — Stock: {previousStock} → {item.Quantity}. Reason: {reason}"
            );

            TempData["Success"] = $"Stock-Out of {quantity} '{item.ItemName}' processed successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // 🔧 STOCK ADJUSTMENT — FORM
        // ===============================
        [HttpGet]
        public IActionResult StockAdjust(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.Inventories
                .FirstOrDefault(i => i.Id == id && i.BranchId == branchId);
            if (item == null) return NotFound();


            return View(item);
        }

        // 🔧 STOCK ADJUSTMENT — PROCESS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockAdjust(int id, int newQuantity, string reason)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var item = _context.Inventories
                .FirstOrDefault(i => i.Id == id && i.BranchId == branchId);
            if (item == null) return NotFound();


            if (newQuantity < 0)
            {
                TempData["Error"] = "Adjusted quantity cannot be negative.";
                return RedirectToAction(nameof(StockAdjust), new { id });
            }

            int previousStock = item.Quantity;
            int adjustmentQty = newQuantity - previousStock;

            item.Quantity = newQuantity;

            // 📝 LOG TRANSACTION
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ItemName = item.ItemName,
                TransactionType = "Adjustment",
                Quantity = adjustmentQty,
                PreviousStock = previousStock,
                NewStock = newQuantity,
                Reason = reason,
                PerformedBy = User.Identity?.Name ?? "System",
                TransactionDate = DateTime.Now,
                BranchId = branchId.Value
            });


            _context.SaveChanges();

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Adjustment",
                "Inventory",
                "Inventory",
                id.ToString(),
                $"Stock adjusted for '{item.ItemName}' — Stock: {previousStock} → {newQuantity} (Change: {(adjustmentQty >= 0 ? "+" : "")}{adjustmentQty}). Reason: {reason}"
            );

            TempData["Success"] = $"Stock adjusted from {previousStock} to {newQuantity} for '{item.ItemName}'.";
            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // 📋 TRANSACTION HISTORY
        // ===============================
        public IActionResult Transactions(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();
            var query = _context.InventoryTransactions
                .Where(t => t.BranchId == branchId && !t.IsArchived)
                .OrderByDescending(t => t.TransactionDate);

            var totalRecords = query.Count();

            var transactions = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new InventoryTransactionViewModel
            {
                Transactions = transactions,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_TransactionsTable", model);
            }

            return View(model);
        }

        // ===============================
        // 📦 ARCHIVE TRANSACTION
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveTransaction(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var tx = _context.InventoryTransactions
                .FirstOrDefault(t => t.Id == id && t.BranchId == branchId);

            if (tx == null) return NotFound();

            tx.IsArchived = true;
            tx.ArchivedDate = DateTime.Now;
            _context.SaveChanges();

            await _auditService.LogAsync(
                "Archive",
                "Inventory",
                "InventoryTransaction",
                id.ToString(),
                $"Archived transaction: {tx.TransactionType} of {tx.Quantity} '{tx.ItemName}'"
            );

            TempData["Success"] = "Transaction archived successfully.";
            return RedirectToAction(nameof(Transactions));
        }

        // ===============================
        // 📂 ARCHIVED TRANSACTIONS LIST
        // ===============================
        public IActionResult ArchivedTransactions(int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            var branchId = _branchService.GetCurrentBranchId();
            var query = _context.InventoryTransactions
                .Where(t => t.BranchId == branchId && t.IsArchived)
                .OrderByDescending(t => t.ArchivedDate);

            var totalRecords = query.Count();

            var transactions = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new InventoryTransactionViewModel
            {
                Transactions = transactions,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ArchivedTransactionsTable", model);
            }

            return View(model);
        }

        // ===============================
        // ♻️ UNARCHIVE TRANSACTION
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveTransaction(int id)
        {
            var branchId = _branchService.GetCurrentBranchId();
            var tx = _context.InventoryTransactions
                .FirstOrDefault(t => t.Id == id && t.BranchId == branchId);

            if (tx == null) return NotFound();

            tx.IsArchived = false;
            tx.ArchivedDate = null;
            _context.SaveChanges();

            await _auditService.LogAsync(
                "Unarchive",
                "Inventory",
                "InventoryTransaction",
                id.ToString(),
                $"Restored transaction: {tx.TransactionType} of {tx.Quantity} '{tx.ItemName}'"
            );

            TempData["Success"] = "Transaction restored successfully.";
            return RedirectToAction(nameof(ArchivedTransactions));
        }
    }
}
