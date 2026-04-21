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

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        // ... Create (Giữ nguyên hoặc thêm kiểm tra trùng tên) ...

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? ImageFile)
        {
            if (id != product.ProductId) return NotFound();

            // Lấy dữ liệu cũ để giữ lại ImagePath nếu không upload ảnh mới
            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);

            if (ModelState.IsValid)
            {
                try
                {
                    if (ImageFile != null)
                    {
                        // Xóa ảnh cũ vật lý
                        DeletePhysicalFile(existingProduct.ImagePath);
                        // Lưu ảnh mới
                        product.ImagePath = await SaveImage(ImageFile);
                    }
                    else
                    {
                        product.ImagePath = existingProduct.ImagePath;
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu.");
                }
            }
            ViewBag.CategoryId = new SelectList(_context.Categories.Where(c => c.ParentId != null), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                DeletePhysicalFile(product.ImagePath); // Xóa ảnh khi xóa sản phẩm
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private void DeletePhysicalFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            string savePath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
            string fileName = Guid.NewGuid().ToString() + "_" + image.FileName;
            string filePath = Path.Combine(savePath, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return fileName;
        }
    }
}