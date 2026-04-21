using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(int? categoryId, int? tableId)
        {
            // --- 1. XỬ LÝ TABLE ID ---
            // Ưu tiên: Tham số URL > Cookie > Session
            if (!tableId.HasValue)
            {
                var cookieId = Request.Cookies["SavedTableId"];
                if (int.TryParse(cookieId, out int id)) tableId = id;
            }

            if (tableId.HasValue)
            {
                var table = await _context.Tables.FindAsync(tableId.Value);
                if (table != null)
                {
                    if (table.Status == "Empty")
                    {
                        table.Status = "Occupied";
                        _context.Update(table);
                        await _context.SaveChangesAsync();
                    }
                    ViewBag.CurrentTable = table;

                    // Lưu lại để các request sau không bị mất
                    HttpContext.Session.SetString("TableId", tableId.Value.ToString());
                    Response.Cookies.Append("SavedTableId", tableId.Value.ToString(), new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(1),
                        HttpOnly = true,
                        IsEssential = true
                    });
                }
            }

            // --- 2. LẤY DANH MỤC ---
            var allCategories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.Categories = allCategories;
            ViewBag.SelectedId = categoryId;

            // --- 3. TRUY VẤN SẢN PHẨM ---
            var productsQuery = _context.Products.AsNoTracking()
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                // Lấy ID chính nó và các ID con để hiển thị sản phẩm của cả nhóm
                var listCategoryIds = allCategories
                    .Where(c => c.CategoryId == categoryId || c.ParentId == categoryId)
                    .Select(c => c.CategoryId)
                    .ToList();

                productsQuery = productsQuery.Where(p => listCategoryIds.Contains(p.CategoryId));
            }

            return View(await productsQuery.ToListAsync());
        }

        public async Task<IActionResult> GetProductDetail(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null) return NotFound();
            return PartialView("_ProductDetailPartial", product);
        }
    }
}