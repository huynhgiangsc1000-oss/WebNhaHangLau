using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
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

        // Constructor tiêm cả DbContext và UserManager
        public CartController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Hiển thị giỏ hàng của bàn hiện tại
        public async Task<IActionResult> Index()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (string.IsNullOrEmpty(tableIdStr))
            {
                return RedirectToAction("Index", "Menu");
            }

            int tableId = int.Parse(tableIdStr);
            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.TableId == tableId)
                .ToListAsync();

            ViewBag.TableId = tableId;
            return View(cartItems);
        }

        // 2. Thêm món vào giỏ hàng
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
            {
                return Json(new { success = false, message = "Vui lòng quét mã QR tại bàn!" });
            }

            int tableId = int.Parse(tableIdStr);

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId);

            if (existingItem == null)
            {
                var newItem = new CartItem
                {
                    ProductId = productId,
                    TableId = tableId,
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

            var totalCount = await _context.CartItems.Where(c => c.TableId == tableId).SumAsync(c => c.Quantity);
            return Json(new { success = true, count = totalCount });
        }

        // 3. Cập nhật số lượng món ăn trong giỏ
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int change)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId);

            if (item != null)
            {
                item.Quantity += change;
                if (item.Quantity <= 0)
                    _context.CartItems.Remove(item);
                else
                    _context.CartItems.Update(item);

                await _context.SaveChangesAsync();
            }

            var cart = await _context.CartItems.Include(c => c.Product).Where(c => c.TableId == tableId).ToListAsync();
            return Json(new
            {
                success = true,
                count = cart.Sum(x => x.Quantity),
                total = cart.Sum(x => x.Quantity * x.Product.Price).ToString("N0")
            });
        }

        // 4. Xóa món khỏi giỏ
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            var cart = await _context.CartItems.Include(c => c.Product).Where(c => c.TableId == tableId).ToListAsync();
            return Json(new
            {
                success = true,
                count = cart.Sum(x => x.Quantity),
                total = cart.Sum(x => x.Quantity * x.Product.Price).ToString("N0")
            });
        }

        // 5. Xác nhận đặt món (Checkout)
        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
                return Json(new { success = false, message = "Không tìm thấy thông tin bàn!" });

            int tableId = int.Parse(tableIdStr);

            // Lấy User đang đăng nhập
            var userIdString = _userManager.GetUserId(User);
            int? currentUserId = string.IsNullOrEmpty(userIdString) ? null : int.Parse(userIdString);

            // Lấy danh sách món trong giỏ của bàn
            var cartItems = await _context.CartItems.Include(c => c.Product)
                                          .Where(c => c.TableId == tableId).ToListAsync();

            if (!cartItems.Any())
                return Json(new { success = false, message = "Giỏ hàng của bạn đang trống!" });

            // BẮT ĐẦU TRANSACTION ĐỂ ĐẢM BẢO DỮ LIỆU ĐỒNG NHẤT
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Cập nhật trạng thái bàn sang "Occupied" (Màu đỏ trên sơ đồ)
                var table = await _context.Tables.FindAsync(tableId);
                if (table != null)
                {
                    table.Status = "Occupied";
                    _context.Tables.Update(table);
                }

                // B. Tạo Order mới
                var order = new Order
                {
                    TableId = tableId,
                    UserId = currentUserId,
                    OrderDate = DateTime.Now,
                    TotalAmount = cartItems.Sum(x => x.Quantity * x.Product.Price),
                    Status = "Pending" // Chờ nhà bếp/admin xác nhận
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Lưu để có OrderId

                // C. Lưu chi tiết đơn hàng (OrderDetail)
                foreach (var item in cartItems)
                {
                    var detail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    };
                    _context.OrderDetails.Add(detail);
                }

                // D. Xóa giỏ hàng của bàn sau khi đã đặt xong
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Hoàn tất các bước

                return Json(new { success = true, message = "Đặt món thành công!", orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
    }
}