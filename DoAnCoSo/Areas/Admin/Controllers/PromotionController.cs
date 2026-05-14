using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PromotionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public PromotionController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // Hiển thị danh sách Voucher
        public async Task<IActionResult> Index()
        {
            var promos = await _context.Promotions.ToListAsync();
            return View(promos);
        }

        // Trang Tạo mới
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Promotion promo, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                if (file != null)
                {
                    promo.ImageUrl = await SaveImage(file);
                }

                _context.Promotions.Add(promo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(promo);
        }

        // --- SỬA LỖI EDIT TẠI ĐÂY ---
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound();
            return View(promo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Promotion promotion, IFormFile? ImageFile)
        {
            if (id != promotion.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // Xóa ảnh cũ nếu có
                        if (!string.IsNullOrEmpty(promotion.ImageUrl))
                        {
                            DeleteOldImage(promotion.ImageUrl);
                        }
                        // Lưu ảnh mới
                        promotion.ImageUrl = await SaveImage(ImageFile);
                    }

                    _context.Update(promotion);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.Id)) return NotFound();
                    throw;
                }
            }
            return View(promotion);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var obj = await _context.Promotions.FindAsync(id);
            if (obj == null) return NotFound();

            if (!string.IsNullOrEmpty(obj.ImageUrl))
            {
                DeleteOldImage(obj.ImageUrl);
            }

            _context.Promotions.Remove(obj);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // --- CÁC HÀM PHỤ TRỢ (HELPER) ---

        private async Task<string> SaveImage(IFormFile file)
        {
            string wwwRootPath = _hostEnvironment.WebRootPath;
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string folderPath = Path.Combine(wwwRootPath, @"images/promotions");

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fullPath = Path.Combine(folderPath, fileName);
            using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return @"/images/promotions/" + fileName;
        }

        private void DeleteOldImage(string imageUrl)
        {
            var oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.Id == id);
        }
    }
}