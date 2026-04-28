using BookShoppingCartMvcUI;
using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Repositories;
using BookShoppingCartMvcUI.Services;
using BookShoppingCartMvcUI.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add logging (already included by default, but ensure it's there)
builder.Services.AddLogging();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("conn")));

// Add Identity with Role support
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Email Sender
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Register File Service
builder.Services.AddScoped<IFileService, FileService>();

// Register all repositories
builder.Services.AddScoped<IHomeRepository, HomeRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IGenreRepository, GenreRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IUserOrderRepository, UserOrderRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

// Add authorization policies
builder.Services.AddAuthorization(options =>
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin")));

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Seed roles, admin user, and order statuses
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Seed Roles
    string[] roles = { "Admin", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Seed Admin User
    var adminEmail = "admin@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin@123");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // ⭐ SEED ORDER STATUSES - THIS IS CRITICAL FOR CHECKOUT ⭐
    if (!dbContext.orderStatuses.Any())
    {
        await dbContext.orderStatuses.AddRangeAsync(
            new OrderStatus { StatusName = "Pending" },
            new OrderStatus { StatusName = "Processing" },
            new OrderStatus { StatusName = "Shipped" },
            new OrderStatus { StatusName = "Delivered" },
            new OrderStatus { StatusName = "Cancelled" }
        );
        await dbContext.SaveChangesAsync();
        Console.WriteLine("Order statuses seeded successfully!");
    }
}

app.Run();