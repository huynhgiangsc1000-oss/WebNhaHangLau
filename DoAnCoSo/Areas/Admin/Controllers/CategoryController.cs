using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context) => _context = context;

        // Hiển thị danh sách phân cấp
        // Sửa lại Index để nhìn rõ phân cấp trong trang quản trị
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentId ?? c.CategoryId) // Sắp xếp để con đi theo cha
                .ThenBy(c => c.ParentId == null ? 0 : 1)
                .ToListAsync();
            return View(categories);
        }

        // Sửa lại Create/Edit để chỉ những thằng không có cha mới được làm "Cha" của thằng khác
        public IActionResult Create()
        {
            // Chỉ lấy những danh mục gốc (ParentId == null) để làm danh sách lựa chọn
            ViewBag.ParentId = new SelectList(_context.Categories.Where(c => c.ParentId == null), "CategoryId", "CategoryName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm loại món thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ParentId = new SelectList(_context.Categories.Where(c => c.ParentId == null), "CategoryId", "CategoryName", category.ParentId);
            return View(category);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            // Lấy danh sách nhóm gốc, loại bỏ chính nó để tránh chọn chính mình làm cha
            ViewBag.ParentId = new SelectList(_context.Categories.Where(c => c.ParentId == null && c.CategoryId != id), "CategoryId", "CategoryName", category.ParentId);
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.CategoryId) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var category = await _context.Categories.Include(c => c.ParentCategory).FirstOrDefaultAsync(m => m.CategoryId == id);
            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return RedirectToAction(nameof(Index));

            // Kiểm tra xem có danh mục con (Sub-category) không
            bool hasSubCategories = await _context.Categories.AnyAsync(c => c.ParentId == id);

            // Kiểm tra xem có sản phẩm (Product) nào thuộc danh mục này không
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);

            if (hasSubCategories || hasProducts)
            {
                TempData["Error"] = "LỖI: Danh mục này đang chứa món ăn hoặc danh mục con. Hãy xóa hoặc di chuyển chúng trước khi xóa danh mục này!";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa danh mục thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}