using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Latog_Final_project.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public SuperAdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _auditService = auditService;
        }

        // ============================
        // SUPER ADMIN DASHBOARD
        // ============================
        public async Task<IActionResult> Dashboard(int? year, int? month)
        {
            // Default to current year/month if not specified for some metrics if needed
            // But usually we want to see "Global" unless filtered.
            
            var auditLogQuery = _context.AuditLogs.AsQueryable();
            var resourceRequestQuery = _context.ResourceRequests.AsQueryable();
            var userRoleQuery = _context.UserRoles.AsQueryable();

            if (year.HasValue)
            {
                auditLogQuery = auditLogQuery.Where(l => l.Timestamp.Year == year.Value);
                resourceRequestQuery = resourceRequestQuery.Where(r => r.RequestDate.Year == year.Value);
                // UserRole distribution might not be time-dependent in the same way, 
                // but let's assume we want the current snapshot unless specified otherwise.
            }

            if (month.HasValue)
            {
                auditLogQuery = auditLogQuery.Where(l => l.Timestamp.Month == month.Value);
                resourceRequestQuery = resourceRequestQuery.Where(r => r.RequestDate.Month == month.Value);
            }

            var model = new DashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalTransactions = await auditLogQuery.CountAsync(),
                TotalBranches = await _context.Branches.CountAsync(),
                RecentLogs = await auditLogQuery
                    .OrderByDescending(l => l.Timestamp)
                    .Take(10)
                    .ToListAsync(),

                FilterYear = year,
                FilterMonth = month,

                // 💎 GLOBAL BI DATA CALCULATIONS
                
                // 1. Activity by Branch (Bar Chart)
                BranchActivity = await resourceRequestQuery
                    .Include(r => r.Branch)
                    .GroupBy(r => r.Branch.BranchName)
                    .Select(g => new ChartDataPoint { 
                        Label = g.Key ?? "Unknown", 
                        Value = g.Count() 
                    })
                    .OrderByDescending(d => d.Value)
                    .ToListAsync(),

                // 2. System Activity Trend (Area Chart)
                GlobalSystemActivity = (await auditLogQuery
                    .GroupBy(l => l.Timestamp.Date)
                    .Select(g => new { 
                        Date = g.Key, 
                        Count = g.Count() 
                    })
                    .OrderBy(d => d.Date)
                    .ToListAsync())
                    .Select(d => new ChartDataPoint {
                        Label = d.Date.ToString("MMM dd"),
                        Value = d.Count
                    })
                    .ToList(),

                // 3. User Role Distribution (Doughnut Chart)
                UserRoleDistribution = await userRoleQuery
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { r.Name })
                    .GroupBy(x => x.Name)
                    .Select(g => new ChartDataPoint { 
                        Label = g.Key, 
                        Value = g.Count() 
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        // ============================
        // USER LIST + SEARCH + FILTER
        // ============================
        public async Task<IActionResult> Index(string search, string role, int? branchId, string mode)
        {
            var branches = await _context.Branches.OrderBy(b => b.BranchName).ToListAsync();
            ViewBag.Branches = branches;
            ViewBag.SelectedBranchId = branchId;
            ViewBag.Mode = mode; // "admin" for Admin Account, null for regular Users

            var usersQuery = _userManager.Users
                .Include(u => u.Branch)
                .AsQueryable();

            // For Admin Account mode, fetch ALL users (no branch filter)
            // For regular Users mode, filter by branch
            if (mode != "admin" && branchId.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.BranchId == branchId);
                var selectedBranch = branches.FirstOrDefault(b => b.Id == branchId);
                ViewBag.SelectedBranchName = selectedBranch?.BranchName ?? "Global";
            }
            else if (mode == "admin")
            {
                ViewBag.SelectedBranchName = "Admin Users";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                usersQuery = usersQuery.Where(u => u.Email != null && u.Email.Contains(search));
            }

            var users = usersQuery.ToList();
            var userRoles = new Dictionary<string, string>();
            var filteredUsers = new List<ApplicationUser>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "None";

                // When viewing Admin Account (mode=admin), only show SuperAdmins
                if (mode == "admin" && roleName != "SuperAdmin")
                    continue;

                // When viewing regular Users, exclude SuperAdmins
                if (mode != "admin" && roleName == "SuperAdmin")
                    continue;

                if (!string.IsNullOrEmpty(role) && roleName != role)
                    continue;

                userRoles[user.Id] = roleName;
                filteredUsers.Add(user);
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.Search = search;
            ViewBag.Role = role;

            return View(filteredUsers);
        }

        // ============================
        // MANAGE ROLES (GET)
        // ============================
        public async Task<IActionResult> ManageRoles(string userId, string mode)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            ViewBag.User = user;
            ViewBag.Roles = _roleManager.Roles.Where(r => r.Name != "SuperAdmin").ToList();
            ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
            ViewBag.Branches = await _context.Branches.OrderBy(b => b.BranchName).ToListAsync();
            ViewBag.Mode = mode;

            return View();
        }


        // ============================
        // MANAGE ROLES (POST) + AUDIT
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(string userId, string role, int? branchId, string mode)

        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var previousRole = currentRoles.FirstOrDefault() ?? "None";

            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            // ✅ UPDATE BRANCH
            user.BranchId = branchId;

            await _context.SaveChangesAsync();


            // ✅ NEW CENTRALIZED AUDIT LOG
            await _auditService.LogAsync(
                "Role Assignment",
                "User Management",
                "ApplicationUser",
                user.Id,
                $"Role changed from '{previousRole}' to '{role}' for user '{user.Email}'"
            );

            return RedirectToAction(nameof(Index), new { branchId = branchId ?? 1, mode = mode });
        }

        // ============================
        // DELETE USER
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId, string mode)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            if (User.Identity?.Name == user.Email)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index), new { branchId = user.BranchId ?? 1, mode = mode });
            }

            var email = user.Email;
            var branchId = user.BranchId;
            await _userManager.DeleteAsync(user);

            // ✅ AUDIT LOG
            await _auditService.LogAsync(
                "Delete",
                "User Management",
                "ApplicationUser",
                userId,
                $"User '{email}' was deleted by Super Admin"
            );

            return RedirectToAction(nameof(Index), new { branchId = branchId ?? 1, mode = mode });
        }

        // ============================
        // RESET USER PASSWORD (GET)
        // ============================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string userId, string mode)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new ResetUserPasswordViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Mode = mode
            };

            return View(model);
        }

        // ============================
        // RESET USER PASSWORD (POST)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            // Using direct removal and addition of password as it's an admin reset
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (removeResult.Succeeded || (removeResult.Errors.Any(e => e.Code == "UserHasNoPassword")))
            {
                var addResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (addResult.Succeeded)
                {
                    await _auditService.LogAsync(
                        "Password Reset",
                        "User Management",
                        "ApplicationUser",
                        user.Id,
                        $"Password was reset for user '{user.Email}' by Super Admin"
                    );

                    TempData["Success"] = $"Password for '{user.Email}' has been reset successfully.";
                    return RedirectToAction(nameof(Index), new { branchId = user.BranchId ?? 1, mode = model.Mode });
                }

                foreach (var error in addResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            else
            {
                foreach (var error in removeResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        // ============================
        // ADD NEW SUPER ADMIN
        // ============================
        [HttpGet]
        public IActionResult AddAdmin()
        {
            return View(new AddAdminViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(AddAdminViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "A user with this email already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = model.FullName,
                BranchId = 1 // Administrators grouped under Branch 1 (Barangay 22-C)
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "SuperAdmin");

                await _auditService.LogAsync("Create", "Admin Management", "ApplicationUser", user.Id, $"New Super Admin account created: {user.Email}");

                TempData["Success"] = "Administrator account created successfully.";
                return RedirectToAction(nameof(Index), new { branchId = 1, mode = "admin" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ============================
        // VIEW SYSTEM AUDIT LOG
        // ============================
        public async Task<IActionResult> AuditLog(string? roleFilter, string? actionFilter, int page = 1)
        {
            int pageSize = 10;
            if (page < 1) page = 1;

            // ✅ START WITH ALL RECORDS
            var query = _context.AuditLogs.AsQueryable();

            // ✅ ROLE FILTER
            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                // Get user IDs that have this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleFilter);
                var userIds = usersInRole.Select(u => u.Id).ToList();
                
                // If filtering by role, we only want logs for those users.
                // If it's a specific action, it will be further filtered below.
                query = query.Where(l => userIds.Contains(l.UserId));
            }

            // ✅ ACTION FILTER
            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(l => l.Action == actionFilter);
            }

            // ✅ GET TOTAL RECORD COUNT (Before pagination)
            var totalRecords = await query.CountAsync();

            // ✅ LOAD PAGINATED RESULTS
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ POPULATE DROPDOWN OPTIONS
            var roles = _roleManager.Roles
                .Select(r => r.Name!)
                .OrderBy(r => r)
                .ToList();

            var actions = await _context.AuditLogs
                .Select(l => l.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var users = await _userManager.Users
                .Select(u => new UserOption { Id = u.Id, Email = u.Email ?? string.Empty })
                .ToListAsync();

            var model = new AuditLogFilterViewModel
            {
                Role = roleFilter,
                Action = actionFilter,
                Logs = logs,
                Roles = roles,
                Actions = actions,
                Users = users,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_AuditLogTable", model);
            }

            return View(model);
        }

        // ============================
        // 🏗️ BRANCH MANAGEMENT
        // ============================
        public async Task<IActionResult> Branches()
        {
            var branches = await _context.Branches
                .OrderBy(b => b.BranchName)
                .ToListAsync();

            // Exclude SuperAdmins from barangay staff lists
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            var superAdminIds = superAdmins.Select(u => u.Id).ToHashSet();
            var users = await _userManager.Users
                .Where(u => u.BranchId != null && !superAdminIds.Contains(u.Id))
                .ToListAsync();
            ViewBag.BranchUsers = users.GroupBy(u => u.BranchId!.Value).ToDictionary(g => g.Key, g => g.ToList());

            return View(branches);
        }

        [HttpGet]
        public IActionResult AddBranch()
        {
            return View(new CreateBarangayViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBranch(CreateBarangayViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // 1. Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "A user with this email already exists.");
                return View(model);
            }

            // 2. Create the Branch
            var branch = new Branch
            {
                BranchName = model.BranchName,
                Location = model.Location,
                ContactNumber = model.ContactNumber,
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            // 3. Create the Chairman User
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                BranchId = branch.Id,
                FullName = "Chairman of " + branch.BranchName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Chairman");

                await _auditService.LogAsync("Create", "Branch Management", "Branch", branch.Id.ToString(), $"New Barangay added: '{branch.BranchName}'");
                await _auditService.LogAsync("Create", "User Management", "ApplicationUser", user.Id, $"New Chairman account created for '{branch.BranchName}': {user.Email}");

                TempData["Success"] = $"Barangay '{branch.BranchName}' and Chairman account created successfully.";
                return RedirectToAction(nameof(Branches));
            }

            // Rollback branch if user creation failed
            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBranch(Branch branch)
        {
            if (!ModelState.IsValid) return View(branch);

            var existing = await _context.Branches.FindAsync(branch.Id);
            if (existing == null) return NotFound();

            existing.BranchName = branch.BranchName;
            existing.Location = branch.Location;
            existing.ContactNumber = branch.ContactNumber;

            _context.Update(existing);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "Branch Management", "Branch", branch.Id.ToString(), $"Barangay '{branch.BranchName}' details updated.");

            TempData["Success"] = $"Barangay '{branch.BranchName}' updated.";
            return RedirectToAction(nameof(Branches));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBranchStatus(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsActive = !branch.IsActive;
            _context.Update(branch);
            await _context.SaveChangesAsync();

            var status = branch.IsActive ? "activated" : "deactivated";
            await _auditService.LogAsync("Update", "Branch Management", "Branch", id.ToString(), $"Barangay '{branch.BranchName}' was {status}.");

            return RedirectToAction(nameof(Branches));
        }
    }
}
