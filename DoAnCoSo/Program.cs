using DoAnCoSo.Data;
using DoAnCoSo.Models;
using DoAnCoSo.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- 1. KẾT NỐI DATABASE ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => {
        sqlOptions.EnableRetryOnFailure();
        // Dòng này rất quan trọng nếu SQL của bạn cũ
        sqlOptions.CommandTimeout(60);
        // Tương thích SQL Server 2014 (không dùng OPENJSON)
        sqlOptions.UseCompatibilityLevel(120);
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- 2. CẤU HÌNH IDENTITY ---
builder.Services.AddIdentity<User, IdentityRole<int>>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- 3. CẤU HÌNH SESSION ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Manwah.Session";
});

// --- 4. CẤU HÌNH COOKIE ĐĂNG NHẬP ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Manwah.Identity";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);

    options.LoginPath = "/Customer/Account/Login";
    options.LogoutPath = "/Customer/Account/Logout";
    options.AccessDeniedPath = "/Customer/Account/AccessDenied";

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/Admin"))
        {
            context.Response.Redirect("/Admin/Account/Login?ReturnUrl=" + context.Request.Path);
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };
});

// --- 5. ĐĂNG KÝ EMAIL SERVICES 
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

// --- 6. CẤU HÌNH VNPAY & SERVICES KHÁC ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<BookingAutoProcessService>();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddRazorPages();

var app = builder.Build();

// --- 7. MIDDLEWARE PIPELINE ---
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Customer/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// --- 8. CẤU HÌNH ROUTING ---
app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Customer" });

app.MapRazorPages();

// --- 9. SEED DATA ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedData(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Có lỗi xảy ra khi khởi tạo dữ liệu mẫu!");
    }
}

app.Run();