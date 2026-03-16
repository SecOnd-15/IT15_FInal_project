using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Latog_Final_project.Data;
using Latog_Final_project.Models;

namespace Latog_Final_project.Services
{
    public interface IBranchService
    {
        int? GetCurrentBranchId();
        Task<Branch?> GetCurrentBranchAsync();
    }

    public class BranchService : IBranchService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;

        public BranchService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public int? GetCurrentBranchId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true) return null;

            var branchIdClaim = user.FindFirst("BranchId")?.Value;
            if (int.TryParse(branchIdClaim, out int branchId))
            {
                return branchId;
            }

            
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var dbUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                return dbUser?.BranchId;
            }

            return null;
        }

        public async Task<Branch?> GetCurrentBranchAsync()
        {
            var branchId = GetCurrentBranchId();
            if (branchId == null) return null;

            return await _context.Branches.FindAsync(branchId);
        }
    }
}
