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
            // 1. Lấy thông tin bàn (Giữ nguyên)
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            ViewBag.HasTable = !string.IsNullOrEmpty(tableIdStr);
            ViewBag.TableId = tableIdStr;

            // 2. Lấy danh sách danh mục để hiển thị sidebar
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .Where(c => c.ParentId == null)
                .ToListAsync();

            List<Product> products;

            if (categoryId.HasValue)
            {
                // TÌM TẤT CẢ ID LIÊN QUAN (BAO GỒM CHÍNH NÓ VÀ CÁC CON)
                // Bước A: Lấy toàn bộ bảng Category vào bộ nhớ để xử lý đệ quy cho nhanh và an toàn
                var allCategories = await _context.Categories.AsNoTracking().ToListAsync();

                var idsToFilter = new List<int>();
                GetCategoryIdsRecursive(categoryId.Value, allCategories, idsToFilter);

                // Bước B: Truy vấn sản phẩm dựa trên danh sách ID đã tìm được
                products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => idsToFilter.Contains(p.CategoryId) && p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.CurrentCategory = categoryId;
            }
            else
            {
                products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();
            }

            ViewBag.Categories = categories;
            return View(products);
        }

        // Hàm hỗ trợ đệ quy để lấy ID cha và toàn bộ ID con
        private void GetCategoryIdsRecursive(int parentId, List<Category> allCats, List<int> result)
        {
            result.Add(parentId); // Thêm chính nó
            var childIds = allCats.Where(c => c.ParentId == parentId).Select(c => c.CategoryId);
            foreach (var id in childIds)
            {
                GetCategoryIdsRecursive(id, allCats, result); // Đệ quy tìm con của con
            }
        }
    }
}