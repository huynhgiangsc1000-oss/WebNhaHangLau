using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- 1. KẾT NỐI DATABASE ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- 2. CẤU HÌNH IDENTITY (Sử dụng User model tùy chỉnh của bạn) ---
builder.Services.AddIdentity<User, IdentityRole<int>>(options => {
    // Thiết lập mật khẩu đơn giản cho môi trường học tập
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

// --- 3. CẤU HÌNH SESSION (Dùng để lưu TableId và Giỏ hàng) ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromHours(2); // Tăng lên 2 tiếng để khách ngồi ăn thoải mái
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Manwah.Session";
});

// --- 4. CẤU HÌNH COOKIE ĐĂNG NHẬP ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Manwah.Identity";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Ghi nhớ đăng nhập 30 ngày

    // Đường dẫn đăng nhập cho khách hàng
    options.LoginPath = "/Customer/Account/Login";
    options.LogoutPath = "/Customer/Account/Logout";
    options.AccessDeniedPath = "/Customer/Account/AccessDenied";

    options.Events.OnRedirectToLogin = context =>
    {
        // Logic điều hướng thông minh khi quét mã QR
        // context.RedirectUri đã bao gồm tham số ReturnUrl (ví dụ: ?ReturnUrl=/Customer/Menu?tableId=5)
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

// --- 5. CẤU HÌNH SERVICES KHÁC ---
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor(); // Cần thiết để truy cập Session từ các class hỗ trợ

var app = builder.Build();

// --- 6. MIDDLEWARE PIPELINE (Thứ tự cực kỳ quan trọng) ---
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
app.UseStaticFiles(); // Đọc ảnh từ wwwroot/images

app.UseRouting();

// Thứ tự: Session -> Auth -> Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// --- 7. CẤU HÌNH ROUTING ---

// Route cho Area (Admin/Customer)
app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route mặc định (Trỏ thẳng vào trang chủ khách hàng)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Customer" });

app.MapRazorPages();

// --- 8. SEED DATA (Khởi tạo Admin, Table, Category mẫu) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Chạy SeedData từ class DbInitializer bạn đã có
        await DbInitializer.SeedData(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Có lỗi xảy ra khi khởi tạo dữ liệu mẫu!");
    }
}

app.Run();