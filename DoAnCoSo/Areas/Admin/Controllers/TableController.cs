using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using QRCoder;
using Microsoft.AspNetCore.Authorization;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]// Chỉ cho phép tài khoản Admin truy cập
    public class TableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TableController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Hiển thị danh sách tất cả các bàn
        public async Task<IActionResult> Index()
        {
            var tables = await _context.Tables
                .OrderBy(t => t.TableName)
                .ToListAsync();
            return View(tables);
        }

        // 2. GET: Trang thêm bàn mới
        public IActionResult Create()
        {
            return View();
        }

        // 3. POST: Xử lý thêm bàn mới và tự động sinh mã QR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TableName,Status")] Table table)
        {
            // Kiểm tra xem tên bàn đã tồn tại chưa
            if (await _context.Tables.AnyAsync(t => t.TableName == table.TableName))
            {
                ModelState.AddModelError("TableName", "Tên bàn này đã tồn tại trong hệ thống.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Bước A: Thêm bàn vào Database trước để lấy TableId
                    _context.Add(table);
                    await _context.SaveChangesAsync();

                    // Bước B: Tạo đường dẫn URL cho mã QR 
                    // Đường dẫn trỏ đến trang Menu của khách hàng kèm TableId
                    var domain = $"{Request.Scheme}://{Request.Host}";
                    var qrUrl = $"{domain}/Customer/Menu/Index?tableId={table.TableId}";

                    // Bước C: Sử dụng QRCoder để sinh mã QR dạng Base64
                    using (var qrGenerator = new QRCodeGenerator())
                    using (var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q))
                    using (var qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrBytes = qrCode.GetGraphic(20);
                        table.QrCode = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
                    }

                    // Bước D: Cập nhật lại bàn đã có mã QR vào DB
                    _context.Update(table);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Đã thêm bàn {table.TableName} thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                }
            }
            return View(table);
        }

        // 4. GET: Xem chi tiết bàn và mã QR lớn để in
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var table = await _context.Tables.FirstOrDefaultAsync(m => m.TableId == id);
            if (table == null) return NotFound();

            return View(table);
        }

        // 5. GET: Trang xác nhận xóa bàn
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var table = await _context.Tables.FirstOrDefaultAsync(m => m.TableId == id);
            if (table == null) return NotFound();

            return View(table);
        }

        // 6. POST: Xử lý xóa bàn
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table != null)
            {
                _context.Tables.Remove(table);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa bàn thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 7. POST: Reset trạng thái bàn về "Trống" (Dùng khi khách đã thanh toán/rời đi)
        [HttpPost]
        public async Task<IActionResult> ResetTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table != null)
            {
                table.Status = "Empty";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Bàn {table.TableName} đã được đưa về trạng thái trống.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 8. POST: Cập nhật mã QR (Dùng khi domain thay đổi hoặc muốn làm mới mã)
        [HttpPost]
        public async Task<IActionResult> RefreshQRCode(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            var domain = $"{Request.Scheme}://{Request.Host}";
            var qrUrl = $"{domain}/Customer/Menu/Index?tableId={table.TableId}";

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrBytes = qrCode.GetGraphic(20);
                table.QrCode = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
            }

            _context.Update(table);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã làm mới mã QR.";
            return RedirectToAction(nameof(Index));
        }
    }
}