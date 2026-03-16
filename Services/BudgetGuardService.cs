using System.Threading.Tasks;
using Latog_Final_project.Data;
using Microsoft.EntityFrameworkCore;

namespace Latog_Final_project.Services
{
    public interface IBudgetGuardService
    {
        Task<BudgetValidationResult> CheckBudgetAsync(string? category, decimal amount, int branchId);
    }

    public class BudgetValidationResult
    {
        public bool IsWithinBudget { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal RemainingBudget { get; set; }
        public decimal CurrentSpending { get; set; }
        public decimal MonthlyLimit { get; set; }
    }

    public class BudgetGuardService : IBudgetGuardService
    {
        private readonly ApplicationDbContext _context;

        public BudgetGuardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BudgetValidationResult> CheckBudgetAsync(string? category, decimal amount, int branchId)
        {
            if (string.IsNullOrEmpty(category))
            {
                return new BudgetValidationResult 
                { 
                    IsWithinBudget = false, 
                    Message = "No category specified." 
                };
            }

            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryName == category && b.BranchId == branchId);

            if (budget == null)
            {
                return new BudgetValidationResult 
                { 
                    IsWithinBudget = false, 
                    Message = $"Budget category '{category}' not found for this branch." 
                };
            }

            decimal available = budget.MonthlyLimit - budget.CurrentSpending;
            bool isWithin = amount <= available;

            return new BudgetValidationResult
            {
                IsWithinBudget = isWithin,
                RemainingBudget = available,
                CurrentSpending = budget.CurrentSpending,
                MonthlyLimit = budget.MonthlyLimit,
                Message = isWithin 
                    ? "Requested amount is within the budget limit." 
                    : $"Budget limit exceeded for '{category}'. Available: ₱{available:N2}, Requested: ₱{amount:N2}."
            };
        }
    }
}
