using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CartController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Hiển thị trang giỏ hàng
        public async Task<IActionResult> Index()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
            {
                return RedirectToAction("Index", "Menu");
            }

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Challenge();
            int userId = int.Parse(userIdStr);

            // SỬA LỖI: Chỉ lấy món thuộc về BÀN HIỆN TẠI của NGƯỜI DÙNG HIỆN TẠI
            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Include(c => c.Table)
                .Where(c => c.TableId == tableId && c.UserId == userId)
                .ToListAsync();

            var tableInfo = await _context.Tables.FindAsync(tableId);
            ViewBag.TableName = tableInfo?.TableName ?? "N/A";
            ViewBag.TableId = tableId;

            return View(cartItems);
        }

        // 2. Thêm món vào giỏ hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
            {
                return Json(new { success = false, message = "Vui lòng quét mã QR tại bàn trước khi đặt món!" });
            }

            if (!int.TryParse(tableIdStr, out int tableId))
            {
                return Json(new { success = false, message = "Mã bàn không hợp lệ." });
            }

            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }
            int userId = int.Parse(userIdStr);

            // Kiểm tra trạng thái bàn/booking để đảm bảo phiên làm việc còn hiệu lực
            var activeBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.TableId == tableId && b.UserId == userId && b.Status == "CheckedIn");

            if (activeBooking == null)
            {
                // Tìm booking của bàn này đang ở trạng thái CheckedIn mà chưa có UserId
                var unlinkedBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.TableId == tableId && b.UserId == null && b.Status == "CheckedIn");

                if (unlinkedBooking != null)
                {
                    unlinkedBooking.UserId = userId;
                    _context.Bookings.Update(unlinkedBooking);

                    // Đồng thời liên kết cả Order tương ứng nếu có
                    var unlinkedOrder = await _context.Orders
                        .FirstOrDefaultAsync(o => o.BookingId == unlinkedBooking.BookingId && o.UserId == null);
                    if (unlinkedOrder != null)
                    {
                        unlinkedOrder.UserId = userId;
                        _context.Orders.Update(unlinkedOrder);
                    }

                    await _context.SaveChangesAsync();
                    activeBooking = unlinkedBooking;
                }
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsAvailable)
            {
                return Json(new { success = false, message = "Sản phẩm hiện không khả dụng." });
            }

            try
            {
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

                if (existingItem == null)
                {
                    _context.CartItems.Add(new CartItem
                    {
                        ProductId = productId,
                        TableId = tableId,
                        UserId = userId,
                        Quantity = quantity
                    });
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

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {product.ProductName} vào giỏ hàng!",
                    count = totalCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // 3. Cập nhật số lượng (+/-)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int change)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false });
            int userId = int.Parse(userIdStr);

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

            if (item != null)
            {
                item.Quantity += change;
                if (item.Quantity <= 0) _context.CartItems.Remove(item);
                else _context.CartItems.Update(item);
                await _context.SaveChangesAsync();
            }

            var cart = await _context.CartItems.Include(c => c.Product)
                .Where(c => c.TableId == tableId && c.UserId == userId).ToListAsync();

            return Json(new
            {
                success = true,
                count = cart.Sum(x => x.Quantity),
                total = cart.Sum(x => x.Quantity * (x.Product?.Price ?? 0)).ToString("N0")
            });
        }

        // 4. Xóa món khỏi giỏ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr)) return Json(new { success = false });

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false });
            int userId = int.Parse(userIdStr);

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.TableId == tableId && c.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        // 5. Lấy số lượng cho icon Badge
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr)) return Json(0);

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Json(0);
            int userId = int.Parse(userIdStr);

            var count = await _context.CartItems
                .Where(c => c.TableId == tableId && c.UserId == userId)
                .SumAsync(c => c.Quantity);
            return Json(count);
        }

        // 6. XÁC NHẬN ĐẶT MÓN (CHECKOUT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];
            if (string.IsNullOrEmpty(tableIdStr))
            {
                return Json(new { success = false, message = "Vui lòng quét mã QR tại bàn để đặt món!" });
            }

            int tableId = int.Parse(tableIdStr);
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Lỗi định danh người dùng!" });
            int userId = int.Parse(userIdStr);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Lấy giỏ hàng chính xác theo User và Bàn
                    var cartItems = await _context.CartItems
                        .Include(c => c.Product)
                        .Where(c => c.UserId == userId && c.TableId == tableId)
                        .ToListAsync();

                    if (!cartItems.Any())
                        return Json(new { success = false, message = "Giỏ hàng trống!" });

                    // A. Cập nhật trạng thái bàn
                    var table = await _context.Tables.FindAsync(tableId);
                    if (table != null && table.Status != "Occupied")
                    {
                        table.Status = "Occupied";
                        _context.Tables.Update(table);
                    }

                    // B. Kiểm tra đơn hàng đang hoạt động tại bàn này
                    var existingOrder = await _context.Orders
                        .FirstOrDefaultAsync(o => o.TableId == tableId
                            && o.UserId == userId
                            && o.Status != "Completed"
                            && o.Status != "Cancelled");

                    if (existingOrder == null)
                    {
                        // Tìm đơn hàng đang hoạt động tại bàn này nhưng chưa có UserId
                        var unlinkedOrder = await _context.Orders
                            .Include(o => o.Booking)
                            .FirstOrDefaultAsync(o => o.TableId == tableId
                                && o.UserId == null
                                && o.Status != "Completed"
                                && o.Status != "Cancelled");

                        if (unlinkedOrder != null)
                        {
                            unlinkedOrder.UserId = userId;
                            _context.Orders.Update(unlinkedOrder);

                            if (unlinkedOrder.Booking != null && unlinkedOrder.Booking.UserId == null)
                            {
                                unlinkedOrder.Booking.UserId = userId;
                                _context.Bookings.Update(unlinkedOrder.Booking);
                            }

                            await _context.SaveChangesAsync();
                            existingOrder = unlinkedOrder;
                        }
                    }

                    // Tìm booking đang CheckedIn tại bàn này để liên kết với đơn hàng mới nếu có
                    var activeBooking = await _context.Bookings
                        .FirstOrDefaultAsync(b => b.TableId == tableId 
                            && b.UserId == userId 
                            && b.Status == "CheckedIn");

                    int finalOrderId;

                    if (existingOrder != null)
                    {
                        // GỘP MÓN vào đơn hàng hiện tại
                        foreach (var item in cartItems)
                        {
                            var existingDetail = await _context.OrderDetails
                                .FirstOrDefaultAsync(od => od.OrderId == existingOrder.OrderId && od.ProductId == item.ProductId);

                            if (existingDetail != null)
                            {
                                // Món đã có → tăng số lượng
                                existingDetail.Quantity += item.Quantity;
                            }
                            else
                            {
                                // Món mới → thêm vào
                                _context.OrderDetails.Add(new OrderDetail
                                {
                                    OrderId = existingOrder.OrderId,
                                    ProductId = item.ProductId,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.Product?.Price ?? 0
                                });
                            }
                        }
                        existingOrder.TotalAmount += cartItems.Sum(x => x.Quantity * (x.Product?.Price ?? 0));
                        finalOrderId = existingOrder.OrderId;
                    }
                    else
                    {
                        // Tạo đơn hàng MỚI (không có đơn cũ)
                        var order = new Order
                        {
                            TableId = tableId,
                            UserId = userId,
                            OrderDate = DateTime.Now,
                            TotalAmount = cartItems.Sum(x => x.Quantity * (x.Product?.Price ?? 0)),
                            Status = activeBooking != null ? "Processing" : "Pending",
                            BookingId = activeBooking?.BookingId
                        };
                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();

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
                        finalOrderId = order.OrderId;
                    }

                    // C. Xóa giỏ hàng của bàn này
                    _context.CartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Đặt món thành công!", orderId = finalOrderId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            });
        }
    }
}