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

        public async Task<IActionResult> Index(int? categoryId, string searchTerm, string priceRange, string sortOrder)
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. Lấy TableId từ Session hoặc Cookie
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (!string.IsNullOrEmpty(tableIdStr))
            {
                int tableId = int.Parse(tableIdStr);
                var table = await _context.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.TableId == tableId);

                // 2. KIỂM TRA XUNG ĐỘT (Chỉ chạy khi CÓ đăng nhập)
                if (user != null)
                {
                    var otherActiveOrder = await _context.Orders
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.TableId == tableId
                                               && o.UserId != user.Id
                                               && o.Status != "Completed"
                                               && o.Status != "Cancelled");

                    if (otherActiveOrder != null)
                    {
                        ClearTableSession();
                        TempData["Error"] = "Bàn này đang được sử dụng bởi khách khác!";
                        return RedirectToAction("Index", "Table");
                    }
                }

                // 3. XÁC MINH QUYỀN TRUY CẬP
                bool hasMyOrder = user != null && await _context.Orders
                    .AnyAsync(o => o.TableId == tableId && o.UserId == user.Id && o.Status != "Completed");

                bool hasCheckedInBooking = await _context.Bookings
                    .AnyAsync(b => b.TableId == tableId && b.Status == "CheckedIn");

                bool isFreshAccess = !string.IsNullOrEmpty(HttpContext.Session.GetString("TableId"));

                // 4. LOGIC CHẶN
                if (table?.Status == "Occupied" && !hasMyOrder && !hasCheckedInBooking && !isFreshAccess)
                {
                    ClearTableSession();
                    TempData["Error"] = "Bàn hiện đang có khách. Vui lòng liên hệ nhân viên!";
                    return RedirectToAction("Index", "Table");
                }

                ViewBag.TableId = tableIdStr;
                ViewBag.TableName = table?.TableName;
            }
            else if (user != null) // 5. TỰ ĐỘNG KHÔI PHỤC (Chỉ cho thành viên)
            {
                var myCheckedInBooking = await _context.Bookings
                    .Where(b => b.UserId == user.Id && b.Status == "CheckedIn")
                    .OrderByDescending(b => b.BookingDate)
                    .FirstOrDefaultAsync();

                if (myCheckedInBooking != null && myCheckedInBooking.TableId.HasValue)
                {
                    tableIdStr = myCheckedInBooking.TableId.Value.ToString();
                    HttpContext.Session.SetString("TableId", tableIdStr);
                    var table = await _context.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.TableId == myCheckedInBooking.TableId);
                    ViewBag.TableId = tableIdStr;
                    ViewBag.TableName = table?.TableName;
                }
            }

            // ĐẢM BẢO LUÔN CÓ GIÁ TRỊ TRUE/FALSE RÕ RÀNG
            ViewBag.HasTable = !string.IsNullOrEmpty(tableIdStr);

            // --- LOGIC HIỂN THỊ DANH MỤC ---
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .Where(c => c.ParentId == null)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.Categories = categories;

            // --- LOGIC HIỂN THỊ VÀ LỌC SẢN PHẨM ---
            // Tải toàn bộ sản phẩm đang kinh doanh lên bộ nhớ để lọc linh hoạt
            var queryProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .AsNoTracking()
                .ToListAsync();

            // A. Lọc theo Danh mục (Đệ quy nếu chọn danh mục cha)
            if (categoryId.HasValue)
            {
                var allCats = await _context.Categories.AsNoTracking().ToListAsync();
                var idsList = new List<int>();

                GetCategoryIdsRecursive(categoryId.Value, allCats, idsList);
                var categoryIdsParam = idsList.Distinct().ToArray();

                queryProducts = queryProducts.Where(p => categoryIdsParam.Contains(p.CategoryId)).ToList();
                ViewBag.CurrentCategory = categoryId;
            }
            else
            {
                ViewBag.CurrentCategory = null;
            }

            // B. Lọc theo Từ khóa tìm kiếm (Tên món ăn)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string term = searchTerm.Trim().ToLower();
                queryProducts = queryProducts.Where(p => p.ProductName.ToLower().Contains(term)).ToList();
            }

            // C. Lọc theo Khoảng giá
            if (!string.IsNullOrEmpty(priceRange))
            {
                switch (priceRange)
                {
                    case "under100":
                        queryProducts = queryProducts.Where(p => p.Price < 100000).ToList();
                        break;
                    case "100to200":
                        queryProducts = queryProducts.Where(p => p.Price >= 100000 && p.Price <= 200000).ToList();
                        break;
                    case "over200":
                        queryProducts = queryProducts.Where(p => p.Price > 200000).ToList();
                        break;
                }
            }

            // D. Sắp xếp kết quả (Sort)
            if (!string.IsNullOrEmpty(sortOrder))
            {
                switch (sortOrder)
                {
                    case "price_asc":
                        queryProducts = queryProducts.OrderBy(p => p.Price).ToList();
                        break;
                    case "price_desc":
                        queryProducts = queryProducts.OrderByDescending(p => p.Price).ToList();
                        break;
                }
            }

            // Gửi ngược trạng thái lọc về giao diện để giữ trạng thái cho các ô Input/Select
            ViewBag.SearchTerm = searchTerm;
            ViewBag.PriceRange = priceRange;
            ViewBag.SortOrder = sortOrder;

            return View(queryProducts);
        }
        private void ClearTableSession()
        {
            HttpContext.Session.Remove("TableId");
            Response.Cookies.Delete("SavedTableId");
        }
        private void GetCategoryIdsRecursive(int parentId, List<Category> allCats, List<int> result)
        {
            if (!result.Contains(parentId)) result.Add(parentId);
            var childIds = allCats.Where(c => c.ParentId == parentId).Select(c => c.CategoryId).ToList();
            foreach (var id in childIds) GetCategoryIdsRecursive(id, allCats, result);
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