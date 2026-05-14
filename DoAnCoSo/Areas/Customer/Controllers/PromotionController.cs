using DoAnCoSo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class PromotionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách tất cả ưu đãi đang diễn ra
        // Trong PromotionController.cs
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var promotions = await _context.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now) // Chỉ lấy mã đang hiệu lực
                .AsNoTracking()
                .ToListAsync();
            return View(promotions);
        }

        // Xem chi tiết một ưu đãi cụ thể
        public async Task<IActionResult> Details(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null) return NotFound();

            return View(promotion);
        }
    }
}