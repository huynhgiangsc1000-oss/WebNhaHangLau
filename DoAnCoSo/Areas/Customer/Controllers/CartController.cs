using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CartController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Hiển thị giỏ hàng
        public async Task<IActionResult> Index()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            var userIdStr = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(tableIdStr))
            {
                // Nếu chưa có bàn, quay lại trang chọn bàn hoặc menu
                return RedirectToAction("Index", "Menu");
            }

            int tableId = int.Parse(tableIdStr);

            // Truy vấn lấy giỏ hàng - Ưu tiên theo TableId
            var query = _context.CartItems
                .Include(c => c.Product)
                .Include(c => c.Table) // Cần thiết để hiện tên bàn
                .Where(c => c.TableId == tableId);

            // Nếu khách đã đăng nhập, lọc thêm theo UserId để bảo mật
            if (!string.IsNullOrEmpty(userIdStr))
            {
                int userId = int.Parse(userIdStr);
                query = query.Where(c => c.UserId == userId);
            }

            var cartItems = await query.ToListAsync();

            // Lấy tên bàn: Cách 1 từ dữ liệu Include, Cách 2 từ DB nếu giỏ trống
            var tableInfo = await _context.Tables.FindAsync(tableId);
            ViewBag.TableName = tableInfo?.TableName ?? "N/A";
            ViewBag.TableId = tableId;

            return View(cartItems);
        }

        // 2. Thêm món vào giỏ hàng
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            var userIdStr = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(tableIdStr))
            {
                return Json(new { success = false, message = "Vui lòng quét mã QR tại bàn!" });
            }

            int tableId = int.Parse(tableIdStr);
            int userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : 0;

            // Tìm món đã có trong giỏ của Bàn này + User này
            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

            if (existingItem == null)
            {
                var newItem = new CartItem
                {
                    ProductId = productId,
                    TableId = tableId,
                    UserId = userId, // Cực kỳ quan trọng để Index lọc được
                    Quantity = quantity
                };
                _context.CartItems.Add(newItem);
            }
            else
            {
                existingItem.Quantity += quantity;
                _context.CartItems.Update(existingItem);
            }

            await _context.SaveChangesAsync();

            var totalCount = await _context.CartItems
                .Where(c => c.TableId == tableId && c.UserId == userId)
                .SumAsync(c => c.Quantity);

            return Json(new { success = true, count = totalCount });
        }

        // 3. Lấy số lượng hiển thị trên Badge icon giỏ hàng
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            var userIdStr = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(tableIdStr)) return Json(0);

            int tableId = int.Parse(tableIdStr);
            int userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : 0;

            var count = await _context.CartItems
                .Where(c => c.TableId == tableId && c.UserId == userId)
                .SumAsync(c => c.Quantity);

            return Json(count);
        }

        // 4. Cập nhật số lượng (+/-)
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int change)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            int userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : 0;

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

            if (item != null)
            {
                item.Quantity += change;
                if (item.Quantity <= 0)
                    _context.CartItems.Remove(item);
                else
                    _context.CartItems.Update(item);

                await _context.SaveChangesAsync();
            }

            var cart = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.TableId == tableId && c.UserId == userId)
                .ToListAsync();

            return Json(new
            {
                success = true,
                count = cart.Sum(x => x.Quantity),
                total = cart.Sum(x => x.Quantity * (x.Product?.Price ?? 0)).ToString("N0")
            });
        }

        // 5. Xóa món
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            int userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : 0;

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // 6. Thanh toán / Đặt món
        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
                return Json(new { success = false, message = "Không tìm thấy thông tin bàn!" });

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            int? currentUserId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.TableId == tableId && (currentUserId == null || c.UserId == currentUserId))
                .ToListAsync();

            if (!cartItems.Any())
                return Json(new { success = false, message = "Giỏ hàng trống!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Cập nhật trạng thái bàn
                var table = await _context.Tables.FindAsync(tableId);
                if (table != null)
                {
                    table.Status = "Occupied";
                    _context.Tables.Update(table);
                }

                // Tạo Order
                var order = new Order
                {
                    TableId = tableId,
                    UserId = currentUserId,
                    OrderDate = DateTime.Now,
                    TotalAmount = cartItems.Sum(x => x.Quantity * (x.Product?.Price ?? 0)),
                    Status = "Pending"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Lưu OrderDetails
                foreach (var item in cartItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product?.Price ?? 0
                    });
                }

                // Xóa giỏ hàng
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = "Đặt món thành công!", orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}