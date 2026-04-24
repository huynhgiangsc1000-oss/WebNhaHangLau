using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Identity;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public MenuController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            // 1. Quản lý thông tin bàn từ Session/Cookie
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            ViewBag.HasTable = !string.IsNullOrEmpty(tableIdStr);
            ViewBag.TableId = tableIdStr;

            // 2. Lấy danh sách danh mục (Gốc) để hiển thị Sidebar
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .Where(c => c.ParentId == null)
                .AsNoTracking()
                .ToListAsync();

            List<Product> products;

            if (categoryId.HasValue)
            {
                var allCategories = await _context.Categories.AsNoTracking().ToListAsync();
                var idsList = new List<int>();
                GetCategoryIdsRecursive(categoryId.Value, allCategories, idsList);

                // FIX CHỐT: Đảm bảo đây là một danh sách số nguyên sạch
                var finalIds = idsList.Distinct().ToList();

                // Thay vì dùng IQueryable, ta lấy dữ liệu thô về rồi lọc 
                // (Vì bảng Product thường không quá lớn, cách này cực kỳ an toàn)
                var allAvailableProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();

                products = allAvailableProducts
                    .Where(p => finalIds.Contains(p.CategoryId))
                    .ToList();

                ViewBag.CurrentCategory = categoryId;
            }
            else
            {
                // Nếu không lọc: Lấy tất cả sản phẩm đang kinh doanh
                products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();
            }

            // Chuẩn hóa ImagePath trước khi gửi sang View để bạn dùng được dấu ~
            // Nếu ImagePath trong DB là "hinh1.jpg", nó sẽ được giữ nguyên để View dùng ~/images/hinh1.jpg
            // Nếu ImagePath null, ta gán một tên file mặc định
            foreach (var p in products)
            {
                if (string.IsNullOrEmpty(p.ImagePath))
                {
                    p.ImagePath = "no-image.png";
                }
            }

            ViewBag.Categories = categories;
            return View(products);
        }

        /// <summary>
        /// Hàm hỗ trợ đệ quy để lấy ID cha và toàn bộ ID danh mục con
        /// </summary>
        private void GetCategoryIdsRecursive(int parentId, List<Category> allCats, List<int> result)
        {
            if (!result.Contains(parentId))
            {
                result.Add(parentId);
            }

            var childIds = allCats
                .Where(c => c.ParentId == parentId)
                .Select(c => c.CategoryId)
                .ToList();

            foreach (var id in childIds)
            {
                GetCategoryIdsRecursive(id, allCats, result);
            }
        }

        // Thêm vào MenuController.cs
        public async Task<IActionResult> GetProductDetail(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();

            // Đảm bảo ImagePath không null để tránh lỗi ở View
            if (string.IsNullOrEmpty(product.ImagePath)) product.ImagePath = "no-image.png";

            return PartialView("_ProductDetailPartial", product);
        }

    }
}