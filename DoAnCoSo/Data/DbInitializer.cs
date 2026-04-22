using Microsoft.AspNetCore.Identity;
using DoAnCoSo.Models;
using DoAnCoSo.Data;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Data
{
    public static class DbInitializer
    {
        public static async Task SeedData(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Khởi tạo các Hạng thành viên (Ranks)
            if (!context.Ranks.Any())
            {
                context.Ranks.AddRange(
                    new Rank { RankName = "Đồng", DiscountPercent = 0, RequiredPoints = 0 },
                    new Rank { RankName = "Bạc", DiscountPercent = 5, RequiredPoints = 100 },
                    new Rank { RankName = "Vàng", DiscountPercent = 10, RequiredPoints = 500 },
                    new Rank { RankName = "Kim Cương", DiscountPercent = 15, RequiredPoints = 1000 }
                );
                await context.SaveChangesAsync();
            }

            // 2. Khởi tạo các Quyền (Roles) - Đã thêm "Staff"
            string[] roleNames = { "Admin", "Staff", "Customer" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(roleName));
                }
            }

            var defaultRank = await context.Ranks.FirstOrDefaultAsync(r => r.RankName == "Đồng");

            // 3. Tạo tài khoản Admin (Sử dụng SĐT làm UserName)
            var adminPhone = "0984962700";
            var adminUser = await userManager.FindByNameAsync(adminPhone);
            if (adminUser == null)
            {
                var admin = new User
                {
                    UserName = adminPhone,
                    PhoneNumber = adminPhone,
                    FullName = "Huynh Van Giang",
                    Email = "huynhgiangsc1000@gmail.com",
                    EmailConfirmed = true,
                    RankId = defaultRank?.RankId,
                    CreatedAt = DateTime.Now
                };

                var result = await userManager.CreateAsync(admin, "Giangsc1000@");
                if (result.Succeeded) await userManager.AddToRoleAsync(admin, "Admin");
            }

            // 4. Tạo tài khoản Nhân viên mẫu (Staff)
            var staffPhone = "0123456789";
            var staffUser = await userManager.FindByNameAsync(staffPhone);
            if (staffUser == null)
            {
                var staff = new User
                {
                    UserName = staffPhone,
                    PhoneNumber = staffPhone,
                    FullName = "Nhân Viên Phục Vụ 01",
                    Email = "staff01@manwah.com",
                    EmailConfirmed = true,
                    RankId = defaultRank?.RankId,
                    CreatedAt = DateTime.Now
                };

                var result = await userManager.CreateAsync(staff, "Staff123@");
                if (result.Succeeded) await userManager.AddToRoleAsync(staff, "Staff");
            }
        }
    }
}