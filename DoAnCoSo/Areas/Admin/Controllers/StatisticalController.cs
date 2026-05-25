using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DoAnCoSo.Controllers
{
    [Area("Admin")]
    public class StatisticalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticalController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string fromDate, string toDate, string sessionFilter = "")
        {
            IFormatProvider culture = new CultureInfo("vi-VN");
            DateTime start, end;

            if (!DateTime.TryParseExact(fromDate, "dd/MM/yyyy", culture, DateTimeStyles.None, out start)) start = DateTime.Today.AddDays(-30);
            if (!DateTime.TryParseExact(toDate, "dd/MM/yyyy", culture, DateTimeStyles.None, out end)) end = DateTime.Today;

            ViewBag.FromDate = start.ToString("dd/MM/yyyy");
            ViewBag.ToDate = end.ToString("dd/MM/yyyy");
            ViewBag.CurrentSession = sessionFilter;

            DateTime endDateTime = end.AddDays(1).AddSeconds(-1);

            // 1. Lấy dữ liệu
            var rawOrders = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= endDateTime)
                .ToListAsync();

            var rawDetails = await _context.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.OrderDate >= start && od.Order.OrderDate <= endDateTime)
                .ToListAsync();

            var filteredOrders = rawOrders.AsEnumerable();
            var filteredDetails = rawDetails.AsEnumerable();

            // Lọc ca
            if (!string.IsNullOrEmpty(sessionFilter))
            {
                filteredOrders = sessionFilter switch
                {
                    "Sang" => filteredOrders.Where(o => o.OrderDate.Hour >= 6 && o.OrderDate.Hour < 12),
                    "Trua" => filteredOrders.Where(o => o.OrderDate.Hour >= 12 && o.OrderDate.Hour < 17),
                    "Toi" => filteredOrders.Where(o => o.OrderDate.Hour >= 17 || o.OrderDate.Hour < 6),
                    _ => filteredOrders
                };
                filteredDetails = filteredDetails.Where(od => filteredOrders.Any(o => o.OrderId == od.OrderId));
            }

            var validOrders = filteredOrders.Where(o => o.Status == "Completed" || o.Status == "Paid").ToList();

            // 2. Gán dữ liệu tổng quát (Dùng trực tiếp TotalAmount từ Model)
            ViewBag.TotalRevenue = validOrders.Sum(o => o.TotalAmount);
            ViewBag.TotalOrders = filteredOrders.Count();
            ViewBag.SuccessOrders = validOrders.Count;
            ViewBag.CancelledOrders = filteredOrders.Count(o => o.Status == "Cancelled");

            // 3. Đồ thị
            var revenueByDay = validOrders.GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.TotalAmount) })
                .OrderBy(g => g.Date).ToList();

            ViewBag.ChartLabels = string.Join(",", revenueByDay.Select(r => $"'{r.Date:dd/MM/yyyy}'"));
            ViewBag.ChartData = string.Join(",", revenueByDay.Select(r => r.Revenue));

            // 4. Top 5 món ăn (Sử dụng tỷ lệ để phân bổ tiền giảm giá)
            var topDishes = filteredDetails
                .Where(od => validOrders.Any(o => o.OrderId == od.OrderId))
                .GroupBy(od => od.Product?.ProductName ?? "Món không xác định")
                .Select(g =>
                {
                    decimal totalOrig = g.Sum(od => od.Quantity * od.UnitPrice);

                    // Tính doanh thu thực thu: Lấy tổng tiền đơn hàng của các món này, 
                    // tỉ lệ hóa theo đơn giá niêm yết để phân bổ tiền giảm giá công bằng
                    decimal totalActual = 0;
                    foreach (var od in g)
                    {
                        var o = validOrders.FirstOrDefault(x => x.OrderId == od.OrderId);
                        if (o != null)
                        {
                            // Tỷ trọng của món này trong đơn hàng
                            decimal ratio = (od.Quantity * od.UnitPrice) / (o.TotalAmount + o.DiscountAmount);
                            totalActual += (o.TotalAmount * ratio);
                        }
                    }

                    return new TopDishViewModel
                    {
                        DishName = g.Key,
                        QuantitySold = g.Sum(od => od.Quantity),
                        OriginalRevenue = totalOrig,
                        ActualRevenue = totalActual,
                        DiscountRevenue = totalOrig - totalActual
                    };
                })
                .OrderByDescending(x => x.ActualRevenue)
                .Take(5)
                .ToList();

            return View(topDishes);
        }
    }

    public class TopDishViewModel
    {
        public string DishName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal OriginalRevenue { get; set; }
        public decimal DiscountRevenue { get; set; }
        public decimal ActualRevenue { get; set; }
    }
}