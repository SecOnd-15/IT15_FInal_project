using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Latog_Final_project.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Latog_Final_project.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<ResourceRequest> ResourceRequests { get; set; }

        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AccountRequest> AccountRequests { get; set; }
        public DbSet<Budget> Budgets { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ✅ MULTI-TENANCY: Global Query Filters
            // These filters automatically exclude data from other branches for non-admin users.
            // Note: In a real app, you'd get the CurrentBranchId from a service.
            // For now, we define the schema; filtering will be handled in Controllers or via a BaseController.

            // ✅ FIX DECIMAL WARNINGS
            builder.Entity<ResourceRequest>()
                .Property(r => r.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<Invoice>()
                .Property(i => i.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<Invoice>()
                .Property(i => i.AmountPaid)
                .HasPrecision(18, 2);

            builder.Entity<Budget>()
                .Property(b => b.MonthlyLimit)
                .HasPrecision(10, 2);

            builder.Entity<Budget>()
                .Property(b => b.CurrentSpending)
                .HasPrecision(10, 2);


            builder.Entity<ResourceRequest>()
                .Property(r => r.EstimatedAmount)
                .HasPrecision(10, 2);
        }

        // =============================================
        // ✅ AUTO-TRACKING: SaveChangesAsync Override
        // Automatically logs Added, Modified, Deleted
        // entities (excluding AuditLog to avoid recursion)
        // =============================================
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = new System.Collections.Generic.List<AuditLog>();

            foreach (var entry in ChangeTracker.Entries())
            {
                
                var entityType = entry.Entity.GetType();
                if (entry.Entity is AuditLog || 
                    entityType.Name.StartsWith("IdentityUserRole") || 
                    entityType.Name.StartsWith("IdentityRoleClaim") || 
                    entityType.Name.StartsWith("IdentityUserClaim"))
                    continue;

                if (entry.State == EntityState.Added ||
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted)
                {
                    var entityName = entry.Entity.GetType().Name;
                    var entityId = GetPrimaryKeyValue(entry);
                    var action = entry.State.ToString();
                    var details = BuildChangeDetails(entry);

                    var currentUserId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
                    var currentBranchIdStr = _httpContextAccessor?.HttpContext?.User?.FindFirst("BranchId")?.Value;
                    int? currentBranchId = int.TryParse(currentBranchIdStr, out var bId) ? bId : null;

                    auditEntries.Add(new AuditLog
                    {
                        UserId = currentUserId,
                        BranchId = currentBranchId,
                        Action = action,
                        Module = MapEntityToModule(entityName),
                        EntityName = entityName,
                        EntityId = entityId,
                        Timestamp = DateTime.Now,
                        Details = details
                    });
                }
            }

           
            var result = await base.SaveChangesAsync(cancellationToken);

        
            if (auditEntries.Any())
            {
                AuditLogs.AddRange(auditEntries);
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

      
        private string GetPrimaryKeyValue(EntityEntry entry)
        {
            var keyProperty = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey());

            return keyProperty?.CurrentValue?.ToString() ?? "N/A";
        }

        private string BuildChangeDetails(EntityEntry entry)
        {
           
            var ignoredProperties = new[]
            {
                "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                "NormalizedEmail", "NormalizedUserName", "LockoutEnabled",
                "AccessFailedCount", "EmailConfirmed", "PhoneNumberConfirmed",
                "TwoFactorEnabled", "LockoutEnd"
            };

            if (entry.State == EntityState.Added)
            {
                var props = entry.Properties
                    .Where(p => p.CurrentValue != null && !ignoredProperties.Contains(p.Metadata.Name))
                    .Select(p => $"{p.Metadata.Name}={p.CurrentValue}");
                
                return string.Join(", ", props);
            }

            if (entry.State == EntityState.Deleted)
            {
                return $"Deleted {entry.Entity.GetType().Name}";
            }

            if (entry.State == EntityState.Modified)
            {
                var changes = entry.Properties
                    .Where(p => p.IsModified && !ignoredProperties.Contains(p.Metadata.Name))
                    .Select(p => $"{p.Metadata.Name}: '{p.OriginalValue}' → '{p.CurrentValue}'")
                    .ToList();

                if (!changes.Any()) return "Modified (Metadata only)";

                return string.Join(", ", changes);
            }

            return string.Empty;
        }

        private string MapEntityToModule(string entityName)
        {
            return entityName switch
            {
                "ResourceRequest" => "Purchase Requisition",
                "Inventory" => "Inventory Management",
                "InventoryTransaction" => "Inventory Management",
                "Invoice" => "Invoice & Payment",
                "AccountRequest" => "Account Management",
                "ApplicationUser" => "User Account",
                "Branch" => "Branch Management",
                "Budget" => "Budget Management",
                _ => "General"

            };
        }
    }
}
