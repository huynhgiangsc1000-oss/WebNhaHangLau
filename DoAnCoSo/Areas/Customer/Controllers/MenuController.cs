using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            // 1. Lấy danh sách danh mục để hiển thị sidebar (Giữ nguyên)
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();

            // 2. Query sản phẩm kèm theo thông tin Category để tránh lỗi null ở View
            var productsQuery = _context.Products.Include(p => p.Category).AsQueryable();

            // 3. Logic lọc cải tiến
            if (categoryId.HasValue)
            {
                // Bước A: Tìm tất cả các ID của danh mục con thuộc về categoryId này
                var subCategoryIds = await _context.Categories
                    .Where(c => c.ParentId == categoryId)
                    .Select(c => c.CategoryId)
                    .ToListAsync();

                // Bước B: Thêm chính ID của category đang chọn vào danh sách lọc
                subCategoryIds.Add(categoryId.Value);

                // Bước C: Lọc sản phẩm nào nằm trong danh sách ID trên
                productsQuery = productsQuery.Where(p => subCategoryIds.Contains(p.CategoryId));

                ViewBag.CurrentCategory = categoryId;
            }

            // 4. Thực thi lấy dữ liệu
            var products = await productsQuery.Where(p => p.IsAvailable).ToListAsync();

            ViewBag.Categories = categories;
            return View(products);
        }
    }
}