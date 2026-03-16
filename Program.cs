using Latog_Final_project.Data;
using Latog_Final_project.Models;
using Latog_Final_project.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// =======================
// ✅ DATABASE
// =======================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// =======================
// ✅ IDENTITY + ROLES
// =======================
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBudgetGuardService, BudgetGuardService>();

builder.Services.AddScoped<IEmailSender<ApplicationUser>, EmailService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailService>();
builder.Services.AddScoped<InvoicePdfService>();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// =======================
// ✅ MIDDLEWARE
// =======================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // TEMPORARY: Show detailed errors to diagnose deployment issues
    // TODO: Remove this after fixing the issue
    app.UseDeveloperExceptionPage();
    //app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// =======================
// ✅ ROUTES
// =======================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// =======================
// ✅ DATABASE + SEED
// =======================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Apply migrations
        context.Database.Migrate();

        // 🟢 SEED BRANCHES
        if (!await context.Branches.AnyAsync())
        {
            context.Branches.Add(new Branch { BranchName = "Barangay 22-C", CreatedDate = DateTime.Now, IsActive = true });

            await context.SaveChangesAsync();
        }

        var defaultBranch = await context.Branches.FirstOrDefaultAsync(b => b.BranchName == "Barangay 22-C");

        int? defaultBranchId = defaultBranch?.Id;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = { "SuperAdmin", "Chairman", "Treasurer", "Staff" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // SUPER ADMIN
        var superAdmin = await userManager.FindByEmailAsync("admin@gmail.com");
        if (superAdmin == null)
        {
            var user = new ApplicationUser
            {
                UserName = "admin@gmail.com",
                Email = "admin@gmail.com",
                FullName = "System Administrator",
                EmailConfirmed = true,
                BranchId = defaultBranchId
            };

            var result = await userManager.CreateAsync(user, "Admin@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, "SuperAdmin");
        }


    // 🔵 CHAIRMAN
    string chairmanEmail = "Chairman@gmail.com";
        string chairmanPassword = "Chairman@123";

        var chairman = await userManager.FindByEmailAsync(chairmanEmail);

        if (chairman == null)
        {
            var user = new ApplicationUser
            {
                UserName = chairmanEmail,
                Email = chairmanEmail,
                FullName = "Emerson Latog",
                EmailConfirmed = true,
                BranchId = defaultBranchId
            };

            var result = await userManager.CreateAsync(user, chairmanPassword);

            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, "Chairman");
        }
        else if (chairman.FullName != "Emerson Latog" || chairman.BranchId == null)
        {
            chairman.FullName = "Emerson Latog";
            chairman.BranchId = defaultBranchId;
            await userManager.UpdateAsync(chairman);
        }


        // 🔵 TREASURER
        string treasurerEmail = "Tres@gmail.com";
        string treasurerPassword = "Tres@123";

        var treasurer = await userManager.FindByEmailAsync(treasurerEmail);

        if (treasurer == null)
        {
            var user = new ApplicationUser
            {
                UserName = treasurerEmail,
                Email = treasurerEmail,
                FullName = "Aldren Reyes",
                EmailConfirmed = true,
                BranchId = defaultBranchId
            };

            var result = await userManager.CreateAsync(user, treasurerPassword);

            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, "Treasurer");
        }
        else if (treasurer.FullName != "Aldren Reyes" || treasurer.BranchId == null)
        {
            treasurer.FullName = "Aldren Reyes";
            treasurer.BranchId = defaultBranchId;
            await userManager.UpdateAsync(treasurer);
        }


        // 🔵 STAFF
        string staffEmail = "Staff@gmail.com";
        string staffPassword = "Staff@123";

        var staff = await userManager.FindByEmailAsync(staffEmail);

        if (staff == null)
        {
            var user = new ApplicationUser
            {
                UserName = staffEmail,
                Email = staffEmail,
                FullName = "Razel Ponce",
                EmailConfirmed = true,
                BranchId = defaultBranchId
            };

            var result = await userManager.CreateAsync(user, staffPassword);

            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, "Staff");
        }
        else if (staff.FullName != "Razel Ponce" || staff.BranchId == null)
        {
            staff.FullName = "Razel Ponce";
            staff.BranchId = defaultBranchId;
            await userManager.UpdateAsync(staff);
        }


        // =======================
        // ✅ ONE-TIME SYNC: FIX MISSING NAMES
        // =======================
        var usersWithoutNames = await userManager.Users
            .Where(u => string.IsNullOrEmpty(u.FullName))
            .ToListAsync();

        foreach (var user in usersWithoutNames)
        {
            var request = await context.AccountRequests
                .FirstOrDefaultAsync(r => r.Email == user.Email);

            if (request != null)
            {
                user.FullName = request.FullName;
                await userManager.UpdateAsync(user);
            }
        }

        // =======================
        // ✅ SEED BUDGETS (FOR DEFAULT BRANCH)
        // =======================
        if (defaultBranchId.HasValue && !await context.Budgets.AnyAsync(b => b.BranchId == defaultBranchId))
        {
            context.Budgets.AddRange(
                new Budget { CategoryName = "Office Supplies", MonthlyLimit = 50000, CurrentSpending = 0, BranchId = defaultBranchId.Value },
                new Budget { CategoryName = "Maintenance", MonthlyLimit = 30000, CurrentSpending = 0, BranchId = defaultBranchId.Value },
                new Budget { CategoryName = "Events", MonthlyLimit = 100000, CurrentSpending = 0, BranchId = defaultBranchId.Value },
                new Budget { CategoryName = "Furniture & Equiptment", MonthlyLimit = 75000, CurrentSpending = 0, BranchId = defaultBranchId.Value }
            );
            await context.SaveChangesAsync();
        }

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();
