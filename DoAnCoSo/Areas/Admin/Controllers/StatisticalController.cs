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

        public async Task<IActionResult> Index(string fromDate, string toDate, string sessionFilter = "", string preset = "")
        {
            IFormatProvider culture = new CultureInfo("vi-VN");
            DateTime start, end;
            int year = DateTime.Today.Year;

            // 1. Xử lý logic chọn nhanh (Preset)
            if (!string.IsNullOrEmpty(preset))
            {
                switch (preset)
                {
                    case "today": start = end = DateTime.Today; break;
                    case "thisMonth": start = new DateTime(year, DateTime.Today.Month, 1); end = DateTime.Today; break;
                    case "q1": start = new DateTime(year, 1, 1); end = new DateTime(year, 3, 31); break;
                    case "q2": start = new DateTime(year, 4, 1); end = new DateTime(year, 6, 30); break;
                    case "q3": start = new DateTime(year, 7, 1); end = new DateTime(year, 9, 30); break;
                    case "q4": start = new DateTime(year, 10, 1); end = new DateTime(year, 12, 31); break;
                    default: start = DateTime.Today.AddDays(-30); end = DateTime.Today; break;
                }
            }
            else
            {
                // Nếu không dùng preset, lấy từ input hoặc mặc định 30 ngày qua
                if (!DateTime.TryParseExact(fromDate, "dd/MM/yyyy", culture, DateTimeStyles.None, out start))
                    start = DateTime.Today.AddDays(-30);
                if (!DateTime.TryParseExact(toDate, "dd/MM/yyyy", culture, DateTimeStyles.None, out end))
                    end = DateTime.Today;
            }

            // Gán dữ liệu cho View
            ViewBag.FromDate = start.ToString("dd/MM/yyyy");
            ViewBag.ToDate = end.ToString("dd/MM/yyyy");
            ViewBag.CurrentSession = sessionFilter;
            ViewBag.CurrentPreset = preset;

            DateTime endDateTime = end.AddDays(1).AddSeconds(-1);

            // 2. Lấy dữ liệu từ Database
            var rawOrders = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= endDateTime)
                .ToListAsync();

            var rawDetails = await _context.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.OrderDate >= start && od.Order.OrderDate <= endDateTime)
                .ToListAsync();

            var filteredOrders = rawOrders.AsEnumerable();
            var filteredDetails = rawDetails.AsEnumerable();

            // 3. Lọc theo ca làm việc
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

            // 4. Tính toán số liệu thống kê
            ViewBag.TotalRevenue = validOrders.Sum(o => o.TotalAmount - o.DiscountAmount);
            ViewBag.TotalOrders = filteredOrders.Count();
            ViewBag.SuccessOrders = validOrders.Count;
            ViewBag.CancelledOrders = filteredOrders.Count(o => o.Status == "Cancelled");

       
            var revenueByDay = new List<object>();

            // Lặp qua từng ngày từ start đến end
            for (var dt = start.Date; dt <= end.Date; dt = dt.AddDays(1))
            {
                // Tìm doanh thu của ngày đó trong danh sách validOrders, nếu không có thì bằng 0
                var dayRevenue = validOrders
                    .Where(o => o.OrderDate.Date == dt)
                    .Sum(o => o.TotalAmount - o.DiscountAmount);

                revenueByDay.Add(new { Date = dt.ToString("dd/MM/yyyy"), Revenue = dayRevenue });
            }

            ViewBag.ChartLabels = string.Join(",", revenueByDay.Select(r => $"'{((dynamic)r).Date}'"));
            ViewBag.ChartData = string.Join(",", revenueByDay.Select(r => ((dynamic)r).Revenue));
            // Biểu đồ trạng thái
            var statusStats = filteredOrders.GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() }).ToList();
            ViewBag.StatusLabels = string.Join(",", statusStats.Select(s => $"'{s.Status}'"));
            ViewBag.StatusData = string.Join(",", statusStats.Select(s => s.Count));

            // 6. Xử lý Top 5 món ăn
            var topDishes = filteredDetails
                .GroupBy(od => od.Product?.ProductName ?? "Món không xác định")
                .Select(g => {
                    decimal totalOrig = g.Sum(od => od.Quantity * od.UnitPrice);
                    return new TopDishViewModel
                    {
                        DishName = g.Key,
                        QuantitySold = g.Sum(od => od.Quantity),
                        OriginalRevenue = totalOrig,
                        ActualRevenue = totalOrig * 0.9m, // Ví dụ phân bổ doanh thu
                        DiscountRevenue = totalOrig * 0.1m
                    };
                })
                .OrderByDescending(x => x.ActualRevenue)
                .Take(5).ToList();

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