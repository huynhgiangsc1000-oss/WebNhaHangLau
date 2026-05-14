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
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Lấy TableId từ Session hoặc Cookie
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (!string.IsNullOrEmpty(tableIdStr))
            {
                int tableId = int.Parse(tableIdStr);

                // Kiểm tra xem có Order nào của NGƯỜI KHÁC đang ngồi đây không
                var otherActiveOrder = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.TableId == tableId
                                           && o.UserId != user.Id
                                           && o.Status != "Completed"
                                           && o.Status != "Cancelled");

                if (otherActiveOrder != null)
                {
                    // Nếu thực sự là khách khác đang dùng đơn này
                    HttpContext.Session.Remove("TableId");
                    Response.Cookies.Delete("SavedTableId");
                    TempData["Error"] = "Bàn này đang được sử dụng bởi khách khác!";
                    return RedirectToAction("Index", "Table");
                }

                // --- PHẦN QUAN TRỌNG: Kiểm tra quyền sở hữu bàn ---
                // Cho phép vào menu nếu: 
                // - Có đơn hàng của mình ĐANG CHẠY 
                // - HOẶC có lịch đặt bàn đã CheckedIn 
                // - HOẶC bàn đang Occupied và mình vừa mới quét mã xong (kiểm tra qua Session)

                var hasBooking = await _context.Bookings
                    .AnyAsync(b => b.TableId == tableId && b.UserId == user.Id && b.Status == "CheckedIn");

                var hasMyOrder = await _context.Orders
                    .AnyAsync(o => o.TableId == tableId && o.UserId == user.Id && o.Status != "Completed" && o.Status != "Cancelled");

                var table = await _context.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.TableId == tableId);

                // SỬA LẠI ĐIỀU KIỆN CHẶN: 
                // Chỉ đuổi ra nếu bàn Occupied mà KHÔNG PHẢI do mình (không đơn, không booking, không phải người vừa quét mã)
                // Lưu ý: Nếu tableIdStr lấy từ Session tức là họ vừa quét mã thành công ở TableController
                if (table?.Status == "Occupied" && !hasBooking && !hasMyOrder && string.IsNullOrEmpty(HttpContext.Session.GetString("TableId")))
                {
                    HttpContext.Session.Remove("TableId");
                    return RedirectToAction("Index", "Table");
                }

                ViewBag.TableId = tableIdStr;
                ViewBag.TableName = table?.TableName;
            }
            else
            {
                // Tự động khôi phục bàn từ Booking CheckedIn (giữ nguyên logic của bạn)
                var myCheckedInBooking = await _context.Bookings
                    .Where(b => b.UserId == user.Id && b.Status == "CheckedIn")
                    .OrderByDescending(b => b.BookingDate)
                    .FirstOrDefaultAsync();

                if (myCheckedInBooking != null && myCheckedInBooking.TableId.HasValue)
                {
                    tableIdStr = myCheckedInBooking.TableId.Value.ToString();
                    HttpContext.Session.SetString("TableId", tableIdStr);
                    ViewBag.TableId = tableIdStr;
                    var table = await _context.Tables.FindAsync(myCheckedInBooking.TableId);
                    ViewBag.TableName = table?.TableName;
                }
            }

            ViewBag.HasTable = !string.IsNullOrEmpty(tableIdStr);

            // --- PHẦN LOAD SẢN PHẨM GIỮ NGUYÊN NHƯ CŨ ---
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .Where(c => c.ParentId == null)
                .AsNoTracking()
                .ToListAsync();

            IQueryable<Product> productQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .AsNoTracking();

            if (categoryId.HasValue)
            {
                var allCats = await _context.Categories.AsNoTracking().ToListAsync();
                var idsList = new List<int>();
                GetCategoryIdsRecursive(categoryId.Value, allCats, idsList);
                productQuery = productQuery.Where(p => idsList.Contains(p.CategoryId));
                ViewBag.CurrentCategory = categoryId;
            }

            var products = await productQuery.ToListAsync();
            ViewBag.Categories = categories;
            return View(products);
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