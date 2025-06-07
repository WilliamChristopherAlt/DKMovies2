using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admins Dashboard
        public async Task<IActionResult> Index()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalEmployees = await _context.Employees.CountAsync();
                var totalMovies = await _context.Movies.CountAsync();
                var totalShowTimes = await _context.ShowTimes.CountAsync();
                var totalConcessions = await _context.Concessions.CountAsync();

                // Revenue from tickets
                var ticketRevenue = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .SumAsync(t => t.ShowTime.Price);

                // Revenue from concession orders
                var concessionRevenue = await _context.OrderItems
                    .SumAsync(oi => oi.Quantity * oi.PriceAtPurchase);

                var totalRevenue = ticketRevenue + concessionRevenue;

                var model = new DashboardViewModel
                {
                    TotalUsers = totalUsers,
                    TotalEmployees = totalEmployees,
                    TotalMovies = totalMovies,
                    TotalShowTimes = totalShowTimes,
                    TotalConcessions = totalConcessions,
                    TotalRevenue = totalRevenue,
                    TicketRevenue = ticketRevenue,
                    ConcessionRevenue = concessionRevenue
                };

                ViewBag.TotalUsers = totalUsers;
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.TotalMovies = totalMovies;
                ViewBag.TotalShowTimes = totalShowTimes;
                ViewBag.TotalConcessions = totalConcessions;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.TicketRevenue = ticketRevenue;
                ViewBag.ConcessionRevenue = concessionRevenue;

                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.TotalUsers = 0;
                ViewBag.TotalEmployees = 0;
                ViewBag.TotalMovies = 0;
                ViewBag.TotalShowTimes = 0;
                ViewBag.TotalConcessions = 0;
                ViewBag.TotalRevenue = 0;
                ViewBag.TicketRevenue = 0;
                ViewBag.ConcessionRevenue = 0;
                ViewBag.ErrorMessage = "Có lỗi xảy ra khi tải dữ liệu dashboard.";

                return View(new DashboardViewModel());
            }
        }

        // ===== SỬA MovieDashboard ACTION TRONG AdminsController =====
        public async Task<IActionResult> MovieDashboard()
        {
            try
            {
                // ✅ Create DashboardViewModel with basic stats
                var model = new DashboardViewModel
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalEmployees = await _context.Employees.CountAsync(),
                    TotalMovies = await _context.Movies.CountAsync(),
                    TotalShowTimes = await _context.ShowTimes.CountAsync(),
                    TotalConcessions = await _context.Concessions.CountAsync(),
                    TotalRevenue = await _context.Tickets
                        .Include(t => t.ShowTime)
                        .SumAsync(t => t.ShowTime.Price),

                    // ✅ Additional metrics
                    TodayTickets = await _context.Tickets
                        .Where(t => t.PurchaseTime.Date == DateTime.Today)
                        .CountAsync(),
                    ThisMonthRevenue = await _context.Tickets
                        .Include(t => t.ShowTime)
                        .Where(t => t.PurchaseTime.Month == DateTime.Now.Month &&
                                   t.PurchaseTime.Year == DateTime.Now.Year)
                        .SumAsync(t => t.ShowTime.Price),
                    ActiveShowtimes = await _context.ShowTimes
                        .Where(st => st.StartTime > DateTime.Now)
                        .CountAsync()
                };

                // ✅ Check if we have any tickets first
                var hasTickets = await _context.Tickets.AnyAsync();
                if (!hasTickets)
                {
                    model.TopMovies = new List<MovieScoreViewModel>();
                    ViewBag.ErrorMessage = "Chưa có dữ liệu bán vé để phân tích";
                    return View(model);
                }

                var movieStats = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                    .ThenInclude(m => m.Reviews)
                    .Where(t => t.ShowTime != null && t.ShowTime.Movie != null)
                    .GroupBy(t => t.ShowTime.MovieID)
                    .Select(g => new
                    {
                        MovieID = g.Key,
                        g.First().ShowTime.Movie.Title,
                        TicketsSold = g.Count(),
                        TotalRevenue = g.Sum(t => t.ShowTime.Price),
                        AvgRating = g.First().ShowTime.Movie.Reviews.Any()
                            ? g.First().ShowTime.Movie.Reviews.Average(r => r.Rating)
                            : 0
                    })
                    .ToListAsync();

                if (!movieStats.Any())
                {
                    model.TopMovies = new List<MovieScoreViewModel>();
                    ViewBag.ErrorMessage = "Không có dữ liệu phim để hiển thị";
                    return View(model);
                }

                double maxRevenue = movieStats.Max(s => (double)s.TotalRevenue);
                double maxTickets = movieStats.Max(s => (double)s.TicketsSold);
                double maxRating = 5.0;

                var scored = movieStats.Select(s => new MovieScoreViewModel
                {
                    MovieID = s.MovieID,
                    Title = s.Title ?? "Unknown Movie",
                    TicketsSold = s.TicketsSold,
                    TotalRevenue = s.TotalRevenue,
                    AvgRating = s.AvgRating,
                    PriorityScore = maxRevenue > 0 && maxTickets > 0
                        ? (double)s.TotalRevenue / maxRevenue * 50
                          + s.TicketsSold / maxTickets * 40
                          + s.AvgRating / maxRating * 10
                        : 0
                })
                .OrderByDescending(s => s.PriorityScore)
                .Take(5)
                .ToList();

                // ✅ Set TopMovies in the model
                model.TopMovies = scored;

                // ✅ Calculate average rating
                model.AverageRating = scored.Any() ? scored.Average(m => m.AvgRating) : 0;

                return View(model); // Return DashboardViewModel
            }
            catch (Exception ex)
            {
                var errorModel = new DashboardViewModel
                {
                    TotalUsers = 0,
                    TotalEmployees = 0,
                    TotalMovies = 0,
                    TotalShowTimes = 0,
                    TotalConcessions = 0,
                    TotalRevenue = 0,
                    TopMovies = new List<MovieScoreViewModel>()
                };

                ViewBag.ErrorMessage = "Có lỗi xảy ra khi tải dữ liệu thống kê phim.";
                return View(errorModel);
            }
        }

        // ===== NEW REAL-TIME TOP 5 MOVIES API =====
        [HttpGet]
        public async Task<JsonResult> GetTop5MoviesRealTime()
        {
            try
            {
                var endDate = DateTime.Now;
                var startDate = endDate.AddDays(-7); // Last 7 days

                var movieStats = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                    .Where(t => t.PurchaseTime >= startDate &&
                               t.ShowTime != null &&
                               t.ShowTime.Movie != null)
                    .GroupBy(t => t.ShowTime.MovieID)
                    .Select(g => new
                    {
                        MovieID = g.Key,
                        g.First().ShowTime.Movie.Title,
                        TicketsSold = g.Count(),
                        TotalRevenue = g.Sum(t => t.ShowTime.Price)
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .ThenByDescending(x => x.TicketsSold)
                    .Take(5)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = movieStats.Select((movie, index) => new
                    {
                        rank = index + 1,
                        movieId = movie.MovieID,
                        title = movie.Title,
                        ticketsSold = movie.TicketsSold,
                        revenue = movie.TotalRevenue,
                        revenueFormatted = string.Format("{0:N0} ₫", movie.TotalRevenue)
                    }),
                    lastUpdated = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Lỗi khi tải dữ liệu top phim" });
            }
        }

        // ===== SIMPLE AUTO SHOWTIME MANAGEMENT =====
        [HttpPost]
        public async Task<JsonResult> AutoManageShowtimes()
        {
            try
            {
                var result = await PerformSimpleAutoManagement();
                return Json(new
                {
                    success = true,
                    message = "Quản lý suất chiếu tự động hoàn thành",
                    addedCount = result.AddedCount,
                    removedCount = result.RemovedCount,
                    details = result.Details
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Lỗi khi quản lý suất chiếu tự động" });
            }
        }

        // Simplified auto management logic
        private async Task<SimpleAutoResult> PerformSimpleAutoManagement()
        {
            var result = new SimpleAutoResult();
            var now = DateTime.Now;
            var oneWeekAgo = now.AddDays(-7);

            try
            {
                // Get movie performance from last week
                var moviePerformance = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                    .Where(t => t.PurchaseTime >= oneWeekAgo &&
                               t.ShowTime != null &&
                               t.ShowTime.Movie != null)
                    .GroupBy(t => t.ShowTime.MovieID)
                    .Select(g => new
                    {
                        MovieID = g.Key,
                        MovieTitle = g.First().ShowTime.Movie.Title,
                        TicketsSold = g.Count(),
                        TotalRevenue = g.Sum(t => t.ShowTime.Price)
                    })
                    .ToListAsync();

                if (!moviePerformance.Any())
                {
                    result.Details.Add("Không có dữ liệu performance để phân tích");
                    return result;
                }

                // Calculate averages
                var avgRevenue = moviePerformance.Average(x => (double)x.TotalRevenue);
                var avgTickets = moviePerformance.Average(x => x.TicketsSold);

                // Identify top performers (above 120% of average)
                var topPerformers = moviePerformance
                    .Where(x => x.TotalRevenue >= (decimal)(avgRevenue * 1.2) ||
                               x.TicketsSold >= avgTickets * 1.2)
                    .OrderByDescending(x => x.TotalRevenue)
                    .Take(2) // Top 2 only
                    .ToList();

                // Identify poor performers (below 50% of average)
                var poorPerformers = moviePerformance
                    .Where(x => x.TotalRevenue <= (decimal)(avgRevenue * 0.5) &&
                               x.TicketsSold <= avgTickets * 0.5)
                    .ToList();

                // Remove some showtimes for poor performers
                foreach (var poor in poorPerformers)
                {
                    var removed = await RemovePoorPerformingShowtimes(poor.MovieID, poor.MovieTitle);
                    result.RemovedCount += removed;
                    if (removed > 0)
                    {
                        result.Details.Add($"Xóa {removed} suất chiếu của '{poor.MovieTitle}' (doanh thu thấp)");
                    }
                }

                // Add showtimes for top performers
                foreach (var top in topPerformers)
                {
                    var added = await AddShowtimesForTopMovie(top.MovieID, top.MovieTitle);
                    result.AddedCount += added;
                    if (added > 0)
                    {
                        result.Details.Add($"Thêm {added} suất chiếu cho '{top.MovieTitle}' (doanh thu cao)");
                    }
                }

                await _context.SaveChangesAsync();
                return result;
            }
            catch (Exception ex)
            {
                result.Details.Add($"Lỗi: {ex.Message}");
                return result;
            }
        }

        private async Task<int> RemovePoorPerformingShowtimes(int movieId, string movieTitle)
        {
            var now = DateTime.Now;

            // Get future showtimes with no tickets
            var showtimesToRemove = await _context.ShowTimes
                .Where(st => st.MovieID == movieId &&
                            st.StartTime > now.AddHours(3)) // At least 3 hours in future
                .Include(st => st.Tickets)
                .Where(st => !st.Tickets.Any()) // No tickets sold
                .OrderBy(st => st.StartTime)
                .Take(2) // Remove max 2 showtimes
                .ToListAsync();

            if (showtimesToRemove.Any())
            {
                _context.ShowTimes.RemoveRange(showtimesToRemove);
            }

            return showtimesToRemove.Count;
        }

        private async Task<int> AddShowtimesForTopMovie(int movieId, string movieTitle)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null) return 0;

            var now = DateTime.Now;
            var addedCount = 0;

            // Try to add 1-2 showtimes for next few days
            var targetTimes = new[]
            {
                now.AddDays(1).Date.AddHours(19), // Tomorrow 7 PM
                now.AddDays(2).Date.AddHours(21)  // Day after 9 PM
            };

            foreach (var targetTime in targetTimes)
            {
                var bestAuditorium = await FindAvailableAuditorium(targetTime, movie.DurationMinutes);
                if (bestAuditorium != null)
                {
                    var newShowtime = new ShowTime
                    {
                        MovieID = movieId,
                        AuditoriumID = bestAuditorium.ID,
                        StartTime = targetTime,
                        DurationMinutes = movie.DurationMinutes,
                        SubtitleLanguageID = 1, // Default
                        Is3D = false,
                        Price = GetOptimalPrice(movieId)
                    };

                    _context.ShowTimes.Add(newShowtime);
                    addedCount++;
                }
            }

            return addedCount;
        }

        private async Task<Auditorium> FindAvailableAuditorium(DateTime targetTime, int duration)
        {
            var auditoriums = await _context.Auditoriums.ToListAsync();

            foreach (var auditorium in auditoriums)
            {
                // Check for conflicts
                var hasConflict = await _context.ShowTimes
                    .AnyAsync(st => st.AuditoriumID == auditorium.ID &&
                                   targetTime < st.StartTime.AddMinutes(st.DurationMinutes + 30) &&
                                   targetTime.AddMinutes(duration + 30) > st.StartTime);

                if (!hasConflict)
                {
                    return auditorium;
                }
            }

            return null;
        }

        private decimal GetOptimalPrice(int movieId)
        {
            var avgPrice = _context.ShowTimes
                .Where(st => st.MovieID == movieId)
                .Select(st => st.Price)
                .DefaultIfEmpty(10.0m)
                .Average();

            return Math.Max(5.0m, Math.Min(20.0m, avgPrice * 1.05m));
        }

        // ===== THAY THẾ METHOD GetRevenueChartData =====
        [HttpGet]
        public async Task<JsonResult> GetRevenueChartData(string period = "7days")
        {
            try
            {
                var endDate = DateTime.Now;
                var startDate = period switch
                {
                    "7days" => endDate.AddDays(-6),
                    "30days" => endDate.AddDays(-29),
                    "12months" => endDate.AddMonths(-11),
                    _ => endDate.AddDays(-6)
                };

                // ✅ SỬA: Tách riêng query để tránh lỗi GroupBy phức tạp
                var tickets = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .Where(t => t.PurchaseTime >= startDate && t.PurchaseTime <= endDate)
                    .Select(t => new
                    {
                        t.PurchaseTime,
                        t.ShowTime.Price
                    })
                    .ToListAsync();

                // ✅ Group data in memory instead of database
                IEnumerable<object> groupedData;

                if (period == "12months")
                {
                    groupedData = tickets
                        .GroupBy(t => new { t.PurchaseTime.Year, t.PurchaseTime.Month })
                        .Select(g => new
                        {
                            Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                            Revenue = g.Sum(t => t.Price),
                            TicketCount = g.Count()
                        })
                        .OrderBy(x => x.Date);
                }
                else
                {
                    groupedData = tickets
                        .GroupBy(t => new { t.PurchaseTime.Year, t.PurchaseTime.Month, t.PurchaseTime.Day })
                        .Select(g => new
                        {
                            Date = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                            Revenue = g.Sum(t => t.Price),
                            TicketCount = g.Count()
                        })
                        .OrderBy(x => x.Date);
                }

                var revenueData = groupedData.ToList();

                var labels = revenueData.Select(r => period == "12months"
                    ? ((dynamic)r).Date.ToString("MM/yyyy")
                    : ((dynamic)r).Date.ToString("dd/MM")).ToArray();

                var revenues = revenueData.Select(r => ((dynamic)r).Revenue).ToArray();
                var tickets_count = revenueData.Select(r => ((dynamic)r).TicketCount).ToArray();

                return Json(new
                {
                    labels,
                    revenues,
                    tickets = tickets_count
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = "Lỗi khi tải dữ liệu doanh thu" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetMoviePerformanceData()
        {
            try
            {
                var movieData = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                    .Where(t => t.ShowTime != null && t.ShowTime.Movie != null)
                    .GroupBy(t => t.ShowTime.Movie.Title)
                    .Select(g => new
                    {
                        MovieTitle = g.Key,
                        TicketsSold = g.Count(),
                        Revenue = g.Sum(t => t.ShowTime.Price)
                    })
                    .OrderByDescending(x => x.TicketsSold)
                    .Take(5)
                    .ToListAsync();

                var labels = movieData.Select(m => m.MovieTitle).ToArray();
                var ticketCounts = movieData.Select(m => m.TicketsSold).ToArray();
                var colors = new[] { "#0d6efd", "#198754", "#ffc107", "#dc3545", "#6c757d" };

                return Json(new
                {
                    labels,
                    data = ticketCounts,
                    colors
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = "Lỗi khi tải dữ liệu phim" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTheaterPerformanceData()
        {
            try
            {
                var theaterData = await _context.ShowTimes
                    .Include(st => st.Auditorium)
                    .ThenInclude(a => a.Theater)
                    .Include(st => st.Tickets)
                    .Where(st => st.Auditorium != null && st.Auditorium.Theater != null)
                    .GroupBy(st => st.Auditorium.Theater.Name)
                    .Select(g => new
                    {
                        TheaterName = g.Key,
                        TotalShows = g.Count(),
                        TicketsSold = g.SelectMany(st => st.Tickets).Count(),
                        TotalCapacity = g.Count() * 100 // Assume 100 seats per show
                    })
                    .ToListAsync();

                var labels = theaterData.Select(t => t.TheaterName).ToArray();
                var performancePercentages = theaterData.Select(t =>
                    t.TotalCapacity > 0 ? Math.Round((double)t.TicketsSold / t.TotalCapacity * 100, 1) : 0
                ).ToArray();

                return Json(new
                {
                    labels,
                    data = performancePercentages
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = "Lỗi khi tải dữ liệu rạp chiếu" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetRecentActivity()
        {
            try
            {
                var activities = new List<object>();

                // Recent movies
                var recentMovies = await _context.Movies
                    .OrderByDescending(m => m.ID)
                    .Take(2)
                    .Select(m => new
                    {
                        Type = "movie",
                        Icon = "fas fa-film",
                        Color = "primary",
                        Title = "Phim mới được thêm",
                        Description = $"{m.Title} - {GetTimeAgo(DateTime.Now.AddHours(-new Random().Next(1, 24)))}"
                    })
                    .ToListAsync();

                // Recent tickets
                var recentTickets = await _context.Tickets
                    .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                    .OrderByDescending(t => t.PurchaseTime)
                    .Take(2)
                    .Select(t => new
                    {
                        Type = "ticket",
                        Icon = "fas fa-ticket-alt",
                        Color = "success",
                        Title = "Vé mới được đặt",
                        Description = $"1 vé cho {t.ShowTime.Movie.Title} - {GetTimeAgo(t.PurchaseTime)}"
                    })
                    .ToListAsync();

                activities.AddRange(recentMovies.Cast<object>());
                activities.AddRange(recentTickets.Cast<object>());

                // Shuffle and take 5
                var random = new Random();
                var shuffledActivities = activities.OrderBy(x => random.Next()).Take(5);

                return Json(shuffledActivities);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Lỗi khi tải hoạt động gần đây" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetDashboardStats()
        {
            try
            {
                var stats = new
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalMovies = await _context.Movies.CountAsync(),
                    TotalShowTimes = await _context.ShowTimes.CountAsync(),
                    TotalRevenue = await _context.Tickets
                        .Include(t => t.ShowTime)
                        .SumAsync(t => t.ShowTime.Price),
                    TodayTickets = await _context.Tickets
                        .Where(t => t.PurchaseTime.Date == DateTime.Today)
                        .CountAsync(),
                    ThisMonthRevenue = await _context.Tickets
                        .Include(t => t.ShowTime)
                        .Where(t => t.PurchaseTime.Month == DateTime.Now.Month &&
                                   t.PurchaseTime.Year == DateTime.Now.Year)
                        .SumAsync(t => t.ShowTime.Price)
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Lỗi khi tải thống kê" });
            }
        }

        // Helper method to calculate time ago
        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} phút trước";
            else if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} giờ trước";
            else if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} ngày trước";
            else
                return dateTime.ToString("dd/MM/yyyy");
        }

        // Add a simple Home action to handle navigation to main dashboard
        public IActionResult Home()
        {
            return RedirectToAction("Index");
        }
    }

    // Simple result class for auto management
    public class SimpleAutoResult
    {
        public int AddedCount { get; set; } = 0;
        public int RemovedCount { get; set; } = 0;
        public List<string> Details { get; set; } = new List<string>();
    }
}