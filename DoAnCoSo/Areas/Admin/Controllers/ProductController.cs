using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // 1. HIỂN THỊ DANH SÁCH SẢN PHẨM
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();
            return View(products);
        }

        // 2. TẠO MỚI SẢN PHẨM (GET)
        public IActionResult Create()
        {
            // Chỉ lấy các danh mục con (có ParentId) để gán món ăn vào
            ViewBag.CategoryId = new SelectList(_context.Categories.Where(c => c.ParentId != null), "CategoryId", "CategoryName");
            return View();
        }

        // 3. TẠO MỚI SẢN PHẨM (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null)
                {
                    product.ImagePath = await SaveImage(ImageFile);
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm món ăn thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoryId = new SelectList(_context.Categories.Where(c => c.ParentId != null), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // 4. CẬP NHẬT SẢN PHẨM (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.CategoryId = new SelectList(_context.Categories.Where(c => c.ParentId != null), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // 5. CẬP NHẬT SẢN PHẨM (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? ImageFile)
        {
            if (id != product.ProductId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Lấy dữ liệu cũ từ DB (không theo dõi) để xử lý ảnh
                    var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);

                    if (ImageFile != null)
                    {
                        // Xóa ảnh cũ vật lý nếu có
                        DeletePhysicalFile(existingProduct?.ImagePath);
                        // Lưu ảnh mới
                        product.ImagePath = await SaveImage(ImageFile);
                    }
                    else
                    {
                        // Giữ lại đường dẫn ảnh cũ nếu không thay đổi
                        product.ImagePath = existingProduct?.ImagePath;
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật món ăn thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId)) return NotFound();
                    else throw;
                }
            }

            ViewBag.CategoryId = new SelectList(_context.Categories.Where(c => c.ParentId != null), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // 6. XÓA SẢN PHẨM (GET - Xác nhận)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // 7. XÓA SẢN PHẨM (POST - Xác nhận)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Kiểm tra xem món ăn này có trong đơn hàng nào không (Ràng buộc dữ liệu)
                bool isInOrder = await _context.OrderDetails.AnyAsync(od => od.ProductId == id);
                if (isInOrder)
                {
                    TempData["Error"] = "Không thể xóa món ăn này vì đã có trong lịch sử đơn hàng!";
                    return RedirectToAction(nameof(Index));
                }

                DeletePhysicalFile(product.ImagePath); // Xóa ảnh thực tế
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa món ăn thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- CÁC PHƯƠNG THỨC HỖ TRỢ (PRIVATE) ---

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }

        private void DeletePhysicalFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            string savePath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

            // Tạo tên file duy nhất để tránh trùng lặp
            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(image.FileName);
            string filePath = Path.Combine(savePath, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return fileName;
        }
    }
}