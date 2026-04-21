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

            // 1. Tạo các Hạng thành viên (Ranks)
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

            // 2. Tạo các Quyền (Roles)
            string[] roleNames = { "Admin", "Customer" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(roleName));
                }
            }

            // 3. Tạo tài khoản Admin mặc định
            // QUAN TRỌNG: Kiểm tra theo UserName là Số điện thoại
            var adminUser = await userManager.FindByNameAsync("HuynhVanGiang");
            if (adminUser == null)
            {
                var defaultRank = await context.Ranks.FirstOrDefaultAsync(r => r.RankName == "Đồng");

                var admin = new User
                {
                    // Để UserName là SĐT để đăng nhập cho nhanh và đồng nhất
                    UserName = "0984962700",
                    PhoneNumber = "0984962700",
                    FullName = "Huynh Van Giang", // Tên hiển thị của bạn
                    Email = "huynhgiangsc1000@gmail.com",
                    EmailConfirmed = true,
                    RankId = defaultRank?.RankId
                };

                var createAdmin = await userManager.CreateAsync(admin, "Giangsc1000@");
                if (createAdmin.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }
    }
}