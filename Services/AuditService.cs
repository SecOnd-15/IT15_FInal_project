using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Latog_Final_project.Data;
using Latog_Final_project.Models;

namespace Latog_Final_project.Services
{
    // ================================
    // INTERFACE
    // ================================
    public interface IAuditService
    {
        Task LogAsync(string action, string module, string entityName, string entityId, string details);
    }

    // ================================
    // IMPLEMENTATION
    // ================================
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string module, string entityName, string entityId, string details)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
                      ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? "System";

            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                Module = module,
                EntityName = entityName,
                EntityId = entityId,
                Timestamp = DateTime.Now,
                Details = details
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
